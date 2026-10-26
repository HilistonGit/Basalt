using System;
using System.Collections.Generic;

namespace Basalt.Core
{
    // Preserve the native dialect for the current official legacy Xray build.
    static class XrayHysteria
    {
        static Dictionary<string, object> Map(Dictionary<string, object> parent, string key)
        {
            object value; Dictionary<string, object> map;
            if (!parent.TryGetValue(key, out value) || (map = value as Dictionary<string, object>) == null)
                throw new FormatException(L10n.T("Hysteria: требуется объект ") + key);
            return map;
        }
        static string Text(Dictionary<string, object> map, string key, string fallback)
        { object value; return map.TryGetValue(key, out value) ? Convert.ToString(value) : fallback; }
        static void Keys(Dictionary<string, object> map, params string[] allowed)
        {
            var keys = new HashSet<string>(allowed);
            foreach (string key in map.Keys) if (!keys.Contains(key)) throw new FormatException(L10n.T("Hysteria: преобразование параметра ") + key + L10n.T(" пока не поддерживается."));
        }
        public static Profile Parse(Dictionary<string, object> outbound, string name)
        {
            if (outbound.ContainsKey("proxySettings")) throw new FormatException(L10n.T("Hysteria: цепочки proxySettings пока не поддерживаются."));
            var settings = Map(outbound, "settings"); Keys(settings, "version", "address", "port");
            var stream = Map(outbound, "streamSettings");
            if (stream.ContainsKey("sockopt") && Map(stream, "sockopt").ContainsKey("dialerProxy")) throw new FormatException(L10n.T("Hysteria: dialerProxy пока не поддерживается."));
            var transport = Map(stream, "hysteriaSettings");
            if (Text(settings, "version", "") != "2" || Text(transport, "version", "") != "2" || Text(stream, "network", "") != "hysteria" || Text(stream, "security", "") != "tls")
                throw new FormatException(L10n.T("Поддерживается Xray Hysteria версии 2 с транспортом hysteria и TLS."));
            var sourceTls = Map(stream, "tlsSettings");
            if (sourceTls.ContainsKey("certificates")) throw new FormatException(L10n.T("Hysteria: внешние сертификаты пока не поддерживаются."));
            if (!Text(sourceTls, "allowInsecure", "false").Equals("false", StringComparison.OrdinalIgnoreCase)) throw new FormatException(L10n.T("Hysteria: отключение проверки TLS не поддерживается."));
            int port; string address = Text(settings, "address", ""), auth = Text(transport, "auth", "");
            if (Uri.CheckHostName(address) == UriHostNameType.Unknown || !Int32.TryParse(Text(settings, "port", ""), out port) || port < 1 || port > 65535)
                throw new FormatException(L10n.T("Hysteria: некорректный адрес или порт."));
            if (String.IsNullOrEmpty(Text(sourceTls, "serverName", ""))) sourceTls["serverName"] = address;
            var profile = new Profile { Name = name, Protocol = "hysteria2", Server = address, Port = port, Secret = auth, XrayOutbound = Json.Write(outbound) };
            profile.Options["type"] = "hysteria";
            return profile;
        }
    }
}
