using System;
using System.Drawing;
using System.Windows.Forms;
namespace Basalt.Windows
{
    sealed class ActivityStrip : Control
    {
        readonly Timer timer = new Timer { Interval = 25 };
        float phase;
        public bool Active { get { return timer.Enabled; } set { timer.Enabled = value; if (!value) phase = 0; Invalidate(); } }
        public ActivityStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            timer.Tick += delegate { phase = (phase + .018f) % 1; Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Design.Surface);
            if (!Active) return;
            using (var b = new SolidBrush(Design.Mint)) e.Graphics.FillRectangle(b, ClientRectangle);
            using (var b = new SolidBrush(Design.Green)) e.Graphics.FillRectangle(b, (phase * 1.4f - .4f) * Width, 0, Width * .4f, Height);
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
