using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    internal sealed class SubscriptionHeaderLayout
    {
        public readonly Rectangle Title, Metadata, Quota, Expiry, Chevron, Refresh, Ping, More, Delete;
        public SubscriptionHeaderLayout(Rectangle bounds, float scale)
        {
            int padding = (int)Math.Round(16 * scale), button = (int)Math.Round(32 * scale), gap = (int)Math.Round(12 * scale);
            int right = bounds.Right - padding;
            Delete = new Rectangle(right - button, bounds.Y + (int)(14 * scale), button, (int)(32 * scale));
            More = new Rectangle(right - button * 2, bounds.Y + (int)(14 * scale), button, (int)(32 * scale));
            Ping = new Rectangle(More.Left - button, More.Top, button, More.Height); Refresh = new Rectangle(Ping.Left - button, More.Top, button, More.Height);
            Chevron = new Rectangle(bounds.X + padding, bounds.Y + (int)(22 * scale), (int)(14 * scale), (int)(14 * scale));
            int titleX = Chevron.Right + (int)(8 * scale);
            Title = new Rectangle(titleX, bounds.Y + (int)(17 * scale), Math.Max(0, Refresh.Left - gap - titleX), (int)(28 * scale));
            Metadata = new Rectangle(titleX, bounds.Y + (int)(46 * scale), Math.Max(0, right - titleX), (int)(22 * scale));
            int content = Math.Max(0, bounds.Width - padding * 2), quotaWidth = Math.Max(0, (content - gap) * 55 / 100);
            Quota = new Rectangle(bounds.X + padding, bounds.Y + (int)(78 * scale), quotaWidth, (int)(22 * scale));
            Expiry = new Rectangle(Quota.Right + gap, Quota.Top, Math.Max(0, right - Quota.Right - gap), Quota.Height);
        }
    }
    sealed class SubscriptionRow { public Subscription Subscription; public Profile Profile; public string GroupId; public string Title; public int Count; public override string ToString() { return Profile == null ? Title : Profile.Name; } }
    sealed class SubscriptionBrowser : ListBox
    {
        readonly HashSet<string> collapsed = new HashSet<string>();
        public Profile SelectedProfile { get { var row = SelectedItem as SubscriptionRow; return row == null ? null : row.Profile; } }
        float scale = 1;
        readonly ToolTip hints = new ToolTip();
        Rectangle hoverAction;
        public string ActiveId;
        public IDictionary<string, int> Latency;
        public event Action<Profile> SelectProfile; public event Action<Profile> EditProfile;
        public event Action<Subscription> RefreshSubscription, TestSubscription, EditSubscription, DeleteSubscription;
        public event Action Rebuild;
        public SubscriptionBrowser() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); DrawMode = DrawMode.OwnerDrawVariable; IntegralHeight = false; BorderStyle = BorderStyle.None; BackColor = Design.Background; Font = Design.Font(10, false); AccessibleName = L10n.T("Подписки и серверы"); }
        public void Populate(Settings settings, List<Profile> profiles)
        {
            int top = TopIndex; var selected = SelectedItem as SubscriptionRow;
            string selection = selected == null ? null : selected.Profile == null ? selected.GroupId : selected.Profile.Id;
            BeginUpdate(); Items.Clear();
            foreach (var subscription in settings.Subscriptions) AddGroup(subscription, subscription.Id, subscription.Name, profiles);
            AddGroup(null, "manual", L10n.T("Добавлено вручную"), profiles);
            ActiveId = settings.SelectedId;
            for (int i = 0; i < Items.Count; i++) { var row = (SubscriptionRow)Items[i]; if ((row.Profile == null ? row.GroupId : row.Profile.Id) == selection) SelectedIndex = i; }
            if (Items.Count > 0) TopIndex = Math.Min(top, Items.Count - 1);
            EndUpdate(); Invalidate();
        }
        void AddGroup(Subscription subscription, string id, string title, List<Profile> profiles)
        {
            var children = profiles.FindAll(delegate(Profile p) { return subscription == null ? p.SubscriptionId == null : p.SubscriptionId == subscription.Id; });
            if (children.Count == 0 && subscription == null) return;
            Items.Add(new SubscriptionRow { Subscription = subscription, GroupId = id, Title = title, Count = children.Count });
            if (!collapsed.Contains(id)) foreach (var p in children) Items.Add(new SubscriptionRow { Profile = p, GroupId = id });
        }
        protected override void OnMeasureItem(MeasureItemEventArgs e) { scale = e.Graphics.DpiX / 96f; e.ItemHeight = (int)Math.Round((e.Index >= 0 && e.Index < Items.Count && ((SubscriptionRow)Items[e.Index]).Profile != null ? 66 : 114) * scale); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, e.ClipRectangle);
            if (Items.Count == 0) return;
            for (int i = Math.Max(0, TopIndex); i < Items.Count; i++)
            {
                var bounds = GetItemRectangle(i); if (bounds.Top >= ClientSize.Height) break;
                if (bounds.IntersectsWith(e.ClipRectangle)) DrawRow(new DrawItemEventArgs(e.Graphics, Font, bounds, i, i == SelectedIndex ? DrawItemState.Selected : DrawItemState.Default));
            }
        }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count || e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;
            using (var bitmap = new Bitmap(e.Bounds.Width, e.Bounds.Height))
            {
                bitmap.SetResolution(e.Graphics.DpiX, e.Graphics.DpiY);
                using (var graphics = Graphics.FromImage(bitmap)) DrawRow(new DrawItemEventArgs(graphics, e.Font, new Rectangle(Point.Empty, e.Bounds.Size), e.Index, e.State));
                e.Graphics.DrawImageUnscaled(bitmap, e.Bounds.Location);
            }
        }
        int S(int value) { return (int)Math.Round(value * scale); }
        static string Bytes(double value) { return value >= 1073741824 ? (value / 1073741824).ToString("0.#") + L10n.T(" ГБ") : (value / 1048576).ToString("0.#") + L10n.T(" МБ"); }
        void DrawRow(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return; var row = (SubscriptionRow)Items[e.Index]; var r = e.Bounds; scale = e.Graphics.DpiX / 96f;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, r);
            if (row.Profile == null)
            {
                scale = e.Graphics.DpiX / 96f;
                var layout = new SubscriptionHeaderLayout(r, scale);
                Design.Box(e.Graphics, new Rectangle(r.X, r.Y + (int)(5 * scale), Math.Max(16, r.Width - 2), r.Height - (int)(7 * scale)), Design.Surface, Design.Line, (int)(10 * scale));
                Design.Icon(e.Graphics, collapsed.Contains(row.GroupId) ? "chevron-right" : "chevron-down", layout.Chevron, Design.Muted);
                using (var f = Design.Font(10, true)) Design.Text(e.Graphics, row.Title, f, layout.Title, Design.Ink, TextFormatFlags.VerticalCenter);
                if (row.Subscription != null)
                {
                    DrawAction(e.Graphics, "refresh", layout.Refresh); DrawAction(e.Graphics, "bolt", layout.Ping); DrawAction(e.Graphics, "settings", layout.More); DrawAction(e.Graphics, "close", layout.Delete);
                }
                var subscription = row.Subscription; DateTime date;
                string updated = subscription != null && DateTime.TryParse(subscription.Updated, out date) ? " · " + date.ToLocalTime().ToString("dd.MM HH:mm") : "";
                string automatic = subscription == null ? "" : subscription.UpdateFailures > 0 ? L10n.T(" · ошибка обновления") : subscription.AutoUpdate ? L10n.T(" · авто ") + SubscriptionSchedule.Hours(subscription) + L10n.T(" ч") : L10n.T(" · вручную");
                using (var f = Design.Font(8.5f, false)) Design.Text(e.Graphics, row.Count + L10n.T(" серверов") + updated + automatic, f, layout.Metadata, Design.Muted, TextFormatFlags.VerticalCenter);
                bool known = subscription != null && subscription.UploadBytes.HasValue && subscription.DownloadBytes.HasValue;
                double used = known ? (double)subscription.UploadBytes.Value + subscription.DownloadBytes.Value : 0;
                string quota = known ? Bytes(used) + " / " + (subscription.TotalBytes.GetValueOrDefault() > 0 ? Bytes(subscription.TotalBytes.Value) : subscription.TotalBytes.HasValue ? "∞" : "?") : L10n.T("Трафик не сообщён");
                if (layout.Quota.Width > 12)
                {
                    Design.Box(e.Graphics, layout.Quota, Design.Background, Design.Line, (int)(5 * scale));
                    if (known && subscription.TotalBytes.GetValueOrDefault() > 0)
                    {
                        int fill = (int)((layout.Quota.Width - 2) * Math.Min(1, used / subscription.TotalBytes.Value));
                        if (fill > 0) { var oldClip = e.Graphics.Save(); using (var path = Design.Round(layout.Quota, (int)(5 * scale))) { e.Graphics.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect); using (var brush = new SolidBrush(Design.Mint)) e.Graphics.FillRectangle(brush, layout.Quota.X + 1, layout.Quota.Y + 1, fill, layout.Quota.Height - 2); } e.Graphics.Restore(oldClip); }
                    }
                }
                using (var f = Design.Font(8.5f, false))
                {
                    Design.Text(e.Graphics, quota, f, Rectangle.Inflate(layout.Quota, -4, 0), Design.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    string expiry = subscription != null && subscription.ExpireUnix.GetValueOrDefault() > 0 ? L10n.T("До ") + new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(subscription.ExpireUnix.Value).ToLocalTime().ToString("dd.MM.yyyy") : L10n.T("Срок не сообщён");
                    Design.Text(e.Graphics, expiry, f, layout.Expiry, Design.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }
            else
            {
                var p = row.Profile; bool active = p.Id == ActiveId;
                using (var brush = new SolidBrush(active ? Design.Mint : Design.Surface)) e.Graphics.FillRectangle(brush, r.X, r.Y, r.Width - 2, r.Height - 1);
                if (active) using (var brush = new SolidBrush(Design.Green)) e.Graphics.FillRectangle(brush, r.X, r.Y, 3, r.Height - 1);
                ServerCountry.Draw(e.Graphics, new Rectangle(r.X + S(16), r.Y + S(22), S(22), S(22)), p.Name);
                using (var f = Design.Font(10, true)) Design.Text(e.Graphics, p.Name, f, new Rectangle(r.X + S(48), r.Y + S(10), Math.Max(0, r.Width - S(170)), S(23)), Design.Ink, TextFormatFlags.Left);
                using (var f = Design.Font(8, false)) Design.Text(e.Graphics, p.Protocol.ToUpperInvariant() + " / " + p.Option("type", "tcp").ToUpperInvariant() + "  ·  " + p.Engine, f, new Rectangle(r.X + S(48), r.Y + S(37), Math.Max(0, r.Width - S(170)), S(20)), Design.Muted, TextFormatFlags.Left);
                using(var pen=new Pen(Design.Muted,1.6f)) { float x=r.Right-S(20), y=r.Y+r.Height/2; e.Graphics.DrawLines(pen,new[]{new PointF(x-S(3),y-S(5)),new PointF(x+S(2),y),new PointF(x-S(3),y+S(5))}); } int ms; string status = Latency != null && Latency.TryGetValue(p.Id, out ms) ? ms < 0 ? L10n.T("Нет ответа") : ms + L10n.T(" мс") : "—";
                using (var f = Design.Font(8, false)) Design.Text(e.Graphics, status, f, new Rectangle(r.Right - S(122), r.Y + S(23), S(75), S(20)), status.EndsWith(L10n.T("мс")) ? Design.Green : Design.Muted, TextFormatFlags.Right);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }
            base.OnMouseDown(e); int index = IndexFromPoint(e.Location); if (index < 0 || index >= Items.Count) return;
            var layout = new SubscriptionHeaderLayout(GetItemRectangle(index), scale);
            var clicked=(SubscriptionRow)Items[index]; if(clicked.Profile!=null && e.X>=GetItemRectangle(index).Right-(int)(40*scale)) { if(EditProfile!=null) EditProfile(clicked.Profile); return; } ActivateRow(index, layout.Refresh.Contains(e.Location) || layout.Ping.Contains(e.Location) || layout.More.Contains(e.Location) || layout.Delete.Contains(e.Location) ? e.X : 0);
        }
        void DrawAction(Graphics graphics, string icon, Rectangle bounds)
        {
            int size = (int)Math.Round(18 * scale);
            bool hovered = hoverAction == bounds;
            if (hovered) Design.Box(graphics, bounds, Design.Mint, Design.Mint, 7);
            Design.Icon(graphics, icon, new Rectangle(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size), hovered ? icon == "close" ? Design.Red : Design.Green : Design.Muted);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); int index = IndexFromPoint(e.Location); string text = ""; Rectangle hover = Rectangle.Empty;
            if (index >= 0 && index < Items.Count)
            {
                var row = (SubscriptionRow)Items[index];
                if(row.Profile!=null) { var bounds=GetItemRectangle(index); text=L10n.T("Страна: ")+(ServerCountry.Code(row.Profile.Name)==""?L10n.T("не определена"):ServerCountry.Code(row.Profile.Name))+" · "+(String.IsNullOrEmpty(row.Profile.ProviderDocumentId)&&String.IsNullOrEmpty(row.Profile.ProviderConfig)?L10n.T("Маршруты подписки не получены"):L10n.T("Маршруты: ")+row.Profile.ProviderFormat); if(e.X>=bounds.Right-(int)(40*scale)) { hover=new Rectangle(bounds.Right-(int)(40*scale),bounds.Y,(int)(40*scale),bounds.Height); text=L10n.T("Просмотр и редактирование JSON сервера"); } }
                if (row.Subscription != null)
                {
                    var layout = new SubscriptionHeaderLayout(GetItemRectangle(index), scale);
                    foreach (var button in new[] { layout.Refresh, layout.Ping, layout.More, layout.Delete }) if (button.Contains(e.Location)) hover = button;
                    text = layout.Delete.Contains(e.Location) ? L10n.T("Удалить подписку") : layout.More.Contains(e.Location) ? L10n.T("Изменить подписку") : layout.Ping.Contains(e.Location) ? L10n.T("Проверить серверы по HTTPS") : layout.Refresh.Contains(e.Location) ? L10n.T("Обновить подписку") : row.Title;
                }
            }
            if (hints.GetToolTip(this) != text) hints.SetToolTip(this, text);
            if (hover != hoverAction) { Rectangle old = hoverAction; hoverAction = hover; if (!old.IsEmpty) Invalidate(old); if (!hover.IsEmpty) Invalidate(hover); Cursor = hover.IsEmpty ? Cursors.Default : Cursors.Hand; }
        }
        protected override void OnMouseLeave(EventArgs e) { var old = hoverAction; hoverAction = Rectangle.Empty; if (!old.IsEmpty) Invalidate(old); base.OnMouseLeave(e); }
        protected override void Dispose(bool disposing) { if (disposing) hints.Dispose(); base.Dispose(disposing); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Enter && SelectedIndex >= 0) { ActivateRow(SelectedIndex, 0); e.Handled = true; } }
        void ActivateRow(int index, int x)
        {
            if (index < 0 || index >= Items.Count) return;
            var row = (SubscriptionRow)Items[index];
            if (row.Profile != null) { if (SelectProfile != null) SelectProfile(row.Profile); return; }
            var layout = new SubscriptionHeaderLayout(GetItemRectangle(index), scale);
            if (row.Subscription != null && x >= layout.Refresh.Left && x < layout.Delete.Right)
            {
                var action = x >= layout.Delete.Left ? DeleteSubscription : x >= layout.More.Left ? EditSubscription : x >= layout.Ping.Left ? TestSubscription : RefreshSubscription;
                if (action != null) action(row.Subscription); return;
            }
            if (!collapsed.Add(row.GroupId)) collapsed.Remove(row.GroupId); if (Rebuild != null) Rebuild();
        }
    }
}
