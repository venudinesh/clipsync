// Clips: what a row looks like, and where its actions are.
//
// The phone prints a clip as a margin entry: when it arrived down the left, one
// vertical rule, then what it says. The desktop keeps that shape and adds what a
// pointer makes possible, which is three actions that appear in the row under the
// cursor instead of three buttons sitting in every row forever.
//
// A row has space for two lines, so it prints the clip's first line and then what
// follows it. The phone prints the whole formatted clip in the row and the
// untidied original underneath; one line cannot hold both, and a dim line that
// repeats the words directly above it reads as a mistake rather than as the
// original. The original is a click away in the clip's own sheet.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    /// A clip prepared for the feed. The two lines a row prints are worked out
    /// once when the list is built rather than on every paint: a resize drag
    /// reloads the list every frame, and stripping the markup off five hundred
    /// clips per frame is work nobody would ever see.
    internal sealed class ClipRow
    {
        public readonly ClipEntry Clip;
        public readonly string Head;
        public readonly string Under;

        public ClipRow(ClipEntry c)
        {
            Clip = c;
            string body = (c.ProcessedMarkdown ?? "").Trim();
            if (body.Length == 0) body = c.RawText ?? "";
            Head = Say.FirstLine(Markdown.Peek(body), 300);
            Under = Markdown.Trail(body, 300);
        }

        public bool Two { get { return Under.Length > 0; } }
    }

    internal sealed partial class ClipsView
    {
        private static readonly Glyph[] RowActions =
            new Glyph[] { Glyph.Copy, Glyph.Pin, Glyph.More };

        /// Rebuilds the visible list from the store, newest first with pinned
        /// clips held above, filtered by whatever is in the search box. A query
        /// ranks by relevance — title hits above tag hits above body hits,
        /// rare words above common ones — instead of merely filtering, because
        /// a full history is searched, not scrolled.
        private void Fill()
        {
            string q = _find.Text.Trim();
            _rows.Clear();
            List<ClipEntry> all = Hub.Clips.Items;
            for (int i = 0; i < all.Count; i++)
            {
                if (Matches(all[i], q)) _rows.Add(new ClipRow(all[i]));
            }
            if (q.Length > 0)
            {
                List<string> terms = ClipSmart.Words(q, 2);
                Dictionary<string, double> idf = ClipSmart.Idf(all, terms);
                _rows.Sort(delegate(ClipRow a, ClipRow b)
                {
                    int r = ClipSmart.Score(b.Clip, terms, idf)
                        .CompareTo(ClipSmart.Score(a.Clip, terms, idf));
                    return r != 0 ? r : Order(a, b);
                });
            }
            else
            {
                _rows.Sort(Order);
            }
            Count();
            Empty(q);
            _list.Reload();
        }

        private static bool Matches(ClipEntry c, string q)
        {
            if (q.Length == 0) return true;
            if (Has(c.Title, q)) return true;
            if (c.Tags != null)
            {
                for (int i = 0; i < c.Tags.Count; i++)
                {
                    if (Has(c.Tags[i], q)) return true;
                }
            }
            return Has(c.RawText, q) || Has(c.ProcessedMarkdown, q);
        }

        private static bool Has(string haystack, string needle)
        {
            return haystack != null &&
                   haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static int Order(ClipRow a, ClipRow b)
        {
            if (a.Clip.IsPinned != b.Clip.IsPinned) return a.Clip.IsPinned ? -1 : 1;
            return b.Clip.Timestamp.CompareTo(a.Clip.Timestamp);
        }

        /// The count over the feed, and the two figures beside it: today and
        /// pinned, the same pair the phone shows.
        private void Count()
        {
            int total = Hub.Clips.Items.Count;
            int today = 0, pinned = 0;
            DateTime now = DateTime.Now;
            for (int i = 0; i < Hub.Clips.Items.Count; i++)
            {
                ClipEntry c = Hub.Clips.Items[i];
                if (c.IsPinned) pinned++;
                if (Say.SameDay(c.Timestamp, now)) today++;
            }
            _tally.Set(total.ToString(), total == 1 ? "clip kept" : "clips kept",
                new Figure(today.ToString(), "today"),
                new Figure(pinned.ToString(), "pinned"));
        }

        /// Two empty states, because "nothing here yet" and "nothing matched"
        /// are different problems and only one of them is the app's fault.
        private void Empty(string q)
        {
            if (q.Length > 0 && Hub.Clips.Items.Count > 0)
            {
                _list.EmptyGlyph = Glyph.Search;
                _list.EmptyTitle = "No clip says that";
                _list.EmptyBody = "Nothing in your history matches what you typed.";
                return;
            }
            _list.EmptyGlyph = Glyph.Clips;
            _list.EmptyTitle = "Nothing captured yet";
            _list.EmptyBody = "Copy text anywhere on this PC and it lands here, tidied up.";
        }

        private int RowHeight(int i)
        {
            if (i < 0 || i >= _rows.Count) return Theme.Px(54);
            int h = Theme.Px(Theme.Dense ? 18 : 24) + Theme.Body.Height;
            if (_rows[i].Two) h += Theme.Tiny.Height + Theme.Px(2);
            return h;
        }

        private void DrawRow(Graphics g, Rectangle r, int i, bool hot, bool live)
        {
            if (i < 0 || i >= _rows.Count) return;
            ClipRow row = _rows[i];
            ClipEntry c = row.Clip;
            Rectangle body = RowKit.Plate(r);
            RowKit.Wash(g, body, hot, live);
            // A picked row carries the tick, the same mark the phone's feed
            // uses, so joining reads as picking in both builds.
            RowKit.Margin(g, body, Say.Stamp(c.Timestamp),
                c.IsPinned || _picked.Contains(c.Id));

            int tx = RowKit.TextLeft(body);
            int tw = RowKit.TextWidth(body, hot, RowActions.Length);
            int lh = Theme.Body.Height + Theme.Px(2);
            int ty = row.Two
                ? body.Y + (body.Height - lh - Theme.Tiny.Height - Theme.Px(2)) / 2
                : body.Y + (body.Height - lh) / 2;
            Ui.Line(g, row.Head, Theme.Body, Theme.OnSurface, new Rectangle(tx, ty, tw, lh));
            if (row.Two)
            {
                Ui.Line(g, row.Under, Theme.Tiny, Theme.Faint,
                    new Rectangle(tx, ty + lh, tw, Theme.Tiny.Height + Theme.Px(2)));
            }
            if (hot) RowKit.Zones(g, body, RowActions, c.IsPinned ? 1 : -1);
        }
    }
}
