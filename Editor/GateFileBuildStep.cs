using System;
using System.IO;
using PlayNotes;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlayNotesEditor
{
    // After a Windows build: puts playnotes.on beside the exe where the settings say this kind of build should have it, and takes it
    // away where they say it should not. So a release built into a folder that once held a development build cannot keep the file by accident.
    //
    // The file holds the project's settings as they were at build time. Deleting it switches PlayNotes off in that build; editing it changes them.
    internal sealed class GateFileBuildStep : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;
            try
            {
                string folder = Path.GetDirectoryName(report.summary.outputPath);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
                string gate = Path.Combine(folder, PlayNotesSettings.GateFileName);
                var settings = PlayNotesProject.Load();
                bool development = (report.summary.options & BuildOptions.Development) != 0;
                bool wanted = !PlayNotesProject.CompiledOut() && (settings.gateFileInBuilds == "all" || settings.gateFileInBuilds == "development" && development);
                if (wanted)
                {
                    File.WriteAllText(gate, settings.ToJson() + "\n");
                    Debug.Log("[PlayNotes] this build has " + PlayNotesSettings.GateFileName + " beside it: PlayNotes is awake in it. Do not ship that file.");
                }
                else if (File.Exists(gate))
                {
                    File.Delete(gate);
                    Debug.Log("[PlayNotes] took " + PlayNotesSettings.GateFileName + " away from this build (Project Settings > PlayNotes > Builds).");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayNotes] could not set " + PlayNotesSettings.GateFileName + " for this build: " + e.Message);
            }
        }
    }
}
