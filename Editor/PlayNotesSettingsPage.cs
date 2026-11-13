using System;
using System.Collections.Generic;
using PlayNotes;
using UnityEditor;
using UnityEngine;

namespace PlayNotesEditor
{
    // Project Settings > PlayNotes. Each change is written at once to ProjectSettings/PlayNotesSettings.json, which goes into
    // version control with the project, so a team shares one set. Play mode reads it when it starts.
    internal static class PlayNotesSettingsPage
    {
        static readonly string[] GateChoices = { "development", "all", "never" };
        static readonly string[] GateLabels = { "Development builds", "All builds", "Never (I place the file myself)" };

        static PlayNotesSettings settings;
        // (the lists as they are being typed: read back from the settings they would lose the space or the line break just entered)
        static string keysText, foldersText, micsText;

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/PlayNotes", SettingsScope.Project)
            {
                label = "PlayNotes",
                activateHandler = (_, __) => Read(),
                guiHandler = _ => Draw(),
                keywords = new HashSet<string>(new[] { "PlayNotes", "voice", "playtest", "notes", "microphone", "F8", "feedback" })
            };
        }

        static void Read()
        {
            settings = PlayNotesProject.Load();
            keysText = string.Join(" ", settings.watchedKeys);
            foldersText = string.Join("\n", settings.snapshotFolders);
            micsText = string.Join("\n", settings.skipMicrophones);
        }

        static void Draw()
        {
            if (settings == null) Read();
            float was = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 210;
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);
            settings.toggleKey = EditorGUILayout.DelayedTextField(new GUIContent("Key", "Starts and stops a session. A key's name: F8, F9, Backquote... Empty: no key, sessions start from code only."), settings.toggleKey);
            if (settings.toggleKey.Trim().Length > 0 && !new WatchedKey(settings.toggleKey).Valid)
                EditorGUILayout.HelpBox("This project's input does not know a key called \"" + settings.toggleKey + "\".", MessageType.Warning);
            settings.showHint = EditorGUILayout.Toggle(new GUIContent("Name the key at launch", "Shows the key on screen for a few seconds when the game starts."), settings.showHint);
            keysText = EditorGUILayout.DelayedTextField(new GUIContent("Keys noted in the trail", "Presses of these keys are written down, with spaces between their names. Never every key: what a player types into the game is not kept."), keysText);
            settings.shotSecondsSpeaking = EditorGUILayout.DelayedFloatField(new GUIContent("Picture every (speaking)", "Seconds between pictures while a voice is heard."), settings.shotSecondsSpeaking);
            settings.shotSecondsQuiet = EditorGUILayout.DelayedFloatField(new GUIContent("Picture every (quiet)", "Seconds between pictures while nobody speaks."), settings.shotSecondsQuiet);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Speech to text", EditorStyles.boldLabel);
            settings.language = EditorGUILayout.DelayedTextField(new GUIContent("Language spoken", "A code such as ja or en. Empty: detected from the voice."), settings.language);
            EditorGUILayout.LabelField(new GUIContent("The game's own words", "Names and terms a general speech model has never met, with spaces between them. The single thing that most improves what is written out."));
            settings.words = EditorGUILayout.TextArea(settings.words, GUILayout.MinHeight(48));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);
            settings.sessionsFolder = EditorGUILayout.DelayedTextField(new GUIContent("Sessions folder", "Empty: a PlayNotes folder under Application.persistentDataPath."), settings.sessionsFolder);
            // (on a line of its own and wrapped: a path under AppData is longer than the field)
            EditorGUILayout.LabelField(PlayNotesProject.Portable(PlayNotesSettings.FromJson(settings.ToJson()).SessionsRoot()), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(new GUIContent("Folders copied with each session", "One to a line: the save files, usually. Copied at the start and at the end, so a remark can be reproduced. Relative paths are under Application.persistentDataPath."));
            foldersText = EditorGUILayout.TextArea(foldersText, GUILayout.MinHeight(32));
            EditorGUILayout.LabelField(new GUIContent("Sound inputs to leave alone", "One to a line: a part of the device's name. Loopback devices (Stereo Mix, VB-Audio, Voicemeeter...) are left alone already."));
            micsText = EditorGUILayout.TextArea(micsText, GUILayout.MinHeight(32));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Builds", EditorStyles.boldLabel);
            int gate = Mathf.Max(0, Array.IndexOf(GateChoices, settings.gateFileInBuilds));
            gate = EditorGUILayout.Popup(new GUIContent("Write " + PlayNotesSettings.GateFileName + " beside", "In a build PlayNotes wakes only if this file stands beside the exe. A build that should not have it has it taken away."), gate, GateLabels);
            settings.gateFileInBuilds = GateChoices[gate];

            if (EditorGUI.EndChangeCheck())
            {
                settings.watchedKeys = Split(keysText, ' ', ',');
                settings.snapshotFolders = Split(foldersText, '\n');
                settings.skipMicrophones = Split(micsText, '\n');
                // (saved through the same mending a load does; the page keeps what is being typed)
                PlayNotesProject.Save(PlayNotesSettings.FromJson(settings.ToJson()));
            }

            if (PlayNotesProject.CompiledOut())
                EditorGUILayout.HelpBox(PlayNotesProject.DisableSymbol + " is defined for the selected platform: PlayNotes is not compiled into its builds at all, with or without the file.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("A build without " + PlayNotesSettings.GateFileName + " beside the exe never opens a microphone. To leave the code out of a build altogether, add " + PlayNotesProject.DisableSymbol + " to its Scripting Define Symbols.", MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open latest session")) PlayNotesProject.OpenFolder(PlayNotesProject.LatestSession());
                if (GUILayout.Button("Write new sessions out as words")) PlayNotesProject.Transcribe(PlayNotesSettings.FromJson(settings.ToJson()).SessionsRoot());
                if (GUILayout.Button("Install agent skill")) PlayNotesProject.InstallAgentSkill();
            }
            EditorGUIUtility.labelWidth = was;
        }

        internal static string[] Split(string text, params char[] at)
        {
            var parts = (text ?? "").Split(at, StringSplitOptions.RemoveEmptyEntries);
            var kept = new List<string>();
            foreach (var part in parts)
            {
                string s = part.Trim();
                if (s.Length > 0) kept.Add(s);
            }
            return kept.ToArray();
        }
    }
}
