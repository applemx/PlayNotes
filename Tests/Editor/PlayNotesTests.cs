using System;
using NUnit.Framework;

namespace PlayNotes.Tests
{
    // The parts of PlayNotes that are plain reckoning, and so can be tested without a microphone or a window.
    public sealed class PlayNotesTests
    {
        [Test]
        public void A_trail_line_is_seconds_kind_words_with_tabs_between()
        {
            Assert.AreEqual("12.50\tplace\tWorkbench / order 12", Trail.Format(12.5, "place", "Workbench / order 12"));
        }

        [Test]
        public void A_tab_or_a_line_break_in_the_words_cannot_break_the_line()
        {
            string line = Trail.Format(1, "click", "press \"Two\nlines\"\tand a tab");
            Assert.AreEqual(3, line.Split('\t').Length);
            Assert.IsFalse(line.Contains("\n"));
        }

        [Test]
        public void A_kind_is_always_one_word()
        {
            Assert.AreEqual("my_kind", Trail.Kind("my kind"));
            Assert.AreEqual("note", Trail.Kind(""));
            Assert.AreEqual("note", Trail.Kind(null));
        }

        [TestCase("f8", "F8")]
        [TestCase("F12", "F12")]
        [TestCase("esc", "Escape")]
        [TestCase("Escape", "Escape")]
        [TestCase("return", "Enter")]
        [TestCase("Enter", "Enter")]
        [TestCase("e", "E")]
        [TestCase("1", "1")]
        [TestCase("Alpha1", "1")]
        [TestCase("Digit7", "7")]
        [TestCase(" tab ", "Tab")]
        [TestCase("backquote", "Backquote")]
        [TestCase("", "")]
        public void A_key_has_one_spelling_however_it_was_typed(string typed, string expected)
        {
            Assert.AreEqual(expected, WatchedKey.Normalize(typed));
        }

        [Test]
        public void Each_input_system_gets_its_own_name_for_a_key()
        {
            Assert.AreEqual("Digit1", WatchedKey.NewName("1"));
            Assert.AreEqual("Alpha1", WatchedKey.OldName("1"));
            Assert.AreEqual("Return", WatchedKey.OldName("Enter"));
            Assert.AreEqual("Enter", WatchedKey.NewName("Enter"));
            Assert.AreEqual("F8", WatchedKey.NewName("F8"));
        }

        [Test]
        public void The_default_keys_are_known_to_the_input_that_is_on()
        {
            var settings = new PlayNotesSettings();
            Assert.IsTrue(new WatchedKey(settings.toggleKey).Valid, settings.toggleKey);
            foreach (var name in settings.watchedKeys) Assert.IsTrue(new WatchedKey(name).Valid, name);
            Assert.IsFalse(new WatchedKey("NoSuchKey").Valid);
            Assert.IsFalse(new WatchedKey("").Valid);
        }

        [Test]
        public void Loopback_devices_are_not_microphones()
        {
            Assert.IsTrue(MicFilter.IsVirtual("Stereo Mix (Realtek Audio)", null));
            Assert.IsTrue(MicFilter.IsVirtual("CABLE Output (VB-Audio Virtual", null));
            Assert.IsFalse(MicFilter.IsVirtual("Microphone (USB Audio Device)", null));
            Assert.IsTrue(MicFilter.IsVirtual("Microphone (USB Audio Device)", new[] { "usb audio" }));
            Assert.IsFalse(MicFilter.IsVirtual("Microphone (USB Audio Device)", new[] { "", "  " }));
        }

        [Test]
        public void A_label_becomes_one_short_plain_line()
        {
            Assert.AreEqual("Start game", ClickLabels.Plain("<b>Start</b>\ngame"));
            Assert.AreEqual("say  hi", ClickLabels.Plain("say \"hi\""));
            Assert.AreEqual(25, ClickLabels.Plain(new string('x', 80)).Length);
            Assert.AreEqual("", ClickLabels.Plain(null));
        }

        [Test]
        public void Settings_left_out_of_the_file_keep_their_defaults()
        {
            var s = PlayNotesSettings.FromJson("{ \"toggleKey\": \"F9\", \"language\": \" ja \" }");
            Assert.AreEqual("F9", s.toggleKey);
            Assert.AreEqual("ja", s.language);
            Assert.AreEqual(1.5f, s.shotSecondsSpeaking);
            Assert.AreEqual("development", s.gateFileInBuilds);
            Assert.IsTrue(s.showHint);
            Assert.Greater(s.watchedKeys.Length, 0);
        }

        [Test]
        public void An_empty_gate_file_or_a_broken_one_means_the_defaults()
        {
            Assert.AreEqual("F8", PlayNotesSettings.FromJson("").toggleKey);
            Assert.AreEqual("F8", PlayNotesSettings.FromJson("   \n").toggleKey);
            Assert.AreEqual("F8", PlayNotesSettings.FromJson("this is not json").toggleKey);
        }

        [Test]
        public void Settings_survive_being_written_and_read()
        {
            var a = new PlayNotesSettings { toggleKey = "F9", words = "Mirelle Okuno-do", snapshotFolders = new[] { "Saves" }, gateFileInBuilds = "never", showHint = false };
            var b = PlayNotesSettings.FromJson(a.ToJson());
            Assert.AreEqual("F9", b.toggleKey);
            Assert.AreEqual("Mirelle Okuno-do", b.words);
            Assert.AreEqual(new[] { "Saves" }, b.snapshotFolders);
            Assert.AreEqual("never", b.gateFileInBuilds);
            Assert.IsFalse(b.showHint);
        }

        [Test]
        public void Settings_out_of_range_are_brought_back()
        {
            var s = PlayNotesSettings.FromJson("{ \"shotSecondsSpeaking\": 0, \"shotSecondsQuiet\": 0.1, \"gateFileInBuilds\": \"sometimes\" }");
            Assert.AreEqual(.5f, s.shotSecondsSpeaking);
            Assert.GreaterOrEqual(s.shotSecondsQuiet, s.shotSecondsSpeaking);
            Assert.AreEqual("development", s.gateFileInBuilds);
        }

        [Test]
        public void Quiet_is_not_a_voice()
        {
            var d = new VoiceDetector();
            for (int i = 0; i < 40; i++) d.Feed(Tone(0f), 8000);
            Assert.IsFalse(d.Voice);
        }

        [Test]
        public void Steady_room_noise_is_not_a_voice()
        {
            var d = new VoiceDetector();
            for (int i = 0; i < 80; i++) d.Feed(Tone(.002f), 8000);
            Assert.IsFalse(d.Voice);
        }

        [Test]
        public void A_voice_over_the_quiet_is_heard_after_half_a_second_and_dropped_when_it_ends()
        {
            var d = new VoiceDetector();
            for (int i = 0; i < 20; i++) d.Feed(Tone(.0005f), 8000);
            d.Feed(Tone(.02f), 8000);
            Assert.IsFalse(d.Voice, "one block is a knock on the desk, not a voice");
            d.Feed(Tone(.02f), 8000);
            Assert.IsTrue(d.Voice);
            Assert.Greater(d.Loudest, .019f);
            d.Feed(Tone(.0005f), 8000);
            Assert.IsFalse(d.Voice);
        }

        [Test]
        public void A_quiet_speaker_is_still_heard()
        {
            // (.004 of full scale: a soft voice on a desk microphone, as measured)
            var d = new VoiceDetector();
            for (int i = 0; i < 20; i++) d.Feed(Tone(.0002f), 8000);
            d.Feed(Tone(.004f), 8000);
            d.Feed(Tone(.004f), 8000);
            Assert.IsTrue(d.Voice);
        }

        [Test]
        public void Nothing_in_the_package_opens_a_dialog_that_waits_for_a_click()
        {
            // An editor is as often driven by a script or an AI agent as by a hand, and a dialog with a button leaves those waiting for ever.
            // (the names are put together here so that this file does not find itself)
            string[] waiting = { "Display" + "Dialog", "Show" + "Modal", "Display" + "CancelableProgressBar" };
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Recorder).Assembly);
            Assert.IsNotNull(package);
            foreach (var file in System.IO.Directory.GetFiles(package.resolvedPath, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string text = System.IO.File.ReadAllText(file);
                foreach (var name in waiting) Assert.IsFalse(text.Contains(name), System.IO.Path.GetFileName(file) + " uses " + name);
            }
        }

        // a quarter of a second of a 220 Hz tone, 16-bit mono at 16 kHz, with this amplitude of full scale
        static byte[] Tone(float amplitude)
        {
            var pcm = new byte[8000];
            for (int i = 0; i < 4000; i++)
            {
                short s = (short)Math.Round(Math.Sin(i * 2 * Math.PI * 220 / 16000) * amplitude * Math.Sqrt(2) * 32767);
                pcm[i * 2] = (byte)(s & 0xff);
                pcm[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            return pcm;
        }
    }
}
