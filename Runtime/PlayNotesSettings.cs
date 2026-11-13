using System;
using System.IO;
using UnityEngine;

namespace PlayNotes
{
    /// <summary>
    /// The few things that differ from one game to the next. Edited in Project Settings &gt; PlayNotes and kept as plain JSON:
    /// in the project as <c>ProjectSettings/PlayNotesSettings.json</c>, in a build as the content of <c>playnotes.on</c>
    /// beside the exe (an empty file there means "awake, with the defaults").
    /// </summary>
    [Serializable]
    public sealed class PlayNotesSettings
    {
        /// <summary>The file whose presence beside the exe wakes PlayNotes in a build.</summary>
        public const string GateFileName = "playnotes.on";

        /// <summary>The project's copy, under ProjectSettings.</summary>
        public const string ProjectFileName = "PlayNotesSettings.json";

        /// <summary>The key that starts and stops a session. Empty: no key, only <see cref="Recorder.Begin"/>.</summary>
        public string toggleKey = "F8";

        /// <summary>The language spoken, as a code for the speech-to-text step ("ja", "en"). Empty: it is detected.</summary>
        public string language = "";

        /// <summary>The game's own words, which a general speech model has never met. Spaces between them.</summary>
        public string words = "";

        /// <summary>Keys whose presses go into the trail. Never every key: a name typed into the game is not ours to keep.</summary>
        public string[] watchedKeys = { "Escape", "Enter", "Tab", "Space", "E", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        /// <summary>Seconds between pictures while a voice is heard.</summary>
        public float shotSecondsSpeaking = 1.5f;

        /// <summary>Seconds between pictures while it is quiet.</summary>
        public float shotSecondsQuiet = 6f;

        /// <summary>Folders copied at the start and the end of a session. See <see cref="Recorder.AddSnapshotFolder"/>.</summary>
        public string[] snapshotFolders = new string[0];

        /// <summary>Where sessions are kept. Empty: <c>Application.persistentDataPath/PlayNotes</c>.</summary>
        public string sessionsFolder = "";

        /// <summary>Parts of the names of sound inputs to leave alone, besides the loopback devices already known.</summary>
        public string[] skipMicrophones = new string[0];

        /// <summary>Which builds get <c>playnotes.on</c> written beside them: "development", "all" or "never".</summary>
        public string gateFileInBuilds = "development";

        /// <summary>Whether the key is named on screen for a few seconds at launch.</summary>
        public bool showHint = true;

        /// <summary>The folder sessions go into, made absolute.</summary>
        public string SessionsRoot()
        {
            if (string.IsNullOrWhiteSpace(sessionsFolder)) return Path.Combine(Application.persistentDataPath, "PlayNotes");
            return Path.IsPathRooted(sessionsFolder) ? sessionsFolder : Path.Combine(Application.persistentDataPath, sessionsFolder);
        }

        /// <summary>The settings as indented JSON.</summary>
        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>Settings from JSON. What the text leaves out keeps its default; text that is empty or not JSON gives the defaults.</summary>
        public static PlayNotesSettings FromJson(string json)
        {
            var s = new PlayNotesSettings();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json, s);
                }
                catch (ArgumentException)
                {
                    s = new PlayNotesSettings();
                }
            }
            s.Mend();
            return s;
        }

        /// <summary>Settings from a file. A missing file gives the defaults.</summary>
        public static PlayNotesSettings Load(string path)
        {
            try
            {
                return FromJson(File.Exists(path) ? File.ReadAllText(path) : "");
            }
            catch (IOException)
            {
                return FromJson("");
            }
        }

        void Mend()
        {
            toggleKey = (toggleKey ?? "").Trim();
            language = (language ?? "").Trim();
            words = words ?? "";
            sessionsFolder = (sessionsFolder ?? "").Trim();
            gateFileInBuilds = (gateFileInBuilds ?? "").Trim().ToLowerInvariant();
            if (gateFileInBuilds != "all" && gateFileInBuilds != "never") gateFileInBuilds = "development";
            if (watchedKeys == null) watchedKeys = new string[0];
            if (snapshotFolders == null) snapshotFolders = new string[0];
            if (skipMicrophones == null) skipMicrophones = new string[0];
            shotSecondsSpeaking = Mathf.Clamp(shotSecondsSpeaking, .5f, 60f);
            shotSecondsQuiet = Mathf.Clamp(shotSecondsQuiet, shotSecondsSpeaking, 600f);
        }
    }
}
