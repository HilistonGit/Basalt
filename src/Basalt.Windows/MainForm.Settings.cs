using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    public sealed partial class MainForm
    {
        void Elevate()
        {
            Save();
            // Windows displays the UAC consent prompt. Cancellation leaves this instance intact.
            var start = new ProcessStartInfo(Application.ExecutablePath, "--elevated") { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory };
            using (var process = Process.Start(start)) { }
            quitting = true; Close();
        }
        static void SettingRow(TableLayoutPanel grid, string label, Control editor)
        {
            int row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var caption = Caption(label, 10, Design.Ink); caption.Dock = DockStyle.Fill; caption.MinimumSize = new Size(180, 32); caption.TextAlign = ContentAlignment.MiddleLeft; caption.Margin = new Padding(14, 4, 8, 4);
            editor.Dock = DockStyle.Fill; editor.Margin = new Padding(8, 7, 16, 7);
            caption.Tag = grid.Tag; editor.Tag = grid.Tag; grid.Controls.Add(caption, 0, row); grid.Controls.Add(editor, 1, row);
        }
        static void SettingHeading(TableLayoutPanel grid, string text)
        {
            int row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Tag = text; var label = Caption(text=="General"?(L10n.English?"General":"Интерфейс"):text=="Updates"?(L10n.English?"Updates":"Обновления"):text, 12, Design.Green); label.Font = Design.Font(12, true); label.Height = 34; label.Dock = DockStyle.Fill; label.MinimumSize = new Size(0, 34); label.TextAlign = ContentAlignment.BottomLeft; label.Margin = new Padding(14, 4, 14, 8);
            label.Tag = text; grid.Controls.Add(label, 0, row); grid.SetColumnSpan(label, 2);
        }
        static NumericUpDown Number(int value, int min, int max) { return new NumericUpDown { Minimum = min, Maximum = max, BackColor = Design.Surface, ForeColor = Design.Ink, Value = Math.Max(min, Math.Min(max, value)) }; }
        void ShowSettings()
        {
            var header = Header(L10n.T("Настройки"), L10n.T("Туннель, DNS и прокси для приложений."));
            var host = new BufferedPanel { Dock = DockStyle.Fill, AutoScroll = true };
            var grid = new BufferedTable { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0, BackColor = Design.Surface, Padding = new Padding(0, 0, 0, 18) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            SettingHeading(grid, "General");
            var language=Choice("System / Системный", "Русский", "English"); language.SelectedIndex=settings.Language=="ru"?1:settings.Language=="en"?2:0;
            SettingRow(grid,L10n.English?"Interface language":"Язык интерфейса",language);
            SettingRow(grid,L10n.English?"System default":"Язык системы",Caption(L10n.English?"Russian for Russian Windows; English otherwise.":"Русский для русской Windows, иначе английский.",9,Design.Muted));
            var autoStart=new ToggleSwitch{Text=L10n.English?"Enabled":"Включено",Checked=settings.AutoStart}; SettingRow(grid,L10n.English?"Start with Windows":"Запускать с Windows",autoStart);
            var autoConnect=new ToggleSwitch{Text=L10n.English?"Enabled":"Включено",Checked=settings.AutoConnect}; SettingRow(grid,L10n.English?"Connect on launch":"Подключаться при запуске",autoConnect);
            var startupOrder=Caption(L10n.English?"Load saved servers → refresh → connect. Saved servers survive failed updates.":"Сохранённые серверы → обновление → подключение. При ошибке остаётся прежний список.",9,Design.Muted);startupOrder.Height=62;SettingRow(grid,L10n.English?"Startup order":"Порядок запуска",startupOrder);
            SettingHeading(grid,"Updates");
            SettingRow(grid,L10n.English?"Application version":"Версия приложения",Caption(typeof(MainForm).Assembly.GetName().Version.ToString(3),11,Design.Ink));
            SettingRow(grid,"GitHub Releases",Button(L10n.English?"Check for updates":"Проверить обновления","refresh",delegate{CheckUpdates();},true));
            SettingRow(grid,L10n.English?"Project":"Проект",Button("HilistonGit / Basalt","",delegate{Process.Start(new ProcessStartInfo(Updates.Repository){UseShellExecute=true});},false));
            SettingRow(grid,"SHA-256",Caption(L10n.English?"Downloads are verified before use.":"Загрузки проверяются перед использованием.",9,Design.Muted));
            SettingHeading(grid, L10n.T("Подключение"));
            var mode = Choice(L10n.T("TUN · весь компьютер"), L10n.T("Системный прокси"), L10n.T("Прокси для приложений")); mode.SelectedIndex = settings.TunMode ? 0 : settings.SystemProxy ? 1 : 2; SettingRow(grid, L10n.T("Режим подключения"), mode);
            SettingRow(grid, L10n.T("Правила маршрутизации"), Button(L10n.T("Настроить"), "route", delegate { ShowPage("routing"); }, false));
            var bypass = new ToggleSwitch { Text = L10n.T("Напрямую"), Checked = settings.BypassLan }; SettingRow(grid, L10n.T("Локальная сеть (TUN)"), bypass);
            var ipv6 = new ToggleSwitch { Text = L10n.T("Разрешить"), Checked = settings.IPv6 }; SettingRow(grid, L10n.T("IPv6 в туннеле"), ipv6);
            var mtu = Number(settings.TunMtu, 1280, 9000); SettingRow(grid, L10n.T("MTU туннеля"), mtu);
            var apps = Input(settings.ApplicationBypass); apps.Multiline = true; apps.Height = 72; apps.ScrollBars = ScrollBars.Vertical; SettingRow(grid, L10n.T("Приложения напрямую (TUN)\nИмена exe, по одному на строку"), apps);
            SettingHeading(grid, L10n.T("DNS туннеля"));
            var direct = Input(settings.DirectDns); SettingRow(grid, L10n.T("DNS для прямых адресов (IP)"), direct);
            var remote = Input(settings.RemoteDns); SettingRow(grid, L10n.T("DNS через VPN (IP)"), remote);
            SettingHeading(grid, L10n.T("Прокси для приложений"));
            var mixed = Number(settings.ProxyPort, 1024, 65535); SettingRow(grid, L10n.T("SOCKS5 / HTTP · смешанный порт"), mixed);
            var http = Number(settings.HttpPort, 1024, 65535); SettingRow(grid, L10n.T("Отдельный HTTP-порт"), http);
            var lan = new ToggleSwitch { Text = L10n.T("Разрешить с паролем"), Checked = settings.AllowLan }; SettingRow(grid, L10n.T("Подключения из LAN"), lan);
            var user = Input(settings.ProxyUsername); SettingRow(grid, L10n.T("Имя пользователя прокси"), user);
            var password = Input(settings.ProxyPassword); password.UseSystemPasswordChar = true; SettingRow(grid, L10n.T("Пароль прокси"), password);
            SettingHeading(grid, L10n.T("Проверка серверов"));
            var timeout = Number(settings.PingTimeout, 1000, 15000); SettingRow(grid, L10n.T("Таймаут HTTPS-проверки, мс"), timeout);
            SettingHeading(grid, L10n.T("Подписки"));
            var agent = UserAgents(settings.SubscriptionUserAgent); SettingRow(grid, L10n.T("User-Agent для всех подписок"), agent);
            var hwid = HwidChoice(settings.SubscriptionHwidMode); SettingRow(grid, L10n.T("Передача HWID"), hwid);
            SettingRow(grid, L10n.T("Интервал автообновления"), Button(L10n.T("В заголовке подписки"), "settings", delegate { ShowPage("servers"); }, false));
            SettingHeading(grid, L10n.T("Встроенные ядра"));
            var versions = Caption(CoreCatalog.Versions + "\nLegacy · " + (CoreCatalog.Architecture == "amd64" ? "x64" : "x86") + " · Wintun 0.14.1", 9, Design.Muted); versions.Height = 52; SettingRow(grid, L10n.T("Ядра выбираются автоматически"), versions);
            var save = Button(L10n.T("Сохранить"), "check", delegate { Guard(delegate {
                if (runtime.Running) throw new InvalidOperationException(L10n.T("Отключитесь перед изменением настроек подключения."));
                if (String.IsNullOrWhiteSpace(agent.Text) || agent.Text.Contains("\r") || agent.Text.Contains("\n")) throw new ArgumentException(L10n.T("Укажите корректный User-Agent."));
                if (mixed.Value == http.Value || mixed.Value == settings.BridgePort || http.Value == settings.BridgePort) throw new ArgumentException(L10n.T("Порты должны отличаться; порт ") + settings.BridgePort + L10n.T(" занят внутренним мостом."));
                IPAddress address; if (!IPAddress.TryParse(direct.Text.Trim(), out address) || !IPAddress.TryParse(remote.Text.Trim(), out address)) throw new ArgumentException(L10n.T("Укажите IP-адреса DNS-серверов."));
                if (lan.Checked && (String.IsNullOrWhiteSpace(user.Text) || String.IsNullOrEmpty(password.Text))) throw new ArgumentException(L10n.T("Для доступа из LAN задайте имя пользователя и пароль."));
                if (mode.SelectedIndex == 1 && !String.IsNullOrWhiteSpace(user.Text)) throw new ArgumentException(L10n.T("Автоматический системный прокси не поддерживает авторизацию. Выберите TUN или настройте прокси в приложениях."));
                if(autoStart.Checked!=settings.AutoStart) StartupRegistration.Configure(autoStart.Checked); settings.AutoStart=autoStart.Checked; settings.AutoConnect=autoConnect.Checked; if(!settings.AutoConnect) autoConnectPending=false;
                settings.TunMode = mode.SelectedIndex == 0; settings.SystemProxy = mode.SelectedIndex == 1; settings.BypassLan = bypass.Checked; settings.IPv6 = ipv6.Checked; settings.TunMtu = (int)mtu.Value;
                settings.ApplicationBypass = apps.Text; settings.DirectDns = direct.Text.Trim(); settings.RemoteDns = remote.Text.Trim(); settings.ProxyPort = (int)mixed.Value; settings.HttpPort = (int)http.Value;
                settings.AllowLan = lan.Checked; settings.ProxyUsername = user.Text.Trim(); settings.ProxyPassword = password.Text; settings.PingTimeout = (int)timeout.Value;
                settings.SubscriptionUserAgent = agent.Text.Trim(); settings.SubscriptionHwidMode = HwidValue(hwid);
                foreach (var subscription in settings.Subscriptions) { subscription.UserAgent = settings.SubscriptionUserAgent; subscription.HwidMode = settings.SubscriptionHwidMode; }
                string selectedLanguage=language.SelectedIndex==1?"ru":language.SelectedIndex==2?"en":"auto"; if(selectedLanguage!=(settings.Language??"auto")){ChangeLanguage(selectedLanguage);return;} Save(); Notify(L10n.T("Настройки сохранены."), false);
            }); }, true); save.Height = 38; save.Dock = DockStyle.Right; save.Width = 155;
            var saveBar = new BufferedPanel { Dock = DockStyle.Bottom, Height = 48 }; saveBar.Controls.Add(save);
                        var tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Design.Background, WrapContents = false, AutoScroll = true };
            string[] sections = { "General", "Updates", L10n.T("Подключение"), L10n.T("DNS туннеля"), L10n.T("Прокси для приложений"), L10n.T("Проверка серверов"), L10n.T("Подписки"), L10n.T("Встроенные ядра") };
            string[] labels = { L10n.English?"General":"Интерфейс", L10n.English?"Updates":"Обновления", L10n.T("Туннель"), "DNS", L10n.T("Прокси"), L10n.T("Пинг"), L10n.T("Подписки"), L10n.T("Ядра") };
            Action<string> selectSection = delegate(string selected) { grid.SuspendLayout(); foreach (Control control in grid.Controls) control.Visible = (string)control.Tag == selected; grid.ResumeLayout(true); foreach (ActionButton tab in tabs.Controls) { tab.Selected = (string)tab.Tag == selected; tab.Invalidate(); } };
            for (int i = 0; i < sections.Length; i++) { string section = sections[i]; var tab = Button(labels[i], "", delegate { selectSection(section); }, false); tab.Tag = section; tab.Width = i == 6 ? 120 : 94; tab.Height = 36; tabs.Controls.Add(tab); }
            grid.MaximumSize = new Size(780, 0);
            host.Controls.Add(grid); content.Controls.Add(host); content.Controls.Add(saveBar); content.Controls.Add(tabs); content.Controls.Add(header); selectSection(sections[0]);
        }
    }
}
