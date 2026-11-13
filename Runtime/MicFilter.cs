using System;

namespace PlayNotes
{
    // Which sound inputs have no one speaking into them: loopback and streaming devices, which look like microphones to Windows.
    internal static class MicFilter
    {
        static readonly string[] Known =
        {
            "Stereo Mix", "ステレオ ミキサー", "Virtual", "VB-Audio", "CABLE", "Voicemeeter", "Steam Streaming", "SYNCROOM", "NDI", "Loopback"
        };

        internal static bool IsVirtual(string name, string[] alsoSkip)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var word in Known)
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (alsoSkip == null) return false;
            foreach (var word in alsoSkip)
                if (!string.IsNullOrWhiteSpace(word) && name.IndexOf(word.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
