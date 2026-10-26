using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    public sealed partial class MainForm
    {
        static ComboBox UserAgents(string current)
        {
            var field = Choice("Basalt/0.6.7", "Happ/1.0", "v2rayNG/1.9.0", "sing-box/1.13.0", "clash-verge/1.6.0");
            field.DropDownStyle = ComboBoxStyle.DropDown; field.Text = current ?? "Basalt/0.6.7"; return field;
        }
        static ComboBox HwidChoice(string mode) { var box = Choice(L10n.T("Авто · для Happ"), L10n.T("Передавать"), L10n.T("Не передавать")); box.SelectedIndex = mode == "always" ? 1 : mode == "never" ? 2 : 0; return box; }
        static string HwidValue(ComboBox box) { return box.SelectedIndex == 1 ? "always" : box.SelectedIndex == 2 ? "never" : "auto"; }
        void ShowImportError(SubscriptionImportException error)
        {
            Notify(L10n.T("Подписка не импортирована. Подробности — в отчёте об ошибке."), true);
            using (var dialog = Dialog(L10n.T("Не удалось импортировать подписку"), 462))
            {
                Place(dialog, Caption(L10n.T("Причина отказа импорта"), 18, Design.Ink), 24, 20, 610, 36);
                var report = Input(error.Report); report.Multiline = true; report.ReadOnly = true; report.ScrollBars = ScrollBars.Vertical; report.Font = Design.Font(10, false); Place(dialog, report, 24, 72, 612, 300);
                Place(dialog, Button(L10n.T("Копировать отчёт"), "", delegate { Guard(delegate { Clipboard.SetText(error.Report); }); }, false), 24, 396, 215, 42);
                var close = Button(L10n.T("Закрыть"), "", delegate { dialog.Close(); }, true); Place(dialog, close, 476, 396, 160, 42); dialog.CancelButton = close; dialog.AcceptButton = close;
                dialog.ShowDialog(this);
            }
        }
        bool importOpen;
        void ImportClipboard()
        {
            Guard(delegate { if (!Clipboard.ContainsText()) { Notify(L10n.T("В буфере обмена нет текста. Скопируйте ссылку на подписку или сервер."), true); return; } ShowImport(Clipboard.GetText()); });
        }
        void ImportFile()
        {
            using (var dialog = new OpenFileDialog { Filter = L10n.T("Текстовые подписки|*.txt;*.conf;*.list;*.json;*.yaml;*.yml|Все файлы|*.*"), CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) Guard(delegate { if (new FileInfo(dialog.FileName).Length > Subscriptions.MaxBytes) throw new IOException(L10n.T("Файл превышает 2 МБ.")); ShowImport(File.ReadAllText(dialog.FileName)); });
        }
        Form Dialog(string title, int height)
        {
            return new Form { Text = title, ClientSize = new Size(660, height), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, BackColor = Design.Background, ForeColor = Design.Ink, Font = Font, ShowInTaskbar = false };
        }
        void ShowImport(string initial)
        {
            if (busy || importOpen) return;
            importOpen = true;
            try { Guard(delegate { ImportDialogCore(initial); }); } finally { importOpen = false; }
        }
        void ImportDialogCore(string initial)
        {
            if (busy) return;
            string text = initial.Trim(), title = "", agent = settings.SubscriptionUserAgent, hwidMode = settings.SubscriptionHwidMode;
            Uri url;
            if (Uri.TryCreate(text, UriKind.Absolute, out url) && (url.Scheme == "https" || url.Scheme == "http"))
            {
                if (url.Scheme != "https" || url.UserInfo != "") { Notify(L10n.T("Подписка должна использовать HTTPS без userinfo."), true); return; }
                var subscription = new Subscription { Url = text, Name = title.Length == 0 ? url.Host : title, HwidMode = hwidMode, UserAgent = agent.Length == 0 ? "Basalt/0.6.7" : agent };
                var existing = settings.Subscriptions.Find(delegate(Subscription item) { return item.Url == text; }); if (existing != null) { UpdateSubscription(existing); return; }
                Work(delegate { return FetchValidated(subscription, settings.DeviceId); }, delegate(object value)
                {
                    Subscriptions.Merge(settings, (ImportResult)value, subscription.Id); if (((ImportResult)value).Metadata != null) ((ImportResult)value).Metadata.Apply(subscription); SubscriptionSchedule.Success(subscription, DateTime.UtcNow); settings.Subscriptions.Add(subscription); Save(); group = null; favorites = false; BuildNavigation(); ShowPage("servers"); Notify(L10n.T("Подписка добавлена: ") + ((ImportResult)value).Profiles.Count + L10n.T(" серверов."), false);
                }, L10n.T("Загружаем подписку…")); return;
            }
            Work(delegate { return ValidateImported(Subscriptions.Parse(text)); }, delegate(object value)
            {
                var result = (ImportResult)value; Subscriptions.Merge(settings, result, null); Save(); group = "manual"; favorites = false; BuildNavigation(); ShowPage("servers");
                Notify(L10n.T("Импортировано серверов: ") + result.Profiles.Count + (result.Errors.Count == 0 ? "." : L10n.T(". Пропущено строк: ") + result.Errors.Count), result.Errors.Count > 0);
                if (result.Errors.Count > 0) MessageBox.Show(this, String.Join(Environment.NewLine, result.Errors.ToArray()), L10n.T("Не все строки удалось импортировать"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, L10n.T("Проверяем импорт и маршрутизацию…"));
        }
        void UpdateSubscription(Subscription subscription)
        {
            Work(delegate { return FetchValidated(subscription, settings.DeviceId); }, delegate(object value)
            {
                Subscriptions.Merge(settings, (ImportResult)value, subscription.Id); SubscriptionSchedule.Success(subscription, DateTime.UtcNow); Save(); ShowPage(page); Notify(L10n.T("Подписка «") + subscription.Name + L10n.T("» обновлена."), false);
            }, L10n.T("Обновляем подписку «") + subscription.Name + "»…");
        }
        void DeleteSubscription(Subscription subscription)
        {
            if (runtime.Running) { Notify(L10n.T("Отключитесь перед удалением подписки."), true); return; }
            if (MessageBox.Show(this, L10n.T("Удалить подписку «") + subscription.Name + L10n.T("» и её серверы?"), "Basalt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Guard(delegate { settings.Subscriptions.Remove(subscription); settings.Profiles.RemoveAll(delegate(Profile p) { return p.SubscriptionId == subscription.Id; }); if (SelectedProfile == null) settings.SelectedId = settings.Profiles.Count == 0 ? null : settings.Profiles[0].Id; Save(); BuildNavigation(); ShowPage("subscriptions"); Notify(L10n.T("Подписка удалена."), false); });
        }
        void EditSubscription(Subscription subscription)
        {
            using (var dialog = Dialog(L10n.T("Изменить подписку"), 370))
            {
                Place(dialog, Caption(L10n.T("Название"), 10, Design.Ink), 28, 24, 600, 24); var name = Input(subscription.Name); Place(dialog, name, 28, 56, 604, 32);
                Place(dialog, Caption(L10n.T("HTTPS-ссылка"), 10, Design.Ink), 28, 108, 600, 24); var url = Input(subscription.Url); Place(dialog, url, 28, 140, 604, 32);
                var auto = new ToggleSwitch { Text = L10n.T("Обновлять автоматически"), Checked = subscription.AutoUpdate, ForeColor = Design.Ink }; Place(dialog, auto, 28, 204, 270, 30);
                var interval = Number(subscription.UpdateIntervalHours, 0, 720); Place(dialog, interval, 430, 204, 202, 30);
                Place(dialog, Caption(L10n.T("Интервал, ч"), 9, Design.Muted), 310, 204, 110, 26);
                Place(dialog, Caption(L10n.T("0 — интервал провайдера; если не указан, каждые 12 часов"), 9, Design.Muted), 28, 248, 604, 27);
                var cancel = Button(L10n.T("Отмена"), "", delegate { dialog.DialogResult = DialogResult.Cancel; }, false); Place(dialog, cancel, 308, 299, 140, 42); dialog.CancelButton = cancel;
                var apply = Button(L10n.T("Сохранить"), "check", delegate
                {
                    Uri parsed; if (String.IsNullOrWhiteSpace(name.Text) || !Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out parsed) || parsed.Scheme != "https" || parsed.UserInfo != "") { MessageBox.Show(dialog, L10n.T("Укажите название и корректную HTTPS-ссылку.")); return; }
                    dialog.DialogResult = DialogResult.OK;
                }, true); Place(dialog, apply, 462, 299, 170, 42); dialog.AcceptButton = apply;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Guard(delegate { subscription.Name = name.Text.Trim(); subscription.Url = url.Text.Trim(); subscription.AutoUpdate = auto.Checked; subscription.UpdateIntervalHours = (int)interval.Value; subscription.UpdateFailures = 0; subscription.LastUpdateError = null; Save(); BuildNavigation(); ShowPage("subscriptions"); Notify(L10n.T("Подписка сохранена. Нажмите «Обновить», чтобы получить серверы."), false); });
            }
        }
        string TextPrompt(string title, string initial)
        {
            using (var dialog = Dialog(title, 190))
            {
                Place(dialog, Caption(title, 14, Design.Ink), 28, 22, 604, 35); var input = Input(initial); Place(dialog, input, 28, 68, 604, 32);
                var cancel = Button(L10n.T("Отмена"), "", delegate { dialog.DialogResult = DialogResult.Cancel; }, false); Place(dialog, cancel, 308, 123, 140, 42); dialog.CancelButton = cancel;
                var apply = Button(L10n.T("Сохранить"), "check", delegate { dialog.DialogResult = DialogResult.OK; }, true); Place(dialog, apply, 462, 123, 170, 42); dialog.AcceptButton = apply;
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }
        public void RenderPreview(string path, string screen)
        {
            if (!rendering) throw new InvalidOperationException("Preview requires isolated mode.");
            if (screen != "empty")
            {
                var subscription = new Subscription { Id = "demo", Name = L10n.T("Основная подписка"), Url = "https://example.invalid/subscription", Updated = DateTime.UtcNow.ToString("o") }; settings.Subscriptions.Add(subscription);
                string[] names = { L10n.T("Нидерланды · Amsterdam"), L10n.T("Германия · Frankfurt"), L10n.T("Финляндия · Helsinki"), L10n.T("Франция · Paris"), L10n.T("Швеция · Stockholm") };
                for (int i = 0; i < names.Length; i++) { var p = Subscriptions.ParseUri("trojan://demo@server-" + i + ".example.invalid:443#" + Uri.EscapeDataString(names[i])); p.SubscriptionId = "demo"; p.Favorite = i == 0 || i == 2; settings.Profiles.Add(p); }
                settings.SelectedId = settings.Profiles[0].Id;
            }
            BuildNavigation(); ShowPage(screen == "settings" || screen == "routing" || screen == "subscriptions" ? screen : "servers");
            Notify(screen == "empty" ? L10n.T("Готов к работе. Добавьте подписку или сервер.") : L10n.T("Демонстрационные профили для проверки интерфейса. Подключение не выполнялось."), false);
            Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.GetFullPath(path)); }
        }
    }
}
