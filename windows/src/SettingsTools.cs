// Settings: pictures and speech.
//
// Two things this app cannot do on its own. Reading text out of a picture needs
// either Tesseract on the PC or a vision model on the local server, and taking
// dictation needs a Windows speech engine for the language. Both are named here
// rather than guessed at, and both say plainly when they are not there instead of
// failing later on the page that needs them.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private readonly List<VoiceKind> _voices = new List<VoiceKind>();

        private Group Tools()
        {
            _tess.Placeholder = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
            _tess.ShowTrail = true;
            _tess.Trail = Glyph.External;
            _tess.Trailed += OnBrowse;
            _tess.Submitted += OnTess;
            _tess.Box.Leave += OnTess;
            _tess.Height = Theme.Px(34);

            _voice.Look = ButtonLook.Outline;
            _voice.ShowIcon = true;
            _voice.Icon = Glyph.Mic;
            _voice.Label = "Choose";
            _voice.Click += OnVoice;

            _voiceRow = Row("Dictation language", "Used by the microphone on the Capture page. " +
                "Windows does the listening, on this PC", _voice, Theme.Px(230));

            Group g = new Group("Pictures and speech");
            g.Add(Under("Tesseract", "Reads text out of a picture without a model. Leave it empty " +
                "to use the vision model instead", _tess));
            g.Add(_voiceRow);
            return g;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            try
            {
                d.Title = "Find tesseract.exe";
                d.Filter = "Tesseract|tesseract.exe|Programs|*.exe|Every file|*.*";
                d.CheckFileExists = true;
                string had = (Hub.Settings.TesseractPath ?? "").Trim();
                if (had.Length > 0)
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(had);
                        if (dir != null && Directory.Exists(dir)) d.InitialDirectory = dir;
                    }
                    catch (Exception) { }
                }
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                Tess(d.FileName);
            }
            catch (Exception ex)
            {
                Paths.Log("tesseract browse", ex);
                Hub.Oops("That file could not be opened");
            }
            finally
            {
                d.Dispose();
            }
        }

        private void OnTess(object sender, EventArgs e)
        {
            if (_loading) return;
            Tess(_tess.Text);
        }

        /// Keeps the path. An empty box is a real answer, meaning use the vision
        /// model, so it is saved rather than treated as a mistake. A path that is
        /// not there is refused and the box is put back, because a setting that
        /// looks right and does nothing is the worst of the three states.
        private void Tess(string path)
        {
            string want = (path ?? "").Trim().Trim('"');
            if (want == (Hub.Settings.TesseractPath ?? "").Trim()) return;
            if (want.Length > 0 && !File.Exists(want))
            {
                Hub.Oops("There is nothing at that path");
                _loading = true;
                _tess.Text = Hub.Settings.TesseractPath ?? "";
                _loading = false;
                return;
            }
            Hub.Settings.TesseractPath = want;
            Hub.SaveSettings();
            _loading = true;
            _tess.Text = want;
            _loading = false;
            Hub.Say(want.Length == 0
                ? "Pictures will go to the vision model"
                : "Pictures will be read by Tesseract");
        }

        private void OnVoice(object sender, EventArgs e)
        {
            _voices.Clear();
            _voices.AddRange(Voices.Installed());
            if (_voices.Count == 0)
            {
                Hub.Oops("Windows has no speech engine installed. Add one in Windows " +
                    "settings under Speech");
                return;
            }
            List<string> labels = new List<string>();
            for (int i = 0; i < _voices.Count; i++) labels.Add(_voices[i].Label);
            Choose("Dictation language", Say.Plural(_voices.Count, "language") + " on this PC",
                labels, Voices.Pretty(Hub.Settings.SpeechCulture), new Action<string>(Spoken));
        }

        private void Spoken(string label)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Label != label) continue;
                Hub.Settings.SpeechCulture = _voices[i].Culture;
                Hub.SaveSettings();
                Fresh();
                Hub.Say("Dictation will listen for " + label);
                return;
            }
        }
    }
}
