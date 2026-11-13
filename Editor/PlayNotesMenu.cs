using PlayNotes;
using UnityEditor;
using UnityEngine;

namespace PlayNotesEditor
{
    // Tools > PlayNotes: the things one wants within reach after a session.
    internal static class PlayNotesMenu
    {
        const string Root = "Tools/PlayNotes/";

        [MenuItem(Root + "Start or Stop Recording", false, 10)]
        static void Toggle() => Recorder.Toggle();

        [MenuItem(Root + "Start or Stop Recording", true)]
        static bool CanToggle() => Application.isPlaying && Recorder.IsAvailable;

        [MenuItem(Root + "Open Latest Session", false, 30)]
        static void OpenLatest() => PlayNotesProject.OpenFolder(PlayNotesProject.LatestSession());

        [MenuItem(Root + "Write New Sessions Out as Words", false, 31)]
        static void Transcribe() => PlayNotesProject.Transcribe(PlayNotesProject.Load().SessionsRoot());

        [MenuItem(Root + "Install Agent Skill (.claude)", false, 50)]
        static void InstallSkill() => PlayNotesProject.InstallAgentSkill();

        [MenuItem(Root + "Settings...", false, 70)]
        static void OpenSettings() => SettingsService.OpenProjectSettings("Project/PlayNotes");
    }
}
