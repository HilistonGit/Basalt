using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    static class Design
    {
        public static readonly Color Background = Color.FromArgb(24, 26, 31), Surface = Color.FromArgb(33, 36, 43), Ink = Color.FromArgb(235, 238, 244), Muted = Color.FromArgb(160, 169, 185), Line = Color.FromArgb(55, 61, 72), Green = Color.FromArgb(130, 111, 244), Mint = Color.FromArgb(49, 43, 76), Red = Color.FromArgb(244, 121, 131);
        public static Font Font(float size, bool bold) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); }
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath(); int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90); path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        public static void Box(Graphics g, Rectangle r, Color fill, Color stroke, int radius)
        { using (var path = Round(r, radius)) using (var brush = new SolidBrush(fill)) using (var pen = new Pen(stroke)) { g.FillPath(brush, path); g.DrawPath(pen, path); } }
        public static void Text(Graphics g, string text, Font font, Rectangle r, Color color, TextFormatFlags flags)
        { TextRenderer.DrawText(g, text, font, r, color, flags | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis); }
        public static void Icon(Graphics g, string icon, Rectangle r, Color color)
        {
            var old = g.Save(); g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 24f, r.Height / 24f); g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (icon == "close") { g.DrawLine(pen, 5, 5, 19, 19); g.DrawLine(pen, 19, 5, 5, 19); }
                else if (icon == "minimize") g.DrawLine(pen, 4, 12, 20, 12);
                else if (icon == "maximize") g.DrawRectangle(pen, 5, 5, 14, 14);
                else if (icon == "restore") { g.DrawLines(pen, new[] { new Point(8, 4), new Point(20, 4), new Point(20, 16) }); g.DrawRectangle(pen, 4, 8, 12, 12); }
                else if (icon == "chevron-down") g.DrawLines(pen, new[] { new Point(6, 9), new Point(12, 15), new Point(18, 9) });
                else if (icon == "chevron-right") g.DrawLines(pen, new[] { new Point(9, 6), new Point(15, 12), new Point(9, 18) });
                else if (icon == "more") { using (var brush = new SolidBrush(color)) for (int x = 5; x <= 19; x += 7) g.FillEllipse(brush, x - 1.5f, 10.5f, 3, 3); }
                else if (icon == "power") { g.DrawArc(pen, 4, 4, 16, 16, -48, 276); g.DrawLine(pen, 12, 2, 12, 11); }
                else if (icon == "plus") { g.DrawLine(pen, 5, 12, 19, 12); g.DrawLine(pen, 12, 5, 12, 19); }
                else if (icon == "search") { g.DrawEllipse(pen, 3, 3, 12, 12); g.DrawLine(pen, 14, 14, 21, 21); }
                else if (icon == "refresh") { g.DrawArc(pen, 4, 4, 16, 16, 35, 285); g.DrawLines(pen, new[] { new Point(20, 3), new Point(20, 8), new Point(15, 8) }); }
                else if (icon == "star")
                {
                    var points = new PointF[10]; for (int i = 0; i < 10; i++) { double angle = -Math.PI / 2 + i * Math.PI / 5; float rad = i % 2 == 0 ? 9 : 4; points[i] = new PointF(12 + (float)Math.Cos(angle) * rad, 12 + (float)Math.Sin(angle) * rad); } g.DrawPolygon(pen, points);
                }
                else if (icon == "route") { g.DrawBezier(pen, 6, 18, 6, 8, 18, 16, 18, 6); g.DrawEllipse(pen, 3, 15, 6, 6); g.DrawEllipse(pen, 15, 3, 6, 6); }
                else if (icon == "settings") { g.DrawLine(pen, 4, 7, 20, 7); g.DrawLine(pen, 4, 17, 20, 17); using (var brush = new SolidBrush(color)) { g.FillEllipse(brush, 7, 4, 6, 6); g.FillEllipse(brush, 13, 14, 6, 6); } }

                else if (icon == "log") { using (var outline = Round(new Rectangle(4, 3, 16, 18), 3)) g.DrawPath(pen, outline); g.DrawLine(pen, 8, 8, 16, 8); g.DrawLine(pen, 8, 12, 16, 12); g.DrawLine(pen, 8, 16, 13, 16); }
                else if (icon == "folder")
                {
                    using (var path = new GraphicsPath()) { path.AddLine(4, 4, 10, 4); path.AddLine(10, 4, 13, 7); path.AddLine(13, 7, 20, 7); path.AddBezier(20, 7, 21, 7, 22, 8, 22, 9); path.AddLine(22, 9, 22, 18); path.AddBezier(22, 18, 22, 20, 21, 21, 19, 21); path.AddLine(19, 21, 5, 21); path.AddBezier(5, 21, 3, 21, 2, 20, 2, 18); path.AddLine(2, 18, 2, 6); path.AddBezier(2, 6, 2, 5, 3, 4, 4, 4); path.CloseFigure(); g.DrawPath(pen, path); }
                }
                else if (icon == "check") g.DrawLines(pen, new[] { new Point(5, 12), new Point(10, 17), new Point(20, 6) });
                else if (icon == "bolt") g.DrawLines(pen, new[] { new Point(14, 2), new Point(5, 14), new Point(12, 14), new Point(10, 22), new Point(20, 10), new Point(13, 10), new Point(14, 2) });
                else { g.DrawEllipse(pen, 3, 3, 18, 18); g.DrawEllipse(pen, 8, 3, 8, 18); g.DrawLine(pen, 3, 12, 21, 12); }
            }
            g.Restore(old);
        }
    }
    sealed class Card : Panel
    {
        public Card() { DoubleBuffered = true; BackColor = Design.Surface; Padding = new Padding(18); }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e); if (Width < 28 || Height < 28) return;
            var old = Region; using (var path = Design.Round(new Rectangle(0, 0, Width, Height), 14)) Region = new Region(path); if (old != null) old.Dispose();
        }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); if (Width < 28 || Height < 28) return; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = Design.Round(new Rectangle(0, 0, Width - 1, Height - 1), 14)) using (var pen = new Pen(Design.Line)) e.Graphics.DrawPath(pen, path); }
    }
    sealed class ActionButton : Button
    {
        public string Glyph = ""; public bool Primary; public bool Selected; public bool Navigation;
        bool hover, pressed;
        float glow;
        readonly Timer motion = new Timer { Interval = 20 };
        public ActionButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Font = Design.Font(10, true); Cursor = Cursors.Hand; Height = 42; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); motion.Tick -= Animate; motion.Tick += Animate; }
        void Animate(object sender, EventArgs e) { float target = hover ? 1 : 0; glow += (target - glow) * .3f; if (Math.Abs(target - glow) < .02f) { glow = target; motion.Stop(); } Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; motion.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; motion.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void Dispose(bool disposing) { if (disposing) motion.Dispose(); base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Design.Background : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? Design.Green : Selected ? Design.Mint : Navigation ? Parent.BackColor : Design.Surface;
            int lift = (int)(glow * 16) - (pressed ? 24 : 0);
            fill = Color.FromArgb(Math.Max(0, Math.Min(255, fill.R + lift)), Math.Max(0, Math.Min(255, fill.G + lift)), Math.Max(0, Math.Min(255, fill.B + lift)));
            Color ink = !Enabled ? Design.Muted : Primary ? Color.White : Selected ? Design.Green : Design.Ink;
            Design.Box(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), fill, Navigation || Primary || Selected ? fill : Design.Line, 11);
            int icon = (int)(20 * e.Graphics.DpiX / 96), gap = 8, inset = 14;
            int textWidth = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            int total = Math.Min(Width - inset * 2, textWidth + (Glyph == "" ? 0 : icon + gap));
            int left = Navigation ? inset : Math.Max(inset, (Width - total) / 2);
            if (Glyph != "") { Design.Icon(e.Graphics, Glyph, new Rectangle(left, (Height - icon) / 2, icon, icon), ink); left += icon + gap; }
            Design.Text(e.Graphics, Text, Font, new Rectangle(left, 0, Math.Max(0, Width - left - inset), Height), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.Left);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8));
        }
    }
    sealed class PowerButton : Button
    {
        public bool Connected;
        public PowerButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; AccessibleName = L10n.T("Подключить или отключить"); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Design.Mint)) e.Graphics.FillEllipse(brush, 1, 1, Width - 2, Height - 2);
            using (var brush = new SolidBrush(Enabled ? Design.Green : Color.FromArgb(165, 186, 179))) e.Graphics.FillEllipse(brush, 12, 12, Width - 24, Height - 24);
            Design.Icon(e.Graphics, "power", new Rectangle(Width / 2 - 22, Height / 2 - 22, 44, 44), Color.White);
            if (Connected) using (var pen = new Pen(Design.Green, 2)) e.Graphics.DrawEllipse(pen, 3, 3, Width - 6, Height - 6);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(30, 30, Width - 60, Height - 60));
        }
    }
}
