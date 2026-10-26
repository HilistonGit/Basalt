using Basalt.Core;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Basalt.Windows
{
    public sealed partial class MainForm
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
        void SetupChrome()
        {
            FormBorderStyle = FormBorderStyle.None;
            var titlebar = new BufferedPanel { Dock = DockStyle.Top, Height = 42, BackColor = Design.Background, Padding = new Padding(18, 0, 8, 0) };
            var title = Caption("BASALT", 9, Design.Muted); title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleLeft; titlebar.Controls.Add(title);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 138, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
            var minimize = new WindowButton { Symbol = "minimize" }; minimize.Click += delegate { WindowState = FormWindowState.Minimized; }; minimize.AccessibleName = L10n.T("Свернуть");
            var maximize = new WindowButton { Symbol = "maximize" }; maximize.Click += delegate { ToggleMaximize(); }; Resize += delegate { maximize.Symbol = WindowState == FormWindowState.Maximized ? "restore" : "maximize"; maximize.Invalidate(); }; maximize.AccessibleName = L10n.T("Развернуть / восстановить");
            var close = new WindowButton { Symbol = "close" }; close.Click += delegate { Close(); }; close.AccessibleName = L10n.T("Закрыть");
            foreach (var button in new[] { minimize, maximize, close }) { button.Size = new Size(42, 30); button.Margin = new Padding(1, 0, 1, 0); actions.Controls.Add(button); }
            titlebar.Controls.Add(actions); Controls.Add(titlebar);
            MouseEventHandler drag = delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left && e.Clicks == 1) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
            title.MouseDown += drag; titlebar.MouseDown += drag;
            title.DoubleClick += delegate { ToggleMaximize(); }; titlebar.DoubleClick += delegate { ToggleMaximize(); };
            Resize += delegate { UpdateWindowRegion(); }; UpdateWindowRegion();
        }
        void ToggleMaximize() { MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        void UpdateWindowRegion()
        {
            var old = Region;
            if (WindowState == FormWindowState.Maximized || Width < 24 || Height < 24) Region = null;
            else using (var path = Design.Round(new Rectangle(0, 0, Width, Height), 12)) Region = new Region(path);
            if (old != null) old.Dispose();
        }
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg != 0x84 || WindowState != FormWindowState.Normal) return;
            long position = message.LParam.ToInt64(); var point = PointToClient(new Point((short)(position & 65535), (short)((position >> 16) & 65535)));
            const int edge = 7; bool left = point.X < edge, right = point.X >= Width - edge, top = point.Y < edge, bottom = point.Y >= Height - edge;
            if (top && left) message.Result = new IntPtr(13); else if (top && right) message.Result = new IntPtr(14);
            else if (bottom && left) message.Result = new IntPtr(16); else if (bottom && right) message.Result = new IntPtr(17);
            else if (left) message.Result = new IntPtr(10); else if (right) message.Result = new IntPtr(11);
            else if (top) message.Result = new IntPtr(12); else if (bottom) message.Result = new IntPtr(15);
        }
    }
}
