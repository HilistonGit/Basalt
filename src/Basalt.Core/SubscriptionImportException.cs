using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Basalt.Core
{
    public sealed class SubscriptionImportException : FormatException
    {
        public readonly string Report;
        public SubscriptionImportException(ImportResult result, bool updating)
            : base(BuildReport(result, updating)) { Report = Message; }
        static string BuildReport(ImportResult result, bool updating)
        {
            var report = new StringBuilder();
            report.AppendLine(updating ? L10n.T("Подписка не обновлена. Предыдущие серверы сохранены.") : L10n.T("Подписка не добавлена."));
            report.AppendLine(L10n.T("Формат ответа: ") + (String.IsNullOrEmpty(result.Format) ? L10n.T("не определён") : result.Format));
            report.AppendLine(L10n.T("Распознано серверов: ") + result.Profiles.Count + L10n.T(". Ошибок: ") + result.Errors.Count + ".");
            report.AppendLine();
            for (int i = 0; i < Math.Min(20, result.Errors.Count); i++) report.AppendLine(SafeDetail(result.Errors[i]));
            if (result.Errors.Count > 20) report.AppendLine(L10n.T("Остальные ошибки: ") + (result.Errors.Count - 20) + ".");
            if (result.Errors.Count == 0) report.AppendLine(L10n.T("Ответ не содержит поддерживаемых серверов. Проверьте формат, User-Agent и требования HWID у провайдера."));
            report.AppendLine(); report.Append(L10n.T("Импорт остановлен, чтобы не пропустить серверы незаметно."));
            return report.ToString();
        }
        static string SafeDetail(string value)
        {
            value = Regex.Replace(value ?? "", @"[a-zA-Z][a-zA-Z0-9+.-]*://\S+", L10n.T("[ссылка скрыта]"));
            value = Regex.Replace(value, @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", L10n.T("[UUID скрыт]"));
            value = value.Replace('\r', ' ').Replace('\n', ' ');
            return value.Length > 240 ? value.Substring(0, 240) + "…" : value;
        }
    }
}
