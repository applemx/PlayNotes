using UnityEngine;

namespace PlayNotes
{
    // The few words PlayNotes shows on screen, in the language of the PC.
    internal static class Words
    {
        static bool Japanese => Application.systemLanguage == SystemLanguage.Japanese;

        internal static string Hint(string key)
        {
            return Japanese ? key + " で遊びメモ（声と画面を残す）" : key + ": play notes (keeps your voice and the screen)";
        }

        internal static string Recording(string key)
        {
            if (key.Length == 0) return Japanese ? "遊びメモ　録音中" : "Play notes: recording";
            return Japanese ? "遊びメモ　録音中（" + key + " で止める）" : "Play notes: recording (" + key + " to stop)";
        }

        internal static string NoMicrophone => Japanese ? "マイクが見つかりません。写真と操作の跡だけ残します" : "No microphone found: keeping the pictures and the trail only";

        internal static string Stopped => Japanese ? "遊びメモを止めました" : "Play notes stopped";

        // (in the editor, where a first-timer's next question is "and where did it go?")
        internal static string StoppedInEditor => Japanese ? "遊びメモを止めました（Tools > PlayNotes から開けます）" : "Play notes stopped (Tools > PlayNotes opens them)";

        internal static string NothingHeard => Japanese ? "まだ声が届いていません（マイクを確かめてください）" : "No voice heard yet (check the microphone)";

        internal static string CouldNotBegin => Japanese ? "遊びメモを始められませんでした（Console を見てください）" : "Play notes could not begin (see the Console)";
    }
}
