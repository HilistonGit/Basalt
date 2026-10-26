using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Basalt.Windows
{
    sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); AutoSize = false; Height = 30; Cursor = Cursors.Hand; ForeColor = Design.Ink; }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Design.Surface : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int height = Math.Min(22, Height - 4), top = (Height - height) / 2;
            var track = new Rectangle(1, top, 42, height); var color = Checked ? Design.Green : Design.Line;
            Design.Box(e.Graphics, track, color, color, height / 2);
            using (var brush = new SolidBrush(Enabled ? Color.White : Design.Muted)) e.Graphics.FillEllipse(brush, Checked ? 24 : 4, top + 3, height - 6, height - 6);
            Design.Text(e.Graphics, Text, Font, new Rectangle(54, 0, Math.Max(0, Width - 54), Height), Enabled ? ForeColor : Design.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1));
        }
    }
    sealed class BufferedPanel : Panel
    {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
        public void BeginRedraw() { if (IsHandleCreated) SendMessage(Handle, 0xB, IntPtr.Zero, IntPtr.Zero); }
        public void EndRedraw() { if (IsHandleCreated) { SendMessage(Handle, 0xB, new IntPtr(1), IntPtr.Zero); Invalidate(true); } }
        public BufferedPanel() { DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
    }
    sealed class BufferedTable : TableLayoutPanel
    {
        public BufferedTable() { DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint, true); }
    }
    sealed class WindowButton : Button
    {
        public string Symbol = "close"; bool hover;
        public WindowButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            var background = Parent == null ? Design.Background : Parent.BackColor;
            e.Graphics.Clear(background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover) Design.Box(e.Graphics, new Rectangle(1, 1, Width - 3, Height - 3), Symbol == "close" ? Color.FromArgb(178, 56, 73) : Design.Surface, background, 6);
            int size = Math.Max(16, (int)Math.Round(20 * e.Graphics.DpiX / 96));
            Design.Icon(e.Graphics, Symbol, new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), Design.Ink);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8));
        }
    }
    sealed class DarkComboBox : ComboBox
    {
        [StructLayout(LayoutKind.Sequential)] struct PaintData
        {
            public IntPtr Hdc; public int Erase; public int Left, Top, Right, Bottom; public int Restore, IncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
        }
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr window, out PaintData paint);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr window, ref PaintData paint);
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x14) { message.Result = new IntPtr(1); return; }
            if (message.Msg != 0xF || Width < 24 || Height < 8) { base.WndProc(ref message); return; }
            // Paint the native client and decoration to the same back buffer.
            // Never paint a second visible frame with CreateGraphics after WM_PAINT.
            PaintData paint; IntPtr target = BeginPaint(Handle, out paint);
            try
            {
                using (var bitmap = new Bitmap(Width, Height)) using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(BackColor); IntPtr dc = graphics.GetHdc();
                    try { var print = Message.Create(Handle, 0x318, dc, new IntPtr(4)); base.WndProc(ref print); }
                    finally { graphics.ReleaseHdc(dc); }
                    int button = SystemInformation.VerticalScrollBarWidth + 3;
                    using (var brush = new SolidBrush(Design.Surface))
                    {
                        graphics.FillRectangle(brush, 0, 0, Width, 3); graphics.FillRectangle(brush, 0, Height - 3, Width, 3);
                        graphics.FillRectangle(brush, 0, 0, 3, Height); graphics.FillRectangle(brush, Width - button, 0, button, Height);
                    }
                    using (var pen = new Pen(Focused ? Design.Green : Design.Line)) graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                    Design.Icon(graphics, "chevron-down", new Rectangle(Width - button + (button - 14) / 2, (Height - 14) / 2, 14, 14), Design.Muted);
                    if (target != IntPtr.Zero) using (var output = Graphics.FromHdc(target)) output.DrawImageUnscaled(bitmap, 0, 0);
                }
            }
            finally { EndPaint(Handle, ref paint); }
            message.Result = IntPtr.Zero;
        }
    }
}
