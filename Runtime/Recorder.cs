#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !PLAYNOTES_DISABLED
#define PLAYNOTES_ON
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayNotes
{
    /// <summary>
    /// The whole public face of PlayNotes.
    /// <para>
    /// Nothing here is needed for PlayNotes to work: install the package, press the key (F8 unless changed), play and talk.
    /// These members let a game say more than a picture can (<see cref="Where"/>, <see cref="Mark"/>) and let a test or a
    /// tool start and stop a session without the key.
    /// </para>
    /// <para>
    /// Every member is safe to call anywhere. Where PlayNotes does not run (other platforms, a build without the
    /// <c>playnotes.on</c> file, or with <c>PLAYNOTES_DISABLED</c> defined) they do nothing and cost nothing.
    /// </para>
    /// </summary>
    public static class Recorder
    {
        static readonly List<string> snapshotFolders = new List<string>();

        /// <summary>
        /// Where the player is, in a few words: "Workbench / order 12", "Level 3 / shop". Asked four times a second while a
        /// session is on; a line goes into the trail whenever the answer changes. Without it the active scene's name is used.
        /// </summary>
        public static Func<string> Where { get; set; }

        /// <summary>A session has begun. The argument is its folder.</summary>
        public static event Action<string> Began;

        /// <summary>A session has ended and its files are closed. The argument is its folder.</summary>
        public static event Action<string> Stopped;

        /// <summary>True where PlayNotes is awake: the Windows editor, or a Windows build with <c>playnotes.on</c> beside the exe.</summary>
        public static bool IsAvailable
        {
            get
            {
#if PLAYNOTES_ON
                return RecorderHost.Instance != null;
#else
                return false;
#endif
            }
        }

        /// <summary>True between the start and the end of a session.</summary>
        public static bool IsRecording
        {
            get
            {
#if PLAYNOTES_ON
                return RecorderHost.Instance != null && RecorderHost.Instance.On;
#else
                return false;
#endif
            }
        }

        /// <summary>The folder of the session that is on, or of the last one. Empty before the first.</summary>
        public static string SessionFolder
        {
            get
            {
#if PLAYNOTES_ON
                return RecorderHost.Instance != null ? RecorderHost.Instance.Folder : "";
#else
                return "";
#endif
            }
        }

        /// <summary>Begins a session, as the key does. Returns its folder, or null where PlayNotes is not awake.</summary>
        public static string Begin()
        {
#if PLAYNOTES_ON
            return RecorderHost.Instance != null ? RecorderHost.Instance.Begin() : null;
#else
            return null;
#endif
        }

        /// <summary>Ends the session, as the key does. Closing the game ends it too.</summary>
        public static void Stop()
        {
#if PLAYNOTES_ON
            if (RecorderHost.Instance != null) RecorderHost.Instance.Stop();
#endif
        }

        /// <summary>Begins a session if none is on, ends it otherwise.</summary>
        public static void Toggle()
        {
            if (IsRecording) Stop();
            else Begin();
        }

        /// <summary>
        /// Writes a line of the game's own into the trail: <c>Recorder.Mark("order", "accepted #12")</c>.
        /// The kind is one word; it is shown beside whatever is said in the seconds after it.
        /// </summary>
        public static void Mark(string kind, string words)
        {
#if PLAYNOTES_ON
            if (RecorderHost.Instance != null) RecorderHost.Instance.Mark(kind, words);
#endif
        }

        /// <summary>
        /// A folder to copy at the start and at the end of every session (the save files, usually), so a remark can be
        /// reproduced from the state it was made in. Relative paths are under <c>Application.persistentDataPath</c>.
        /// Only the files directly in the folder are copied, each up to 16 MB.
        /// </summary>
        public static void AddSnapshotFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (!snapshotFolders.Contains(folder)) snapshotFolders.Add(folder);
        }

        /// <summary>
        /// For automated runs, which press buttons past the mouse: records a click at this screen position (pixels from the
        /// bottom left, as <c>Input.mousePosition</c>) exactly as a real one would be. In the trail it is written from the
        /// top left, as the pictures are counted.
        /// </summary>
        public static void NoteClickAt(Vector2 screenPosition)
        {
#if PLAYNOTES_ON
            if (RecorderHost.Instance != null) RecorderHost.Instance.NoteClick(screenPosition);
#endif
        }

        /// <summary>One line on the state of the session: folder, pictures taken, each microphone's seconds and loudest level, any trouble.</summary>
        public static string Status()
        {
#if PLAYNOTES_ON
            return RecorderHost.Instance != null ? RecorderHost.Instance.Status() : "PlayNotes is not awake in this build";
#else
            return "PlayNotes is not compiled into this build";
#endif
        }

        internal static IReadOnlyList<string> SnapshotFolders => snapshotFolders;

        internal static void RaiseBegan(string folder)
        {
            try
            {
                Began?.Invoke(folder);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static void RaiseStopped(string folder)
        {
            try
            {
                Stopped?.Invoke(folder);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // (with the editor's domain reload switched off, last play's hooks would still be standing)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            Where = null;
            Began = null;
            Stopped = null;
            snapshotFolders.Clear();
        }
    }
}
