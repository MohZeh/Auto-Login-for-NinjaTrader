using System;
using System.Collections.Generic;
using System.Drawing;

namespace NinjaTraderAutoLogin
{
    // Geometry of the About page: three titled cards in the same style as the Setup page.
    internal sealed class AboutLayoutPlan
    {
        public readonly Dictionary<string, Rectangle> Bounds = new Dictionary<string, Rectangle>();
        public readonly Dictionary<string, string> Parents = new Dictionary<string, string>();
        public readonly int Height;
        public readonly bool Compact;
        private readonly int dpi, width;
        private readonly bool rtl;

        public AboutLayoutPlan(int width, int dpi, bool rtl, Func<string, int, int> textHeight, Func<string, int> buttonWidth, ICollection<string> shown)
        {
            this.width = width = Math.Max(1, width);
            this.dpi = dpi;
            this.rtl = rtl;
            Compact = width < Px(420);
            int inset = Px(16), field = Math.Max(1, width - 2 * inset), top = 0;

            string card = "versionCard";
            int y = Heading("versionHeading", card, inset, field);
            y = Row("versionLabel", "versionTag", card, inset, field, y, Px(28), 0, textHeight);
            y = Row("compatibilityLabel", "compatibility", card, inset, field, y + Px(2), Px(24), Px(3), textHeight);
            int check = Math.Min(field, buttonWidth("checkUpdates"));
            int action = y + Px(4);
            Place("checkUpdates", card, inset, action, check, Px(32));
            y = action + Px(32);
            foreach (string role in new[] { "updateStatus", "releaseLink" })
            {
                if (Compact)
                {
                    y = Result(role, card, inset, field, y, textHeight, shown);
                }
                else if (shown.Contains(role))
                {
                    // The result reads as the continuation of the button, on the same row.
                    int beside = inset + check + Px(10), room = Math.Max(1, field - check - Px(10));
                    int height = Math.Max(Px(32), textHeight(role, room));
                    Place(role, card, beside, action, room, height);
                    y = action + height;
                }
            }
            top = Finish(card, top, y + Px(8));

            card = "contactCard";
            y = Heading("contactHeading", card, inset, field);
            foreach (string name in new[] { "email", "github", "source" })
            {
                y = Row(name + "Label", name + "Link", card, inset, field, y, Px(22), 0, textHeight);
            }
            top = Finish(card, top, y + Px(6));

            card = "supportCard";
            y = Heading("supportHeading", card, inset, field);
            int legal = textHeight("legal", field);
            Place("legal", card, inset, y, field, legal);
            y += legal + Px(6);
            int open = Math.Min(field, buttonWidth("openDiagnostics")), copy = Math.Min(field, buttonWidth("copySummary"));
            if (open + copy + Px(8) <= field)
            {
                Place("openDiagnostics", card, inset, y, open, Px(32));
                Place("copySummary", card, inset + open + Px(8), y, copy, Px(32));
                y += Px(32);
            }
            else
            {
                Place("openDiagnostics", card, inset, y, field, Px(32));
                Place("copySummary", card, inset, y + Px(38), field, Px(32));
                y += Px(70);
            }
            y = Result("supportStatus", card, inset, field, y, textHeight, shown);
            Height = Finish(card, top, y + Px(8)) - Px(6);
        }
        private int Px(int logical)
        {
            return (int)Math.Round(logical * dpi / 96.0);
        }
        private void Place(string role, string parent, int x, int y, int rowWidth, int height)
        {
            Bounds.Add(role, new Rectangle(rtl ? width - x - rowWidth : x, y, rowWidth, height));
            Parents.Add(role, parent);
        }
        private int Heading(string role, string card, int inset, int field)
        {
            Place(role, card, inset, Px(6), field, Px(22));
            return Px(30);
        }
        private int Result(string role, string card, int inset, int field, int y, Func<string, int, int> textHeight, ICollection<string> shown)
        {
            if (!shown.Contains(role))
            {
                return y;
            }

            int height = textHeight(role, field);
            Place(role, card, inset, y + Px(6), field, height);
            return y + Px(6) + height;
        }
        // A label beside its value; narrow pages put the label above. Labels inset their
        // text, so edge lets a label used as a value line up with values that do not.
        private int Row(string label, string value, string card, int inset, int field, int y, int minimum, int edge, Func<string, int, int> textHeight)
        {
            if (Compact)
            {
                int height = Math.Max(minimum, textHeight(value, field));
                Place(label, card, inset, y, field, Px(22));
                Place(value, card, inset, y + Px(22), field, height);
                return y + Px(24) + height;
            }

            int labelWidth = Px(84), column = inset + labelWidth + Px(8), columnWidth = Math.Max(1, field - labelWidth - Px(8));
            int rowHeight = Math.Max(minimum, textHeight(value, columnWidth));
            Place(label, card, inset, y, labelWidth, rowHeight);
            Place(value, card, column - edge, y, columnWidth + edge, rowHeight);
            return y + rowHeight;
        }
        private int Finish(string card, int top, int height)
        {
            Bounds.Add(card, new Rectangle(0, top, width, height));
            Parents.Add(card, "aboutColumn");
            return top + height + Px(6);
        }
    }
}
