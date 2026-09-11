// Clips: join mode.
//
// Join mode turns row clicks into picks. The checklist button in the header (or
// the "Select for join" verb in a clip's own sheet) starts it, clicks toggle
// the row under them, and the link button beside it commits the picked clips
// into one new entry. The sources are left alone: a join that ate its inputs
// would be un-undoable, and the phone's undo only drops the row it made, which
// the desktop answers by keeping everything.

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ClipsView
    {
        private bool _joining;
        private readonly HashSet<string> _picked = new HashSet<string>();
        private readonly IconButton _join = new IconButton();
        private readonly IconButton _joinGo = new IconButton();

        private void WireJoin()
        {
            _join.Click += OnJoinToggle;
            _joinGo.Click += OnJoinGo;
            Controls.Add(_join);
            Controls.Add(_joinGo);
            RefreshJoin();
        }

        private void OnJoinToggle(object sender, EventArgs e)
        {
            _joining = !_joining;
            _picked.Clear();
            RefreshJoin();
            Lay();
            Fill();
        }

        private void OnJoinGo(object sender, EventArgs e)
        {
            JoinPicked();
        }

        private void TogglePick(ClipEntry c)
        {
            if (c == null || c.Id == null) return;
            if (!_picked.Remove(c.Id)) _picked.Add(c.Id);
            RefreshJoin();
            _list.Reload();
        }

        /// The header's two join controls follow the mode: the toggle names its
        /// way out, and the commit counts what it will merge, staying asleep
        /// until two clips are picked.
        private void RefreshJoin()
        {
            int n = _picked.Count;
            _join.Icon = _joining ? Glyph.Close : Glyph.Tasks;
            _join.Tip = _joining ? "Stop picking clips" : "Pick clips to join";
            _joinGo.Visible = _joining;
            _joinGo.Enabled = n >= 2;
            _joinGo.Tip = n >= 2
                ? "Join " + Say.Plural(n, "picked clip") + " into one"
                : "Pick at least two clips to join";
        }

        private void StartJoin(ClipEntry c)
        {
            _joining = true;
            _picked.Clear();
            if (c != null && c.Id != null) _picked.Add(c.Id);
            RefreshJoin();
            Lay();
            Fill();
        }

        private void JoinPicked()
        {
            List<ClipEntry> picked = new List<ClipEntry>();
            for (int i = 0; i < Hub.Clips.Items.Count; i++)
            {
                ClipEntry c = Hub.Clips.Items[i];
                if (c != null && _picked.Contains(c.Id)) picked.Add(c);
            }
            if (picked.Count < 2) { Hub.Oops("Pick at least two clips to join"); return; }
            ClipEntry joined = ClipJoin.Join(picked);
            Hub.Clips.Items.Add(joined);
            Hub.Clips.Save();
            Hub.RaiseClips();
            _joining = false;
            _picked.Clear();
            RefreshJoin();
            Lay();
            Fill();
            Hub.Say("Joined " + Say.Plural(picked.Count, "clip"));
        }
    }
}
