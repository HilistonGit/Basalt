using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Basalt.Core;

namespace Basalt.Windows
{
    public sealed partial class MainForm
    {
        bool autoUpdating; DateTime nextSubscriptionScan = DateTime.UtcNow.AddSeconds(15);
        ImportResult FetchValidated(Subscription subscription, string deviceId)
        {
            subscription = Json.Serializer().Deserialize<Subscription>(Json.Write(subscription));
            subscription.UserAgent = settings.SubscriptionUserAgent; subscription.HwidMode = settings.SubscriptionHwidMode;
            var result = Subscriptions.Parse(Subscriptions.Fetch(subscription, deviceId));
            Log(L10n.T("Ответ подписки: ") + result.Summary());
            return ValidateImported(result);
        }
        ImportResult ValidateImported(ImportResult result)
        {
            if (result.Errors.Count != 0 || result.Profiles.Count == 0) return result;
            CoreCatalog.Verify(); RoutingAssets.Prepare(result, dataDirectory); var checkSettings = new Settings(); CoreCatalog.Configure(checkSettings);
            string folder = Path.Combine(dataDirectory, "validation-" + Guid.NewGuid().ToString("N"));
            try { using (var checker = new CoreRuntime(folder)) foreach (var profile in result.Profiles) checker.Validate(profile, checkSettings); }
            finally { if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder); }
            return result;
        }
        void RefreshDueSubscription()
        {
            if (rendering || quitting || busy || autoUpdating || importOpen || (Form.ActiveForm != null && Form.ActiveForm != this) || DateTime.UtcNow < nextSubscriptionScan) return;
            nextSubscriptionScan = DateTime.UtcNow.AddSeconds(30);
            var subscription = settings.Subscriptions.Find(delegate(Subscription s) { return SubscriptionSchedule.Due(s, DateTime.UtcNow); });
            if (subscription == null) return;
            var snapshot = Json.Serializer().Deserialize<Subscription>(Json.Write(subscription)); string deviceId = settings.DeviceId;
            autoUpdating = true; activity.Active = true; notice.Text = L10n.T("Обновляем подписку…"); var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = FetchValidated(snapshot, deviceId); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                autoUpdating = false; activity.Active = busy; worker.Dispose(); if (IsDisposed || quitting) return;
                // Discard stale responses after a manual edit, refresh, or deletion.
                if (busy || !settings.Subscriptions.Contains(subscription) || subscription.Url != snapshot.Url || subscription.UserAgent != snapshot.UserAgent || subscription.HwidMode != snapshot.HwidMode || subscription.Updated != snapshot.Updated || !subscription.AutoUpdate) return;
                try
                {
                    if (e.Error != null) throw e.Error;
                    Subscriptions.Merge(settings, (ImportResult)e.Result, subscription.Id);
                    SubscriptionSchedule.Success(subscription, DateTime.UtcNow); Save();
                    if (page == "servers") RefreshServers();
                    Notify(L10n.T("Подписка «") + subscription.Name + L10n.T("» обновлена автоматически."), false);
                }
                catch (Exception)
                {
                    SubscriptionSchedule.Failure(subscription, DateTime.UtcNow);
                    try { Save(); } catch (Exception) { Log(L10n.T("Не удалось сохранить состояние автообновления.")); }
                    Log(L10n.T("Подписка «") + subscription.Name + L10n.T("»: автообновление не удалось, список сохранён. Повтор после ") + SubscriptionSchedule.Next(subscription).ToLocalTime().ToString("HH:mm") + ".");
                    if (page == "servers") RefreshServers();
                }
            };
            worker.RunWorkerAsync();
        }
    }
}
