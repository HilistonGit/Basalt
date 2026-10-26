using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Basalt.Core
{
    public sealed class ImportResult
    {
        public readonly List<Profile> Profiles = new List<Profile>();
        public readonly List<string> Errors = new List<string>();
        public SubscriptionMetadata Metadata;
        public string Format;
        public string Summary()
        {
            var counts = new SortedDictionary<string, int>();
            foreach (var profile in Profiles) { if (!counts.ContainsKey(profile.Protocol)) counts[profile.Protocol] = 0; counts[profile.Protocol]++; }
            var parts = new List<string>(); foreach (var pair in counts) parts.Add(pair.Key + ": " + pair.Value);
            return Format + L10n.T("; серверов: ") + Profiles.Count + "; " + String.Join(", ", parts.ToArray()) + L10n.T("; ошибок: ") + Errors.Count;
        }
    }
    public sealed class SubscriptionDownload { public string Body; public SubscriptionMetadata Metadata; public string Routing; }
    public sealed class SubscriptionMetadata
    {
        public long? Upload, Download, Total, Expire;
        public int UpdateHours;
        public string SupportUrl;
        public static SubscriptionMetadata Read(string usage, string interval, string support)
        {
            var result = new SubscriptionMetadata();
            foreach (var part in (usage ?? "").Split(';'))
            {
                var pair = part.Split('='); long value;
                if (pair.Length != 2 || !Int64.TryParse(pair[1].Trim(), out value) || value < 0) continue;
                switch (pair[0].Trim().ToLowerInvariant()) { case "upload": result.Upload = value; break; case "download": result.Download = value; break; case "total": result.Total = value; break; case "expire": if (value <= 253402300799L) result.Expire = value; break; }
            }
            int hours; if (Int32.TryParse(interval, out hours) && hours > 0 && hours <= 720) result.UpdateHours = hours;
            Uri uri; if (Uri.TryCreate(support, UriKind.Absolute, out uri) && uri.Scheme == "https") result.SupportUrl = support;
            return result;
        }
        public void Apply(Subscription target) { target.UploadBytes = Upload; target.DownloadBytes = Download; target.TotalBytes = Total; target.ExpireUnix = Expire; target.UpdateHours = UpdateHours; target.SupportUrl = SupportUrl; }
    }
    public static class Subscriptions
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public static ImportResult Parse(SubscriptionDownload response) { var result = Parse(response.Body); result.Metadata = response.Metadata; if (!String.IsNullOrEmpty(response.Routing)) ProviderRouting.AttachHapp(result, response.Routing); return result; }
        static string Decode(string value)
        {
            value = value.Trim().Replace('-', '+').Replace('_', '/');
            return Encoding.UTF8.GetString(Convert.FromBase64String(value.PadRight((value.Length + 3) / 4 * 4, '=')));
        }
        public static ImportResult Parse(string text)
        {
            if (text == null || text.Length > MaxBytes) throw new FormatException(L10n.T("Подписка пуста или превышает 2 МБ."));
            text = text.Trim().TrimStart('\uFEFF');
            if (!text.Contains("://")) { try { text = Decode(text); } catch (FormatException) { } }
            if (text.StartsWith("{") || text.StartsWith("[") || System.Text.RegularExpressions.Regex.IsMatch(text, @"(?m)^\s*[""']?proxies[""']?\s*:")) return StructuredSubscriptions.Parse(text);
            var result = new ImportResult { Format = L10n.T("Share-ссылки (plain text / base64)") }; var seen = new HashSet<string>(); var routing = new List<string>();
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("#")) continue;
                if (lines[i].Trim().StartsWith("happ://routing/", StringComparison.OrdinalIgnoreCase)) { routing.Add(lines[i].Trim()); continue; }
                try { var p = ParseUri(lines[i].Trim()); if (seen.Add(p.Identity)) result.Profiles.Add(p); }
                catch (Exception e) { if (!(e is FormatException || e is ArgumentException)) throw; result.Errors.Add(L10n.T("Строка ") + (i + 1) + ": " + e.Message); }
            }
            foreach (var link in routing) ProviderRouting.AttachHapp(result, link);
            return result;
        }
        public static Profile ParseUri(string raw)
        {
            if (raw.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
            {
                int at = raw.LastIndexOf('@');
                if (at < 0) throw new FormatException(L10n.T("Shadowsocks: нужен SIP002 URI с адресом после @."));
                string auth = raw.Substring(5, at - 5);
                if (!auth.Contains(":")) auth = Uri.EscapeDataString(Decode(auth));
                raw = "ss://" + auth + raw.Substring(at);
            }
            Uri u;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out u)) throw new FormatException(L10n.T("Некорректная share-ссылка."));
            string protocol = u.Scheme == "hy2" ? "hysteria2" : u.Scheme;
            if (protocol != "vless" && protocol != "trojan" && protocol != "hysteria2" && protocol != "ss")
                throw new FormatException(L10n.T("Поддерживаются vless, trojan, ss и hysteria2. JSON/Clash/VMess пока не импортируются."));
            var p = new Profile { Protocol = protocol, Server = u.DnsSafeHost.Trim('[', ']'), Port = u.Port < 0 ? 443 : u.Port, Secret = Uri.UnescapeDataString(u.UserInfo), Name = Uri.UnescapeDataString(u.Fragment.TrimStart('#')) };
            if (p.Port < 1 || p.Port > 65535 || String.IsNullOrWhiteSpace(p.Server) || String.IsNullOrEmpty(p.Secret)) throw new FormatException(L10n.T("Не указан адрес, порт или ключ сервера."));
            if (String.IsNullOrEmpty(p.Name)) p.Name = p.Server;
            Guid uuid;
            if (protocol == "vless" && !Guid.TryParse(p.Secret, out uuid)) throw new FormatException(L10n.T("VLESS: некорректный UUID."));
            foreach (string pair in u.Query.TrimStart('?').Split('&'))
            {
                if (pair.Length == 0) continue;
                var parts = pair.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(parts[0]);
                if (p.Options.ContainsKey(key)) throw new FormatException(L10n.T("Повторяющийся параметр: ") + key);
                p.Options[key] = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : "";
            }
            // Compile during import so unsupported transports/options never silently change semantics.
            Configuration.Outbound(p);
            return p;
        }
        public static SubscriptionDownload Fetch(Subscription subscription)
        { return Fetch(subscription, null); }
        public static Dictionary<string, string> DeviceHeaders(Subscription subscription, string deviceId, bool is64Bit)
        {
            string agent = (subscription.UserAgent ?? "").Trim();
            bool happ = agent.Equals("Happ", StringComparison.OrdinalIgnoreCase) || agent.StartsWith("Happ/", StringComparison.OrdinalIgnoreCase);
            bool send = subscription.HwidMode == "always" || ((subscription.HwidMode == null || subscription.HwidMode == "auto") && happ);
            var result = new Dictionary<string, string>();
            if (!send) return result;
            if (String.IsNullOrWhiteSpace(deviceId) || deviceId.Contains("\r") || deviceId.Contains("\n")) throw new ArgumentException(L10n.T("Не задан корректный идентификатор установки для HWID."));
            result["X-Hwid"] = deviceId; result["X-Device-Os"] = "windows"; result["X-Device-Model"] = "Basalt " + (is64Bit ? "amd64" : "386");
            return result;
        }
        public static SubscriptionDownload Fetch(Subscription subscription, string deviceId)
        {
            Uri url;
            if (!Uri.TryCreate(subscription.Url, UriKind.Absolute, out url) || url.Scheme != "https" || url.UserInfo.Length != 0)
                throw new ArgumentException(L10n.T("Для подписки нужен HTTPS URL без userinfo."));
            var headers = DeviceHeaders(subscription, deviceId, Environment.Is64BitOperatingSystem);
            for (int redirect = 0; redirect < 6; redirect++)
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = false; request.Timeout = 20000; request.ReadWriteTimeout = 20000;
                request.UserAgent = String.IsNullOrWhiteSpace(subscription.UserAgent) ? "Basalt/0.6.7" : subscription.UserAgent; request.Proxy = null;
                request.Accept = "*/*"; foreach (var header in headers) request.Headers[header.Key] = header.Value;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                    {
                        var next = new Uri(url, response.Headers["Location"]);
                        if (next.Scheme != "https" || next.UserInfo.Length != 0) throw new IOException(L10n.T("Небезопасное перенаправление подписки."));
                        if (headers.Count != 0 && !String.Equals(next.Authority, url.Authority, StringComparison.OrdinalIgnoreCase)) throw new IOException(L10n.T("Подписка перенаправляет на другой сервер. HWID не передан: укажите конечную доверенную ссылку явно."));
                        url = next; continue;
                    }
                    using (var stream = response.GetResponseStream()) using (var output = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192]; int count;
                        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                        { if (output.Length + count > MaxBytes) throw new IOException(L10n.T("Подписка превышает 2 МБ.")); output.Write(buffer, 0, count); }
                        return new SubscriptionDownload { Body = Encoding.UTF8.GetString(output.ToArray()), Routing = response.Headers["routing"], Metadata = SubscriptionMetadata.Read(response.Headers["subscription-userinfo"], response.Headers["profile-update-interval"], response.Headers["support-url"]) };
                    }
                }
            }
            throw new IOException(L10n.T("Слишком много перенаправлений."));
        }
        public static void Merge(Settings settings, ImportResult result, string subscriptionId)
        {
            bool updating = subscriptionId != null && settings.Subscriptions.Exists(delegate(Subscription item) { return item.Id == subscriptionId; });
            if (result.Profiles.Count == 0 || (subscriptionId != null && result.Errors.Count != 0)) throw new SubscriptionImportException(result, updating);
            var selectedBefore=settings.Profiles.Find(delegate(Profile p){return p.Id==settings.SelectedId;});
            var old = new Dictionary<string, Profile>();
            foreach (var p in settings.Profiles) if (p.SubscriptionId == subscriptionId) old[p.Identity] = p;
            foreach (var p in result.Profiles)
            { Profile previous; if (old.TryGetValue(p.Identity, out previous)) { p.Id = previous.Id; p.Favorite = previous.Favorite; } p.SubscriptionId = subscriptionId; }
            if (subscriptionId != null) settings.Profiles.RemoveAll(delegate(Profile p) { return p.SubscriptionId == subscriptionId; });
            foreach (var p in result.Profiles) if (!settings.Profiles.Exists(delegate(Profile x) { return x.Id == p.Id; })) settings.Profiles.Add(p);
            ProviderRouting.StoreDocuments(settings,result);
            if (!settings.Profiles.Exists(delegate(Profile p) { return p.Id == settings.SelectedId; })) {var replacement=selectedBefore==null?null:settings.Profiles.Find(delegate(Profile p){return p.SubscriptionId==selectedBefore.SubscriptionId && p.Name==selectedBefore.Name && p.Protocol==selectedBefore.Protocol;});settings.SelectedId=(replacement??settings.Profiles[0]).Id;}
            var subscription = settings.Subscriptions.Find(delegate(Subscription s) { return s.Id == subscriptionId; });
            if (subscription != null && result.Metadata != null) result.Metadata.Apply(subscription);
        }
    }
}
