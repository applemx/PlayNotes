#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !PLAYNOTES_DISABLED
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace PlayNotes
{
    // The one object PlayNotes puts into a running game, and only where it is allowed to wake (see Wake below).
    // It watches for the key, and between the start and the end of a session writes the trail and keeps the microphones and the pictures going.
    [AddComponentMenu("")]
    internal sealed class RecorderHost : MonoBehaviour
    {
        const float WarnAfter = 12f;
        const float HitchSeconds = .05f;
        const float StartupSeconds = .5f;
        const long LargestSnapshotFile = 16L * 1024 * 1024;
        const int MostSnapshotFiles = 200;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        internal static RecorderHost Instance { get; private set; }

        PlayNotesSettings settings;
        WatchedKey toggle;
        WatchedKey[] watched;
        Badge badge;

        bool on;
        string folder = "";
        Stopwatch clock;
        Trail trail;
        Microphones mics;
        WindowShots shots;

        float nextPlace, nextMove, startedAt, noteUntil, shownLevel;
        string lastWhere;
        string noteWords = "";
        Vector3 lastAt;
        float lastYaw;
        bool heard, whereFailed;

        internal bool On => on;

        internal string Folder => folder;

        // Where PlayNotes wakes:
        //   in the Windows editor, always;
        //   in a Windows build, only with the file playnotes.on beside the exe (the editor writes it for development builds, see the settings);
        //   nowhere else, and never in batch mode. With PLAYNOTES_DISABLED defined none of this is compiled at all.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Wake()
        {
            if (Instance != null) return;
            if (Application.isBatchMode) return;
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer) return;
            string beside = Path.GetDirectoryName(Application.dataPath) ?? "";
            string path = Application.isEditor
                ? Path.Combine(beside, "ProjectSettings", PlayNotesSettings.ProjectFileName)
                : Path.Combine(beside, PlayNotesSettings.GateFileName);
            if (!Application.isEditor && !File.Exists(path)) return;
            var go = new GameObject("PlayNotes");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RecorderHost>();
            Instance.Setup(PlayNotesSettings.Load(path));
        }

        void Setup(PlayNotesSettings loaded)
        {
            settings = loaded;
            toggle = new WatchedKey(settings.toggleKey);
            if (settings.toggleKey.Length > 0 && !toggle.Valid) Debug.LogWarning("[PlayNotes] the key \"" + settings.toggleKey + "\" is not one this project's input knows; sessions can only be started from code.");
            watched = new WatchedKey[settings.watchedKeys.Length];
            for (int i = 0; i < watched.Length; i++) watched[i] = new WatchedKey(settings.watchedKeys[i]);
            badge = gameObject.AddComponent<Badge>();
            badge.enabled = false;
            if (settings.showHint && toggle.Valid) Say(Words.Hint(toggle.Name), 8f);
        }

        void Update()
        {
            if (settings == null) return;
            if (toggle.Pressed())
            {
                if (on) Stop();
                else Begin();
            }
            ShowBadge();
            if (!on) return;
            shots.PointerFree = Cursor.lockState != CursorLockMode.Locked && Cursor.visible;
            WatchPlace();
            if (WatchedKey.Clicked(out var at)) NoteClick(at);
            foreach (var key in watched)
                if (key.Pressed()) trail.Line("key", key.Name);
            WatchMove();
            // (not in a session's first half second: opening the microphones and drawing the badge for the first time take about a tenth
            // of a second between them, and those long frames are ours, not the game's)
            if (Time.unscaledDeltaTime > HitchSeconds && Time.unscaledTime - startedAt > StartupSeconds) trail.Line("hitch", (Time.unscaledDeltaTime * 1000f).ToString("0", Invariant));
        }

        void OnApplicationQuit() => Stop();

        void OnDestroy() => Stop();

        internal string Begin()
        {
            if (on) return folder;
            try
            {
                clock = Stopwatch.StartNew();
                string root = settings.SessionsRoot();
                string name = DateTime.Now.ToString("yyyyMMdd-HHmmss", Invariant);
                folder = Path.Combine(root, name);
                for (int again = 2; Directory.Exists(folder); again++) folder = Path.Combine(root, name + "-" + again);
                Directory.CreateDirectory(Path.Combine(folder, "shots"));
                trail = new Trail(Path.Combine(folder, "trail.txt"), clock);
                trail.Line("format", Trail.FormatVersion);
                trail.Line("start", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Invariant));
                trail.Line("app", Application.productName + " " + Application.version + (Application.isEditor ? " (editor)" : ""));
                trail.Line("unity", Application.unityVersion);
                trail.Line("screen", Screen.width + "x" + Screen.height);
                if (settings.language.Length > 0) trail.Line("language", settings.language);
                if (settings.words.Trim().Length > 0) trail.Line("words", settings.words);
                CopySnapshots("save_start");
                mics = Microphones.Open(folder, trail, clock, settings.skipMicrophones);
                var listening = mics;
                shots = WindowShots.Start(folder, clock, () => listening.VoiceAt, settings.shotSecondsSpeaking, settings.shotSecondsQuiet);
                if (shots.Trouble.Length > 0) trail.Line("trouble", shots.Trouble);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayNotes] could not begin: " + e);
                mics?.Close();
                shots?.Close();
                trail?.Dispose();
                mics = null;
                shots = null;
                trail = null;
                Say(Words.CouldNotBegin, 6f);
                return null;
            }
            on = true;
            heard = false;
            whereFailed = false;
            lastWhere = null;
            nextPlace = 0;
            nextMove = 0;
            startedAt = Time.unscaledTime;
            Say(mics.Count > 0 ? Words.Recording(toggle.Valid ? toggle.Name : "") : Words.NoMicrophone, 4f);
            Debug.Log("[PlayNotes] began " + folder);
            Recorder.RaiseBegan(folder);
            return folder;
        }

        internal void Stop()
        {
            if (!on) return;
            on = false;
            shots.Close();
            mics.Close();
            if (shots.Trouble.Length > 0) trail.Line("trouble", shots.Trouble);
            if (mics.Trouble.Length > 0) trail.Line("trouble", mics.Trouble);
            CopySnapshots("save_end");
            trail.Line("end", shots.Count + " shots");
            trail.Dispose();
            trail = null;
            if (Application.isEditor) Say(Words.StoppedInEditor, 5f);
            else Say(Words.Stopped, 3f);
            Debug.Log("[PlayNotes] stopped " + folder);
            Recorder.RaiseStopped(folder);
        }

        internal void Mark(string kind, string words)
        {
            if (on) trail.Line(kind, words);
        }

        internal void NoteClick(Vector2 at)
        {
            if (!on) return;
            shots.SoonAfterClick();
            // (written from the top left, as a picture is counted, so that a click can be found in the pictures by its numbers)
            trail.Line("click", ClickLabels.Describe(at) + "  @" + at.x.ToString("0", Invariant) + "," + (Screen.height - at.y).ToString("0", Invariant));
        }

        internal string Status()
        {
            var sb = new StringBuilder();
            sb.Append(on ? "on " : "off ").Append(folder);
            if (shots != null) sb.Append(" shots ").Append(shots.Count);
            if (mics != null) mics.Describe(sb);
            if (shots != null && shots.Trouble.Length > 0) sb.Append(" | trouble: ").Append(shots.Trouble);
            if (mics != null && mics.Trouble.Length > 0) sb.Append(" | trouble: ").Append(mics.Trouble);
            return sb.ToString();
        }

        void WatchPlace()
        {
            if (Time.unscaledTime < nextPlace) return;
            nextPlace = Time.unscaledTime + .25f;
            string where = null;
            var ask = Recorder.Where;
            if (ask != null && !whereFailed)
            {
                try
                {
                    where = ask();
                }
                catch (Exception e)
                {
                    // (once: a hook that throws would otherwise throw four times a second)
                    whereFailed = true;
                    trail.Line("trouble", "Recorder.Where threw: " + e.Message);
                    Debug.LogException(e);
                }
            }
            if (string.IsNullOrEmpty(where)) where = SceneManager.GetActiveScene().name;
            if (where == lastWhere) return;
            lastWhere = where;
            trail.Line("place", where);
        }

        void WatchMove()
        {
            if (Time.unscaledTime < nextMove) return;
            nextMove = Time.unscaledTime + 1f;
            var eye = Camera.main;
            if (eye == null) return;
            var at = eye.transform.position;
            float yaw = eye.transform.eulerAngles.y;
            if (Vector3.Distance(at, lastAt) < 1f && Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) < 20f) return;
            lastAt = at;
            lastYaw = yaw;
            trail.Line("at", at.x.ToString("0.0", Invariant) + " " + at.y.ToString("0.0", Invariant) + " " + at.z.ToString("0.0", Invariant) + " yaw " + yaw.ToString("0", Invariant));
        }

        void CopySnapshots(string into)
        {
            int n = 0;
            foreach (var from in settings.snapshotFolders) CopySnapshot(from, into, ref n);
            foreach (var from in Recorder.SnapshotFolders) CopySnapshot(from, into, ref n);
        }

        void CopySnapshot(string from, string into, ref int n)
        {
            if (string.IsNullOrWhiteSpace(from)) return;
            try
            {
                string source = Path.IsPathRooted(from) ? from : Path.Combine(Application.persistentDataPath, from);
                if (!Directory.Exists(source)) return;
                string to = Path.Combine(folder, into, new DirectoryInfo(source).Name);
                Directory.CreateDirectory(to);
                foreach (var file in Directory.GetFiles(source))
                {
                    if (n >= MostSnapshotFiles) return;
                    if (new FileInfo(file).Length > LargestSnapshotFile) continue;
                    File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
                    n++;
                }
            }
            catch (Exception e)
            {
                trail.Line("trouble", "snapshot of " + from + ": " + e.Message);
            }
        }

        void Say(string words, float seconds)
        {
            noteWords = words;
            noteUntil = Time.unscaledTime + seconds;
        }

        void ShowBadge()
        {
            if (badge == null) return;
            float level = 0;
            if (on)
            {
                level = mics.Level;
                if (mics.Voice) heard = true;
            }
            shownLevel = Mathf.Max(level, shownLevel - Time.unscaledDeltaTime * 1.5f);
            string line = "";
            if (Time.unscaledTime < noteUntil) line = noteWords;
            else if (on && !heard && mics.Count > 0 && Time.unscaledTime - startedAt > WarnAfter) line = Words.NothingHeard;
            badge.Recording = on;
            badge.Level = shownLevel;
            badge.Line = line;
            bool show = on || line.Length > 0;
            if (badge.enabled != show) badge.enabled = show;
        }
    }
}
#endif
