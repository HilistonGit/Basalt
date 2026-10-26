using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    public sealed partial class MainForm : Form
    {
        readonly string dataDirectory; readonly SettingsStore store; readonly Settings settings; readonly CoreRuntime runtime; readonly WindowsProxy proxy;
        readonly Panel content = new BufferedPanel(), sidebar = new BufferedPanel(), navigation = new BufferedPanel();
        readonly Label notice = new Label(), sidebarStatus = new Label();
        readonly ActivityStrip activity = new ActivityStrip();
        readonly NotifyIcon tray = new NotifyIcon(); readonly System.Windows.Forms.Timer monitor = new System.Windows.Forms.Timer();
        readonly TextBox logs = new TextBox(); readonly Dictionary<string, int> latency = new Dictionary<string, int>();
        readonly List<ActionButton> navButtons = new List<ActionButton>();
        SubscriptionBrowser servers; TextBox search; ComboBox sorting; Label resultCount, connectionState, connectionDetail, uptime, currentServer, engineLabel;
        ActionButton connect; PowerButton power; Panel emptyState;
        string page = "servers", group; bool favorites, busy, quitting, rendering; int activePort; string activeId;
        DateTime connectedAt; CancellationTokenSource cancellation;
        public MainForm(string data) : this(data, false) { }
        public MainForm(string data, bool preview)
        {
            dataDirectory = data; rendering = preview; store = new SettingsStore(Path.Combine(data, "settings.dat"), new WindowsSecrets()); settings = store.Load(); L10n.SetLanguage(settings.Language); settings.MigrateSubscriptionDefaults(); if (!File.Exists(Path.Combine(data, "settings.dat"))) settings.TunMode = true; CoreCatalog.Configure(settings); if (String.IsNullOrEmpty(settings.DeviceId)) settings.DeviceId = Guid.NewGuid().ToString("N"); if (!preview) store.Save(settings);
            proxy = new WindowsProxy(data); bool recovered = !preview && proxy.Recover();
            runtime = new CoreRuntime(Path.Combine(data, "runtime")); if (!preview) runtime.Stop(); runtime.Log += Log;
            DoubleBuffered = true;
            Text = "Basalt"; ClientSize = new Size(1180, 780); MinimumSize = new Size(1024, 740); StartPosition = FormStartPosition.CenterScreen;
            BackColor = Design.Background; ForeColor = Design.Ink; Font = Design.Font(10, false); AutoScaleMode = AutoScaleMode.Dpi; KeyPreview = true;
            sidebar.Dock = DockStyle.Left; sidebar.Width = 210; sidebar.BackColor = Design.Surface;
            var brand = new BufferedPanel { Dock = DockStyle.Top, Height = 100 };
            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Design.Green)) e.Graphics.FillPolygon(b, new[] { new Point(28, 42), new Point(43, 33), new Point(58, 42), new Point(58, 61), new Point(43, 70), new Point(28, 61) });
                using (var pen = new Pen(Color.White, 2)) { e.Graphics.DrawLine(pen, 38, 45, 38, 58); e.Graphics.DrawArc(pen, 35, 44, 14, 8, -90, 180); e.Graphics.DrawArc(pen, 35, 51, 14, 8, -90, 180); }
                using (var f = Design.Font(21, true)) Design.Text(e.Graphics, "basalt", f, new Rectangle(72, 34, 120, 42), Design.Ink, TextFormatFlags.Left);
                using (var f = Design.Font(9, false)) Design.Text(e.Graphics, L10n.T("Личное подключение"), f, new Rectangle(28, 82, 170, 20), Design.Muted, TextFormatFlags.Left);
            };
            var bottom = new BufferedPanel { Dock = DockStyle.Bottom, Height = 118, BackColor = Design.Surface };
            sidebarStatus.SetBounds(26, 16, 165, 28); sidebarStatus.Font = Design.Font(9, false); bottom.Controls.Add(sidebarStatus);
            Place(bottom, Caption("Basalt 0.6.7  /  Desktop", 9, Design.Muted), 26, 58, 160, 24);
            navigation.Dock = DockStyle.Fill; navigation.AutoScroll = true; sidebar.Controls.Add(navigation); sidebar.Controls.Add(bottom); sidebar.Controls.Add(brand);
            var footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(24, 8, 20, 0), BackColor = Design.Surface };
            notice.Dock = DockStyle.Fill; notice.Font = Design.Font(9, false); notice.ForeColor = Design.Muted; footer.Controls.Add(notice); activity.Dock = DockStyle.Top; activity.Height = 4; footer.Controls.Add(activity);
            content.Dock = DockStyle.Fill; content.Padding = new Padding(26, 12, 26, 16);
            var main = new BufferedPanel { Dock = DockStyle.Fill }; main.Controls.Add(content); main.Controls.Add(footer); Controls.Add(main); Controls.Add(sidebar); SetupChrome();
            logs.Multiline = true; logs.ReadOnly = true; logs.ScrollBars = ScrollBars.Vertical; logs.BackColor = Color.FromArgb(27, 37, 46); logs.ForeColor = Color.FromArgb(216, 231, 223); logs.BorderStyle = BorderStyle.None; logs.Font = new Font("Consolas", 10);
            using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("Basalt.Icon")) Icon = new Icon(iconStream, 32, 32); tray.Icon = Icon; tray.Text = L10n.T("Basalt — отключено"); tray.Visible = !preview;
            var menu = new ContextMenuStrip(); menu.Items.Add(L10n.T("Открыть Basalt"), null, delegate { Restore(); }); menu.Items.Add(L10n.T("Подключить / отключить"), null, delegate { if (!busy) Toggle(); }); menu.Items.Add(L10n.T("Выход"), null, delegate { quitting = true; if (cancellation != null) cancellation.Cancel(); if (!busy) Close(); else Notify(L10n.T("Завершаем текущую операцию перед выходом…"), false); }); tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { Restore(); };
            monitor.Interval = 1000; monitor.Tick += delegate
            {
                if(autoConnectPending && DateTime.UtcNow>=nextAutoConnect && !busy) BeginAutoConnect();
                RefreshDueSubscription();
                if (!busy && runtime.Running && runtime.HasFailed()) { Guard(Disconnect); Notify(L10n.T("Ядро завершилось. Соединение остановлено."), true); }
                if (uptime != null && !uptime.IsDisposed) uptime.Text = runtime.Running ? L10n.T("СЕАНС  ") + (DateTime.Now - connectedAt).ToString(@"hh\:mm\:ss") : L10n.T("СЕАНС НЕ ЗАПУЩЕН");
            }; if (!preview) monitor.Start();
            Shown += delegate { if(!rendering && settings.AutoConnect) BeginInvoke((Action)BeginAutoConnect); };
            FormClosing += HandleClosing; KeyDown += HandleKeys;
            BuildNavigation(); ShowPage("servers"); Notify(recovered ? L10n.T("Предыдущие настройки прокси восстановлены.") : L10n.T("Готов к работе. Добавьте подписку или сервер."), false);
        }
        static Label Caption(string text, float size, Color color) { return new Label { Text = text, Font = Design.Font(size, false), ForeColor = color, AutoSize = false, BackColor = Color.Transparent }; }
        static ActionButton Button(string text, string glyph, Action action, bool primary)
        { var button = new ActionButton { Text = text, Glyph = glyph, Primary = primary }; button.Click += delegate { action(); }; return button; }
        static TextBox Input(string text) { return new TextBox { Text = text ?? "", BorderStyle = BorderStyle.FixedSingle, BackColor = Design.Surface, ForeColor = Design.Ink, Font = Design.Font(11, false) }; }
        static ComboBox Choice(params string[] choices)
        {
            var box = new DarkComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24, FlatStyle = FlatStyle.Flat, BackColor = Design.Surface, ForeColor = Design.Ink, Font = Design.Font(10, false) };
            box.Items.AddRange(choices); box.SelectedIndex = 0;
            box.DrawItem += delegate(object sender, DrawItemEventArgs e) { using (var brush = new SolidBrush(Design.Surface)) e.Graphics.FillRectangle(brush, e.Bounds); Design.Text(e.Graphics, e.Index >= 0 ? (string)box.Items[e.Index] : box.Text, box.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), Design.Ink, TextFormatFlags.VerticalCenter); }; return box;
        }
        static void Place(Control parent, Control child, int x, int y, int width, int height) { child.SetBounds(x, y, width, height); parent.Controls.Add(child); }
        void BuildNavigation()
        {
            while (navigation.Controls.Count > 0) navigation.Controls[0].Dispose(); navButtons.Clear(); int y = 16;
            Nav(L10n.T("Подписки"), "globe", "servers", null, false, ref y);

            y += 18; Nav(L10n.T("Маршрутизация"), "route", "routing", null, false, ref y);
            Nav(L10n.T("Настройки"), "settings", "settings", null, false, ref y); Nav(L10n.T("Журнал"), "log", "logs", null, false, ref y);
        }
        void Nav(string label, string glyph, string target, string filter, bool starred, ref int y)
        {
            var button = Button(label, glyph, delegate { if (busy) return; group = filter; favorites = starred; ShowPage(target); }, false);
            button.Navigation = true; button.Selected = page == target && group == filter && favorites == starred; button.Tag = new object[] { target, filter, starred }; button.Font = Design.Font(9.5f, false);
            Place(navigation, button, 14, y, 182, 44); y += 49; navButtons.Add(button);
        }
        void ShowPage(string target)
        {
            if (busy) return; if (target == "subscriptions") target = "servers"; if (target == page && content.Controls.Count > 0) { if (page == "servers") RefreshServers(); UpdateState(); return; } CachePage(); page = target; ((BufferedPanel)content).BeginRedraw(); content.SuspendLayout();
            try
            {
            content.Controls.Clear();
            servers = null; search = null; connectionState = null; connectionDetail = null; currentServer = null; engineLabel = null; connect = null; power = null; uptime = null; 
            foreach (var button in navButtons) { var info = (object[])button.Tag; button.Selected = (string)info[0] == page && (string)info[1] == group && (bool)info[2] == favorites; button.Invalidate(); }
            if (RestorePage(page)) { if (page == "servers") RefreshServers(); } else if (page == "servers") ShowServers(); else if (page == "subscriptions") ShowSubscriptions(); else if (page == "routing") ShowRouting(); else if (page == "settings") ShowSettings(); else ShowLogs();
            }
            finally { content.ResumeLayout(true); ((BufferedPanel)content).EndRedraw(); }
            UpdateState();
        }
        Panel Header(string title, string subtitle)
        {
            var header = new BufferedPanel { Dock = DockStyle.Top, Height = 82 };
            var heading = Caption(title, 23, Design.Ink); heading.Font = Design.Font(23, true); Place(header, heading, 0, 0, 550, 42);
            Place(header, Caption(subtitle, 9.5f, Design.Muted), 1, 49, 680, 35); return header;
        }
        Profile SelectedProfile { get { return settings.Profiles.Find(delegate(Profile p) { return p.Id == settings.SelectedId; }); } }
        void Save() { ClearPageCache(); if (!rendering) store.Save(settings); }
        void Notify(string message, bool error) { notice.Text = message.Replace("\r", " ").Replace("\n", " "); notice.ForeColor = error ? Design.Red : Design.Muted; Log(message); }
        void Log(string message)
        {
            if (IsDisposed) return; if (InvokeRequired) { try { BeginInvoke((Action<string>)Log, message); } catch (InvalidOperationException) { } return; }
            if (logs.TextLength > 50000) logs.Text = logs.Text.Substring(logs.TextLength - 30000); logs.AppendText(DateTime.Now.ToString("HH:mm:ss") + "   " + message + Environment.NewLine);
        }
        void Guard(Action action) { try { action(); } catch (Exception e) { Error(e); } }
        void Error(Exception e) { var import = e as SubscriptionImportException; if (import != null) { ShowImportError(import); return; } Notify(e.Message, true); MessageBox.Show(this, e.Message, "Basalt", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        void Work(Func<object> work, Action<object> done, string label)
        {
            if (busy) return; busy = true; content.Enabled = false; navigation.Enabled = false; UseWaitCursor = true; notice.Text = label; activity.Active = true;
            var worker = new BackgroundWorker(); worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = work(); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            { busy = false; if (IsDisposed) { worker.Dispose(); return; } if (quitting) { worker.Dispose(); Close(); return; } activity.Active = autoUpdating; content.Enabled = true; navigation.Enabled = true; UseWaitCursor = false; if (e.Error != null) { if (cancellation != null) { cancellation.Dispose(); cancellation = null; } Error(e.Error); } else Guard(delegate { done(e.Result); }); UpdateState(); worker.Dispose(); }; worker.RunWorkerAsync();
        }
        void UpdateState()
        {
            bool running = runtime.Running; var profile = running ? settings.Profiles.Find(delegate(Profile p) { return p.Id == activeId; }) : SelectedProfile;
            sidebarStatus.Text = running ? settings.TunMode ? L10n.T("●  TUN подключён") : L10n.T("●  Прокси запущен") : L10n.T("○  Не подключено"); sidebarStatus.ForeColor = running ? Design.Green : Design.Muted; tray.Text = running ? L10n.T("Basalt — прокси запущен") : L10n.T("Basalt — отключено");
            if (connectionState != null) { connectionState.Text = running ? (settings.TunMode ? L10n.T("TUN подключён") : L10n.T("Прокси запущен")) : L10n.T("Не подключено"); connectionState.ForeColor = running ? Design.Green : Design.Ink; }
            if (connectionDetail != null) connectionDetail.Text = running ? "HTTP / SOCKS5  ·  127.0.0.1:" + activePort : L10n.T("Ваше соединение под контролем");
            if (currentServer != null) currentServer.Text = profile == null ? L10n.T("Выберите сервер слева") : profile.Name;
            if (engineLabel != null) engineLabel.Text = profile == null ? L10n.T("Сервер ещё не выбран") : profile.Engine + "  ·  " + profile.Protocol.ToUpperInvariant();
            if (connect != null) connect.Text = running ? L10n.T("Отключить") : L10n.T("Подключить");
            if (power != null) { power.Connected = running; power.Invalidate(); }

            if (servers != null) { servers.ActiveId = settings.SelectedId; servers.Invalidate(); }
        }
        void Toggle()
        {
            if (busy) return; if (runtime.Running) { Guard(Disconnect); return; }
            var profile = SelectedProfile; if (profile == null) { Notify(L10n.T("Добавьте сервер и выберите его в списке."), true); return; }
            if (settings.TunMode && !TunnelSession.IsAdministrator) { Guard(Elevate); return; }
            Start(profile);
        }
        void Start(Profile profile, bool automatic = false)
        {
            Work(delegate { try { CoreCatalog.Verify(); proxy.Recover(); runtime.Start(profile, settings); if(automatic) CoreRuntime.Probe(settings.ProxyPort,settings.ProxyUsername,settings.ProxyPassword,settings.TunMode); if (settings.SystemProxy && !settings.TunMode) proxy.Enable(settings.ProxyPort); return null; } catch { try { proxy.Recover(); } finally { runtime.Stop(); } if(automatic) return false; throw; } },
                delegate(object result) { if(result is bool && !(bool)result){RetryAutoConnect();Notify(L10n.English?"Automatic connection failed; retry scheduled.":"Автоподключение не удалось; запланирован повтор.",true);return;} autoConnectFailures=0; activePort = settings.ProxyPort; activeId = profile.Id; connectedAt = DateTime.Now; Notify(settings.TunMode ? L10n.T("TUN запущен. Проверьте соединение кнопкой HTTPS.") : L10n.T("Прокси запущен. Проверьте HTTPS в панели подключения."), false); }, L10n.T("Подключаемся к «") + profile.Name + "»…");
        }
        void Disconnect() { autoConnectPending=false; try { proxy.Recover(); } finally { runtime.Stop(); activeId = null; UpdateState(); } Notify(L10n.T("Отключено. Предыдущий системный прокси восстановлен."), false); }
        void Choose(Profile profile)
        {
            if (profile == null || busy || settings.SelectedId == profile.Id) return;
            Guard(delegate { settings.SelectedId = profile.Id; Save(); if (runtime.Running) Start(profile); else { UpdateState(); Notify(L10n.T("Выбран сервер «") + profile.Name + "».", false); } });
        }
        void Probe()
        {
            if (!runtime.Running) { Notify(L10n.T("Сначала подключитесь, чтобы проверить HTTPS через прокси."), true); return; }
            Work(delegate { return CoreRuntime.Probe(activePort, settings.ProxyUsername, settings.ProxyPassword, settings.TunMode); }, delegate(object result) { Notify((string)result, false); }, L10n.T("Проверяем HTTPS через подключение…"));
        }
        void Measure()
        {
            MeasureGroup(null);
        }
        void MeasureGroup(string subscriptionId)
        {
            var profiles = ProfileView.Filter(settings.Profiles, search == null ? "" : search.Text, subscriptionId ?? group, favorites, false, latency);
            if (profiles.Count == 0) return;
            if (runtime.Running && settings.TunMode) { Notify(L10n.T("Отключите TUN перед проверкой всех серверов. Текущее соединение можно проверить справа."), true); return; }
            cancellation = new CancellationTokenSource(); var token = cancellation.Token;
            Work(delegate
            {
                CoreCatalog.Verify(); var results = new Dictionary<string, int>();
                var checkSettings = new Settings { RouteDocuments = settings.RouteDocuments, UseSubscriptionRouting = settings.UseSubscriptionRouting, XrayPath = settings.XrayPath, SingBoxPath = settings.SingBoxPath, ProxyPort = FreePort(), HttpPort = FreePort(), BridgePort = FreePort() };
                while (checkSettings.HttpPort == checkSettings.ProxyPort) checkSettings.HttpPort = FreePort();
                while (checkSettings.BridgePort == checkSettings.ProxyPort || checkSettings.BridgePort == checkSettings.HttpPort) checkSettings.BridgePort = FreePort();
                using (var checker = new CoreRuntime(Path.Combine(dataDirectory, "probe-runtime")))
                foreach (var p in profiles)
                {
                    if (token.IsCancellationRequested) break;
                    int elapsed = -1;
                    try { checker.Start(p, checkSettings); if (!token.IsCancellationRequested) elapsed = CoreRuntime.Measure(checkSettings.ProxyPort, null, null, settings.PingTimeout, false); }
                    catch (Exception e) { if (!(e is System.Net.WebException || e is IOException || e is InvalidOperationException || e is SocketException || e is ArgumentException || e is TimeoutException)) throw; }
                    finally { checker.Stop(); }
                    results[p.Id] = elapsed; int completed = results.Count; string id = p.Id; int measured = elapsed; BeginInvoke((Action)delegate { latency[id] = measured; if (servers != null) servers.Invalidate(); notice.Text = L10n.T("Проверка серверов: ") + completed + " / " + profiles.Count + L10n.T(" · Esc — остановить"); });
                }
                return results;
            }, delegate(object value) { foreach (var pair in (Dictionary<string, int>)value) latency[pair.Key] = pair.Value; cancellation.Dispose(); cancellation = null; RefreshServers(); Notify(L10n.T("Проверка HTTPS через серверы завершена. Задержка включает TLS и ответ контрольного адреса."), false); }, L10n.T("Проверяем серверы по HTTPS… Esc — остановить"));
        }
        static int FreePort() { var listener = new TcpListener(System.Net.IPAddress.Loopback, 0); listener.Start(); try { return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; } finally { listener.Stop(); } }
        void ToggleFavorite(Profile profile) { Guard(delegate { profile.Favorite = !profile.Favorite; Save(); RefreshServers(); }); }
        void DeleteProfile(Profile profile)
        {
            if (profile == null) return; if (runtime.Running && activeId == profile.Id) { Notify(L10n.T("Отключитесь перед удалением активного сервера."), true); return; }
            if (MessageBox.Show(this, L10n.T("Удалить сервер «") + profile.Name + "»?", "Basalt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Guard(delegate { settings.Profiles.Remove(profile); if (settings.SelectedId == profile.Id) settings.SelectedId = settings.Profiles.Count == 0 ? null : settings.Profiles[0].Id; Save(); RefreshServers(); UpdateState(); });
        }
        void HandleKeys(object sender, KeyEventArgs e)
        {
            if (Form.ActiveForm != this) return;
            if (e.KeyCode == Keys.Escape && cancellation != null) { cancellation.Cancel(); e.Handled = true; } if (busy) return;
            if (e.Control && e.KeyCode == Keys.F && search != null) { search.Focus(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.V && !(ActiveControl is TextBoxBase)) { ImportClipboard(); e.SuppressKeyPress = true; }
            if (servers != null && servers.Focused && e.KeyCode == Keys.Delete) { DeleteProfile(servers.SelectedProfile); e.Handled = true; }
        }
        void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        void HandleClosing(object sender, FormClosingEventArgs e)
        {

            if (!rendering && !quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            ClearPageCache(); monitor.Stop(); if (!rendering) try { Disconnect(); } catch (Exception error) { if (e.CloseReason == CloseReason.UserClosing) { Error(error); e.Cancel = true; monitor.Start(); return; } }
            tray.Visible = false; tray.Dispose(); monitor.Dispose(); runtime.Dispose();
        }
    }
}
