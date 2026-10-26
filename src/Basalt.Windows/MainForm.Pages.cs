using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    public sealed partial class MainForm
    {
        void ShowServers()
        {
            string title = favorites ? L10n.T("Избранное") : group == "manual" ? L10n.T("Мои серверы") : L10n.T("Подписки");
            var subscription = settings.Subscriptions.Find(delegate(Subscription s) { return s.Id == group; }); if (subscription != null) title = subscription.Name;
            var header = Header(title, L10n.T("Выберите сервер. Подключитесь. Оставайтесь на связи."));
            var add = Button(L10n.T("Вставить"), "plus", ImportClipboard, true); Place(header, add, header.Width - 160, 4, 148, 42); add.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            var columns = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Design.Background, Margin = Padding.Empty };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 308)); columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var left = new BufferedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 22, 0) };
            var toolbar = new BufferedPanel { Dock = DockStyle.Top, Height = 106 };
            var searchFrame = new Card { Padding = new Padding(38, 12, 10, 8), Dock = DockStyle.Top, Height = 44 };
            searchFrame.Paint += delegate(object sender, PaintEventArgs e) { Design.Icon(e.Graphics, "search", new Rectangle(12, 12, 20, 20), Design.Muted); };
            search = Input(""); search.BorderStyle = BorderStyle.None; search.Dock = DockStyle.Fill; search.AccessibleName = L10n.T("Поиск по имени, адресу или протоколу"); searchFrame.Controls.Add(search);
            var hint = Caption(L10n.T("Поиск по имени, адресу, протоколу"), 10, Design.Muted); hint.Dock = DockStyle.Fill; searchFrame.Controls.Add(hint); hint.BringToFront(); hint.Click += delegate { search.Focus(); };
            search.TextChanged += delegate { hint.Visible = search.Text.Length == 0; RefreshServers(); };
            toolbar.Controls.Add(searchFrame);
            resultCount = Caption("", 9, Design.Muted); Place(toolbar, resultCount, 0, 64, 160, 24);
            sorting = Choice(L10n.T("По названию"), L10n.T("По задержке")); Place(toolbar, sorting, 160, 61, 166, 28); sorting.SelectedIndexChanged += delegate { RefreshServers(); };
            var test = Button(L10n.T("Проверить"), "bolt", Measure, false); Place(toolbar, test, 340, 55, 135, 38); test.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            toolbar.SizeChanged += delegate { test.Left = toolbar.Width - test.Width; sorting.Left = Math.Max(110, test.Left - sorting.Width - 10); resultCount.Width = Math.Max(80, sorting.Left - 12); };
            servers = new SubscriptionBrowser { Dock = DockStyle.Fill, Latency = latency };
            servers.SelectProfile += Choose; servers.EditProfile += EditServerJson;
            servers.RefreshSubscription += UpdateSubscription;
            servers.TestSubscription += delegate(Subscription item) { MeasureGroup(item.Id); };
            servers.EditSubscription += EditSubscription; servers.DeleteSubscription += DeleteSubscription;
            servers.Rebuild += RefreshServers;
            var listHost = new BufferedPanel { Dock = DockStyle.Fill }; listHost.Controls.Add(servers);
            emptyState = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Design.Surface }; listHost.Controls.Add(emptyState);
            var emptyHeading = Caption(L10n.T("Добавьте подписку"), 18, Design.Ink); emptyHeading.TextAlign = ContentAlignment.MiddleCenter; emptyHeading.Font = Design.Font(18, true);
            Place(emptyState, emptyHeading, 20, 104, 360, 36); emptyHeading.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var emptyText = Caption(L10n.T("Вставьте ссылку от провайдера или добавьте\nсвои серверы из файла."), 10, Design.Muted); emptyText.TextAlign = ContentAlignment.TopCenter; Place(emptyState, emptyText, 20, 153, 360, 55); emptyText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var import = Button(L10n.T("Вставить из буфера"), "plus", ImportClipboard, true); Place(emptyState, import, 92, 225, 240, 44);
            var file = Button(L10n.T("Импортировать файл"), "folder", ImportFile, false); Place(emptyState, file, 92, 282, 240, 42);
            emptyState.Paint += delegate(object sender, PaintEventArgs e) { Design.Icon(e.Graphics, "globe", new Rectangle(emptyState.Width / 2 - 22, 36, 44, 44), Design.Green); };
            emptyState.SizeChanged += delegate { emptyHeading.Width = emptyState.Width - 40; emptyText.Width = emptyState.Width - 40; import.Left = (emptyState.Width - import.Width) / 2; file.Left = import.Left; };
            var listFooter = Caption(L10n.T("Обновление и проверка доступны в заголовке подписки"), 8.5f, Design.Muted); listFooter.Dock = DockStyle.Bottom; listFooter.Height = 34; listFooter.Padding = new Padding(0, 10, 0, 0);
            left.Controls.Add(listHost); left.Controls.Add(listFooter); left.Controls.Add(toolbar);
            columns.Controls.Add(left, 0, 0); columns.Controls.Add(ConnectionPanel(), 1, 0); content.Controls.Add(columns); content.Controls.Add(header); RefreshServers();
        }
        Panel ConnectionPanel()
        {
            var panel = new BufferedPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, AutoScroll = true };
            var card = new Card { Dock = DockStyle.Top, Height = 398 };
            uptime = Caption(L10n.T("СЕАНС НЕ ЗАПУЩЕН"), 8, Design.Muted); uptime.TextAlign = ContentAlignment.MiddleCenter; Place(card, uptime, 16, 18, 274, 22);
            power = new PowerButton(); Place(card, power, 106, 44, 96, 96); power.Click += delegate { Toggle(); };
            connectionState = Caption("", 18, Design.Ink); connectionState.Font = Design.Font(18, true); connectionState.TextAlign = ContentAlignment.MiddleCenter; Place(card, connectionState, 12, 153, 282, 34);
            connectionDetail = Caption("", 9, Design.Muted); connectionDetail.TextAlign = ContentAlignment.TopCenter; Place(card, connectionDetail, 16, 194, 274, 30);
            var line = new BufferedPanel { BackColor = Design.Line }; Place(card, line, 22, 218, 264, 1);
            currentServer = Caption("", 11, Design.Ink); currentServer.Font = Design.Font(11, true); currentServer.AutoEllipsis = true; Place(card, currentServer, 24, 228, 258, 25);
            engineLabel = Caption("", 9, Design.Muted); Place(card, engineLabel, 24, 258, 260, 23);
            connect = Button(L10n.T("Подключить"), "", Toggle, true); Place(card, connect, 22, 291, 264, 42);
            var probe = Button(L10n.T("Проверить соединение"), "bolt", Probe, false); Place(card, probe, 22, 344, 264, 38);
            var modes = new Card { Dock = DockStyle.Top, Height = 136, Margin = new Padding(0, 14, 0, 0) };
            Place(modes, Caption(L10n.T("Режим подключения"), 10, Design.Ink), 18, 15, 272, 25);
            var mode = Choice(L10n.T("TUN · весь компьютер"), L10n.T("Системный прокси"), L10n.T("Прокси для приложений"));
            mode.SelectedIndex = settings.TunMode ? 0 : settings.SystemProxy ? 1 : 2;
            Place(modes, mode, 18, 52, 270, 32);
            mode.SelectedIndexChanged += delegate { Guard(delegate {
                if (runtime.Running) Disconnect(); settings.TunMode = mode.SelectedIndex == 0;
                settings.SystemProxy = mode.SelectedIndex == 1; Save(); UpdateState();
            }); };
            Place(modes, Caption(L10n.T("TUN требует прав администратора"), 9, Design.Muted), 18, 96, 272, 28);
            var gap = new BufferedPanel { Dock = DockStyle.Top, Height = 14 };
            panel.Controls.Add(modes); panel.Controls.Add(gap); panel.Controls.Add(card); return panel;
        }
        void RefreshServers()
        {
            if (servers == null) return;
            var filtered = ProfileView.Filter(settings.Profiles, search == null ? "" : search.Text, group, favorites, sorting != null && sorting.SelectedIndex == 1, latency);
            servers.Populate(settings, filtered);
            resultCount.Text = L10n.T("Серверов: ") + filtered.Count;
            emptyState.Visible = settings.Subscriptions.Count == 0 && filtered.Count == 0; servers.Visible = !emptyState.Visible;
            if (emptyState.Visible)
            {
                var labels = new List<Label>(); foreach (Control c in emptyState.Controls) if (c is Label) labels.Add((Label)c);
                if (labels.Count >= 2) { labels[0].Text = settings.Profiles.Count == 0 ? L10n.T("Добавьте подписку") : L10n.T("Здесь пока пусто"); labels[1].Text = settings.Profiles.Count == 0 ? L10n.T("Вставьте ссылку от провайдера или добавьте\nсвои серверы из файла.") : L10n.T("Измените поиск или фильтр.\nДобавляйте серверы в избранное кнопкой ☆."); }
            }
        }
        void ShowSubscriptions()
        {
            var header = Header(L10n.T("Подписки"), L10n.T("Серверы вашего провайдера — в одной группе."));
            var add = Button(L10n.T("Добавить подписку"), "plus", ImportClipboard, true); Place(header, add, header.Width - 210, 4, 198, 42); add.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            if (settings.Subscriptions.Count == 0)
            {
                var empty = new Card { Width = 650, Height = 205, Margin = new Padding(0, 0, 0, 14) };
                Place(empty, Caption(L10n.T("Все серверы обновляются вместе"), 17, Design.Ink), 24, 24, 590, 35);
                Place(empty, Caption(L10n.T("Добавьте HTTPS-ссылку от провайдера. Basalt сохранит\nвыбранный сервер и избранное при следующем обновлении."), 10, Design.Muted), 24, 74, 590, 58);
                Place(empty, Button(L10n.T("Добавить подписку"), "plus", ImportClipboard, true), 24, 143, 220, 42); list.Controls.Add(empty);
            }
            foreach (var s in settings.Subscriptions)
            {
                var subscription = s; var card = new Card { Width = 700, Height = 190, Margin = new Padding(0, 0, 0, 14) };
                var title = Caption(s.Name, 16, Design.Ink); title.Font = Design.Font(16, true); Place(card, title, 22, 20, 440, 32);
                Uri url; string host = Uri.TryCreate(s.Url, UriKind.Absolute, out url) ? url.Host : L10n.T("Подписка");
                DateTime updated; string when = DateTime.TryParse(s.Updated, out updated) ? updated.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : L10n.T("ещё не обновлялась");
                int count = settings.Profiles.FindAll(delegate(Profile p) { return p.SubscriptionId == s.Id; }).Count;
                Place(card, Caption(host + "  ·  " + count + L10n.T(" серверов"), 10, Design.Muted), 22, 64, 620, 25);
                Place(card, Caption(L10n.T("Обновлено: ") + when, 9, Design.Muted), 22, 96, 620, 25);
                Place(card, Button(L10n.T("Открыть серверы"), "folder", delegate { group = subscription.Id; favorites = false; ShowPage("servers"); }, false), 22, 134, 195, 38);
                Place(card, Button(L10n.T("Обновить"), "refresh", delegate { UpdateSubscription(subscription); }, false), 230, 134, 150, 38);
                Place(card, Button(L10n.T("Изменить"), "", delegate { EditSubscription(subscription); }, false), 393, 134, 125, 38);
                Place(card, Button(L10n.T("Удалить"), "", delegate { DeleteSubscription(subscription); }, false), 531, 134, 115, 38);
                list.Controls.Add(card);
            }
            list.SizeChanged += delegate { foreach (Control card in list.Controls) card.Width = Math.Max(650, list.ClientSize.Width - 24); };
            content.Controls.Add(list); content.Controls.Add(header);
        }
        void ShowRouting()
        {
            var header = Header(L10n.T("Маршрутизация"), L10n.T("Локальные исключения имеют приоритет над правилами подписки."));
            var provider = new ToggleSwitch { Text = L10n.T("Правила подписки"), Checked = settings.UseSubscriptionRouting, Width = 245, Height = 36, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Place(header, provider, header.Width - 255, 5, 245, 36);
            provider.CheckedChanged += delegate { settings.UseSubscriptionRouting = provider.Checked; Save(); Notify(L10n.T("Правила подписки ") + (provider.Checked ? L10n.T("включены") : L10n.T("отключены")) + L10n.T(". Переподключитесь для применения."), false); };
            var host = new BufferedPanel { Dock = DockStyle.Fill, AutoScroll = true };
            var card = new Card { Dock = DockStyle.Top, Height = 580, Width = content.ClientSize.Width - 52 }; var options = new List<ActionButton>(); int mode = settings.RouteMode;
            string[] titles = { L10n.T("Без локальных исключений"), L10n.T("Только выбранные адреса"), L10n.T("Всё, кроме выбранных адресов") };
            string[] subtitles = { L10n.T("Правила подписки; если их нет — весь трафик через VPN."), L10n.T("Адреса из списка — через сервер, остальные — напрямую."), L10n.T("Адреса из списка — напрямую, остальные — через сервер.") };
            for (int i = 0; i < 3; i++)
            {
                int value = i; var option = Button(titles[i], "route", delegate { mode = value; for (int j = 0; j < options.Count; j++) { options[j].Selected = j == mode; options[j].Invalidate(); } }, false); option.Selected = mode == i;
                Place(card, option, 20, 20 + i * 72, 380, 40); options.Add(option); Place(card, Caption(subtitles[i], 9, Design.Muted), 34, 64 + i * 72, 650, 23);
            }
            Place(card, Caption(L10n.T("Адреса и домены"), 11, Design.Ink), 22, 248, 500, 28);
            var rules = Input(settings.RouteEntries); rules.Multiline = true; rules.ScrollBars = ScrollBars.Vertical; rules.Font = new Font("Consolas", 10); Place(card, rules, 22, 285, 680, 170); rules.Anchor |= AnchorStyles.Right;
            Place(card, Caption(L10n.T("По одному на строку: example.org, *.example.org, 192.168.0.0/16 или URL"), 9, Design.Muted), 22, 467, 700, 27);
            Place(card, Button(L10n.T("Сохранить правила"), "check", delegate { Guard(delegate { Configuration.Rules(rules.Text, "proxy"); settings.RouteMode = mode; settings.RouteEntries = rules.Text; Save(); Notify(runtime.Running ? L10n.T("Правила сохранены. Переподключитесь для применения.") : L10n.T("Правила сохранены."), false); }); }, true), 22, 514, 225, 42);
            rules.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            card.SizeChanged += delegate { rules.Width = Math.Max(300, card.ClientSize.Width - 44); };
            Place(card, Button(L10n.T("Посмотреть правила подписки"), "route", delegate { ShowProviderRoutes(); }, false), 265, 514, 330, 42); host.Controls.Add(card); content.Controls.Add(host); content.Controls.Add(header);
        }
        void ShowLogs()
        {
            var header = Header(L10n.T("Журнал"), L10n.T("События текущего сеанса. Ключи и ссылки подписок не записываются."));
            var clear = Button(L10n.T("Очистить"), "", delegate { logs.Clear(); }, false); Place(header, clear, header.Width - 142, 4, 130, 40); clear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = logs.BackColor }; logs.Dock = DockStyle.Fill; card.Controls.Add(logs); content.Controls.Add(card); content.Controls.Add(header);
        }
        void Browse(TextBox field) { using (var dialog = new OpenFileDialog { Filter = L10n.T("Исполняемые файлы|*.exe"), CheckFileExists = true }) if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.FileName; }
    }
}
