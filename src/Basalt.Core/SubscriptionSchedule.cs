using System;
using System.Globalization;

namespace Basalt.Core
{
    public static class SubscriptionSchedule
    {
        public static int Hours(Subscription subscription) { return subscription.UpdateIntervalHours > 0 ? Math.Min(720, subscription.UpdateIntervalHours) : subscription.UpdateHours > 0 ? Math.Min(720, subscription.UpdateHours) : 12; }
        static DateTime Read(string value) { DateTime date; return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) ? date.ToUniversalTime() : DateTime.MinValue; }
        public static DateTime Next(Subscription subscription)
        {
            if (!subscription.AutoUpdate) return DateTime.MaxValue;
            DateTime last = Read(subscription.UpdateFailures > 0 ? subscription.LastAttemptUtc : subscription.Updated);
            if (last == DateTime.MinValue) return last;
            double minutes = subscription.UpdateFailures > 0 ? Math.Min(360, 5 * Math.Pow(2, Math.Min(7, subscription.UpdateFailures - 1))) : Hours(subscription) * 60;
            return last > DateTime.MaxValue.AddMinutes(-minutes) ? DateTime.MaxValue : last.AddMinutes(minutes);
        }
        public static bool Due(Subscription subscription, DateTime utcNow) { return subscription.AutoUpdate && Next(subscription) <= utcNow; }
        public static void Success(Subscription subscription, DateTime utcNow) { subscription.Updated = utcNow.ToString("o"); subscription.LastAttemptUtc = subscription.Updated; subscription.UpdateFailures = 0; subscription.LastUpdateError = null; }
        public static void Failure(Subscription subscription, DateTime utcNow) { subscription.LastAttemptUtc = utcNow.ToString("o"); subscription.UpdateFailures = Math.Min(20, subscription.UpdateFailures + 1); subscription.LastUpdateError = L10n.T("Обновление не удалось; предыдущий список сохранён"); }
    }
}
