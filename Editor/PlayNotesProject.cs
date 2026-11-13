using System;
using System.Diagnostics;
using System.IO;
using PlayNotes;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PlayNotesEditor
{
    // What the editor side knows about this project: its settings file, its sessions, and where the package's own files are.
    internal static class PlayNotesProject
    {
        internal const string DisableSymbol = "PLAYNOTES_DISABLED";

        static string ProjectFolder => Directory.GetParent(Application.dataPath).FullName;

        internal static string SettingsPath => Path.Combine(ProjectFolder, "ProjectSettings", PlayNotesSettings.ProjectFileName);

        internal static PlayNotesSettings Load() => PlayNotesSettings.Load(SettingsPath);

        internal static void Save(PlayNotesSettings settings)
        {
            File.WriteAllText(SettingsPath, settings.ToJson() + "\n");
        }

        /// <summary>The newest session's folder, or null when there is none yet.</summary>
        internal static string LatestSession()
        {
            string root = Load().SessionsRoot();
            if (!Directory.Exists(root)) return null;
            string latest = null;
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (!File.Exists(Path.Combine(dir, "trail.txt"))) continue;
                if (latest == null || string.CompareOrdinal(Path.GetFileName(dir), Path.GetFileName(latest)) > 0) latest = dir;
            }
            return latest;
        }

        /// <summary>A file of this package by its path inside it ("Transcribe~/notes.py"), wherever the package was installed.</summary>
        internal static string PackageFile(string inside)
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Recorder).Assembly);
            if (info == null) return null;
            string path = Path.Combine(info.resolvedPath, inside);
            return File.Exists(path) ? path : null;
        }

        // What the editor side has to say, said without ever stopping the editor: a line in the Console and a notice that fades
        // from the window in front. Never a dialog with a button. An editor is as often driven by a script or an AI agent as by a
        // hand on the mouse, and a dialog that waits for a click leaves those waiting for ever.
        internal static void Tell(string words, bool problem = false)
        {
            if (problem) Debug.LogWarning("[PlayNotes] " + words);
            else Debug.Log("[PlayNotes] " + words);
            if (Application.isBatchMode) return;
            var window = EditorWindow.focusedWindow;
            if (window != null) window.ShowNotification(new GUIContent(words), 4.0);
        }

        internal static void OpenFolder(string folder)
        {
            if (folder == null || !Directory.Exists(folder))
            {
                Tell("No session yet. Enter Play mode, press the key, talk, and press it again.");
                return;
            }
            EditorUtility.RevealInFinder(Path.Combine(folder, "trail.txt"));
        }

        /// <summary>
        /// Opens a console that writes the voice of a session, or of every session not yet written out, as words.
        /// The work is done by Transcribe~/notes.py through uv, which fetches what the script needs the first time.
        /// </summary>
        internal static void Transcribe(string target)
        {
            string script = PackageFile("Transcribe~/notes.py");
            if (script == null)
            {
                Tell("Transcribe~/notes.py was not found inside the package.", true);
                return;
            }
            if (target == null || !Directory.Exists(target))
            {
                Tell("No session to write out yet.");
                return;
            }
            string command = "uv run \"" + script + "\" \"" + target + "\"";
            if (Application.isBatchMode)
            {
                // (no console of its own where there is no one to read it: the command is there to be run by whoever drives this)
                Debug.Log("[PlayNotes] to write the sessions out: " + command);
                return;
            }
            if (!OnPath("uv"))
            {
                // (said here, in words: the console's own "'uv' is not recognized as an internal or external command" tells a newcomer nothing)
                Tell("uv was not found. It runs the speech-to-text script: get it from https://docs.astral.sh/uv/ and choose this again. If you have only just installed it, restart Unity and the Hub so that they see it.", true);
                return;
            }
            try
            {
                // (/k: the window stays, so the result can be read. It is a window beside the editor; the editor does not wait for it)
                Process.Start(new ProcessStartInfo("cmd.exe", "/k " + command) { UseShellExecute = true });
                Debug.Log("[PlayNotes] writing the sessions out in a console of its own: " + command);
            }
            catch (Exception e)
            {
                Tell("Could not open a console: " + e.Message, true);
            }
        }

        // Whether a program can be started by its name from here. Unsure counts as yes: then the console says what is wrong.
        internal static bool OnPath(string program)
        {
            try
            {
                using (var where = Process.Start(new ProcessStartInfo("where.exe", program) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    if (!where.WaitForExit(3000)) return true;
                    return where.ExitCode == 0;
                }
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Copies the reading rules for an AI coding agent, and the speech-to-text script they call, into this project
        /// (.claude/skills/playnotes), and notes beside them where this project's sessions are.
        /// </summary>
        internal static void InstallAgentSkill()
        {
            string to = WriteAgentSkill();
            if (to == null)
            {
                Tell("The skill files were not found inside the package.", true);
                return;
            }
            Tell("Agent skill written to " + to + ". Ask your agent: \"read my play notes\".");
        }

        /// <summary>The copying itself. Returns the folder written, or null if the package's files are missing.</summary>
        internal static string WriteAgentSkill()
        {
            string skill = PackageFile("Skill~/playnotes/SKILL.md");
            string script = PackageFile("Transcribe~/notes.py");
            if (skill == null || script == null) return null;
            string to = Path.Combine(ProjectFolder, ".claude", "skills", "playnotes");
            Directory.CreateDirectory(to);
            File.Copy(skill, Path.Combine(to, "SKILL.md"), true);
            File.Copy(script, Path.Combine(to, "notes.py"), true);
            File.WriteAllText(Path.Combine(to, "project.txt"), "sessions: " + Portable(Load().SessionsRoot()) + "\n");
            return to;
        }

        // (the same line for everyone on the team: the part of the path that is one person's home becomes %USERPROFILE%)
        internal static string Portable(string path)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string full = Path.GetFullPath(path);
            if (home.Length > 0 && full.StartsWith(home, StringComparison.OrdinalIgnoreCase)) return "%USERPROFILE%" + full.Substring(home.Length);
            return full;
        }

        /// <summary>Whether the build target that is selected compiles PlayNotes out altogether.</summary>
        internal static bool CompiledOut()
        {
            var target = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
            foreach (var symbol in symbols.Split(';'))
                if (symbol.Trim() == DisableSymbol) return true;
            return false;
        }
    }
}
