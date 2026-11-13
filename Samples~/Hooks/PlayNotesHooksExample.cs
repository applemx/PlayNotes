using UnityEngine;
using UnityEngine.SceneManagement;

// PlayNotes works without any of this. These few lines let the game say what a picture cannot.
// Put the component on any object in your first scene and call its two methods from your own code.
public sealed class PlayNotesHooksExample : MonoBehaviour
{
    string screen = "Title";

    void OnEnable()
    {
        // Where the player is, in a few words. Asked four times a second during a session; written down when it changes.
        PlayNotes.Recorder.Where = () => SceneManager.GetActiveScene().name + " / " + screen;

        // Copy the save files with every session, so a remark can be reproduced from the state it was made in.
        // (a folder under Application.persistentDataPath, or a full path)
        PlayNotes.Recorder.AddSnapshotFolder("Saves");
    }

    // Call this wherever your game changes screen.
    public void ScreenOpened(string name)
    {
        screen = name;
    }

    // Anything worth knowing when a remark is read later: "this is too hard" means more beside "wave: 7 began".
    public void WaveBegan(int number)
    {
        PlayNotes.Recorder.Mark("wave", number + " began");
    }
}
