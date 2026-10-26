using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Basalt.Core
{
    public static class StructuredSubscriptions
    {
        static Dictionary<string, object> Map(object value) { var result = value as Dictionary<string, object>; if (result == null) throw new FormatException(L10n.T("Ожидался объект конфигурации.")); return result; }
        static string S(Dictionary<string, object> map, string key, string fallback) { object value; return map.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback; }
        static bool B(Dictionary<string, object> map, string key) { string value = S(map, key, "false"); bool result; if (!Boolean.TryParse(value, out result)) throw new FormatException(L10n.T("Ожидалось true/false: ") + key); return result; }
        static int N(Dictionary<string, object> map, string key, int fallback) { int value; if (!map.ContainsKey(key)) return fallback; if (!Int32.TryParse(S(map, key, ""), out value)) throw new FormatException(L10n.T("Некорректное число: ") + key); return value; }
        static Dictionary<string, object> O(params object[] pairs) { return Json.Obj(pairs); }
        static void Optional(Dictionary<string, object> from, string key, Dictionary<string, object> to, string target) { object value; if (from.TryGetValue(key, out value)) to[target] = value; }
        public static ImportResult Parse(string text)
        {
            object root = text.StartsWith("{") || text.StartsWith("[") ? Json.Serializer().DeserializeObject(text) : BoundedYaml.Read(text);
            var documents = root as IList ?? new object[] { root }; var result = new ImportResult(); var seen = new HashSet<string>();
            foreach (var document in documents)
            {
                var config = Map(document); object entries; bool clash = config.TryGetValue("proxies", out entries);
                if (!clash && !config.TryGetValue("outbounds", out entries)) throw new FormatException(L10n.T("Не найден список proxies/outbounds. Внешние proxy-providers не загружаются автоматически."));
                var list = entries as IList; if (list == null) throw new FormatException(L10n.T("proxies/outbounds должен быть списком."));
                result.Format = clash ? "Clash YAML / JSON" : "sing-box JSON";
                bool xray = false; foreach (var item in list) if (Map(item).ContainsKey("protocol")) xray = true;
                if (xray) { result.Format = "Xray JSON"; var parsed = XraySubscriptions.Parse(Json.Write(config)); result.Errors.AddRange(parsed.Errors); foreach (var profile in parsed.Profiles) if (seen.Add(profile.Identity)) result.Profiles.Add(profile); continue; }
                int index = 0;
                foreach (var entry in list)
                {
                    index++;
                    try
                    {
                        var outbound = Map(Json.Serializer().DeserializeObject(Json.Write(entry))); string type = S(outbound, "type", "");
                        if (!clash && (type == "selector" || type == "urltest" || type == "direct" || type == "block" || type == "dns" || type == "reject")) continue;
                        if (clash) outbound = Clash(outbound);
                        string sourceTag = S(outbound, "tag", "");
                        var profile = Native(outbound); ProviderRouting.Attach(profile, config, clash ? "clash" : "sing-box", sourceTag); if (seen.Add(profile.Identity)) result.Profiles.Add(profile);
                    }
                    catch (Exception e) { if (!(e is FormatException || e is ArgumentException || e is KeyNotFoundException)) throw; result.Errors.Add((clash ? "Clash" : "sing-box") + " #" + index + ": " + e.Message); }
                }
                if (config.ContainsKey("endpoints")) result.Errors.Add(L10n.T("sing-box endpoints пока не поддерживаются."));
            }
            return result;
        }
        static Profile Native(Dictionary<string, object> outbound)
        {
            string type = S(outbound, "type", "");
            if (Array.IndexOf(new[] { "vless", "vmess", "trojan", "shadowsocks", "hysteria2", "tuic", "socks", "http" }, type) < 0) throw new FormatException(L10n.T("Неподдерживаемый протокол ") + type);
            foreach (string key in new[] { "detour", "domain_resolver", "bind_interface", "inet4_bind_address", "inet6_bind_address", "routing_mark", "plugin", "plugin_opts" }) if (outbound.ContainsKey(key)) throw new FormatException(L10n.T("Зависимый или системный параметр: ") + key);
            string server = S(outbound, "server", ""); int port = N(outbound, "server_port", 0);
            if (Uri.CheckHostName(server) == UriHostNameType.Unknown || port < 1 || port > 65535) throw new FormatException(L10n.T("Некорректный адрес/порт."));
            string secret = S(outbound, "password", ""); Guid uuid;
            if (type == "vless" || type == "vmess" || type == "tuic") { secret = S(outbound, "uuid", ""); if (!Guid.TryParse(secret, out uuid)) throw new FormatException(L10n.T("Некорректный UUID.")); }
            if (type != "http" && type != "socks" && String.IsNullOrEmpty(secret)) throw new FormatException(L10n.T("Не задан ключ сервера."));
            object tlsObject;
            if (outbound.TryGetValue("tls", out tlsObject))
            {
                var tls = Map(tlsObject); if (B(tls, "insecure")) throw new FormatException(L10n.T("Отключение проверки TLS не поддерживается."));
                foreach (string key in new[] { "certificate_path", "client_certificate_path", "client_key_path", "ech" }) if (tls.ContainsKey(key)) throw new FormatException(L10n.T("Параметр TLS пока не поддерживается: ") + key);
                // Pin the original name before replacing the endpoint with an IP in TUN mode.
                if (B(tls, "enabled") && !tls.ContainsKey("server_name")) tls["server_name"] = server;
            }
            var profile = new Profile { Protocol = type == "shadowsocks" ? "ss" : type, Server = server, Port = port, Secret = secret, Name = S(outbound, "tag", server) };
            outbound["tag"] = "proxy"; profile.SingBoxOutbound = Json.Write(outbound);
            object transport; if (outbound.TryGetValue("transport", out transport)) profile.Options["type"] = S(Map(transport), "type", "tcp");
            return profile;
        }
        internal static Dictionary<string, object> Clash(Dictionary<string, object> node)
        {
            var allowed = new HashSet<string>(new[] { "name", "type", "server", "port", "uuid", "password", "username", "cipher", "alterId", "alter-id", "flow", "tls", "servername", "sni", "skip-cert-verify", "client-fingerprint", "alpn", "reality-opts", "network", "ws-opts", "grpc-opts", "http-opts", "udp", "tfo", "obfs", "obfs-password", "up", "down", "congestion-controller", "udp-relay-mode", "reduce-rtt" });
            foreach (string key in node.Keys) if (!allowed.Contains(key)) throw new FormatException(L10n.T("Параметр Clash пока не поддерживается: ") + key);
            string type = S(node, "type", ""); if (type == "ss") type = "shadowsocks"; if (type == "hy2") type = "hysteria2"; if (type == "socks5") type = "socks";
            var result = O("type", type, "tag", S(node, "name", S(node, "server", "")), "server", S(node, "server", ""), "server_port", N(node, "port", 0));
            if (type == "vless" || type == "vmess" || type == "tuic") result["uuid"] = S(node, "uuid", "");
            if (type != "vless" && type != "vmess") Optional(node, "password", result, "password");
            if (type == "shadowsocks") result["method"] = S(node, "cipher", "");
            if (type == "vmess") { result["security"] = S(node, "cipher", "auto"); result["alter_id"] = N(node, "alterId", N(node, "alter-id", 0)); }
            if (type == "vless") Optional(node, "flow", result, "flow");
            if (type == "socks" || type == "http") { Optional(node, "username", result, "username"); if (type == "socks") result["version"] = "5"; }
            if (B(node, "skip-cert-verify")) throw new FormatException(L10n.T("skip-cert-verify не поддерживается."));
            bool tlsEnabled = B(node, "tls") || node.ContainsKey("reality-opts") || type == "trojan" || type == "hysteria2" || type == "tuic";
            if (tlsEnabled)
            {
                var tls = O("enabled", true, "server_name", S(node, "servername", S(node, "sni", S(node, "server", ""))));
                Optional(node, "alpn", tls, "alpn");
                if (node.ContainsKey("client-fingerprint")) tls["utls"] = O("enabled", true, "fingerprint", S(node, "client-fingerprint", "chrome"));
                object realityObject; if (node.TryGetValue("reality-opts", out realityObject)) { var reality = Map(realityObject); tls["reality"] = O("enabled", true, "public_key", S(reality, "public-key", ""), "short_id", S(reality, "short-id", "")); if (!tls.ContainsKey("utls")) tls["utls"] = O("enabled", true, "fingerprint", "chrome"); }
                result["tls"] = tls;
            }
            string network = S(node, "network", "tcp");
            if (network != "tcp")
            {
                if (network != "ws" && network != "grpc" && network != "http") throw new FormatException(L10n.T("Неподдерживаемый транспорт Clash: ") + network);
                var transport = O("type", network); object optionsObject;
                if (node.TryGetValue(network + "-opts", out optionsObject))
                {
                    var options = Map(optionsObject);
                    foreach (string key in options.Keys)
                    {
                        if (key == "grpc-service-name" && network == "grpc") transport["service_name"] = options[key];
                        else if (key == "max-early-data") transport["max_early_data"] = N(options, key, 0);
                        else if (key == "path" && options[key] is IList) { var paths = (IList)options[key]; if (paths.Count != 1) throw new FormatException(L10n.T("Нужен один HTTP path.")); transport["path"] = paths[0]; }
                        else if (key == "path" || key == "headers" || key == "host" || key == "early-data-header-name") transport[key.Replace('-', '_')] = options[key];
                        else throw new FormatException(L10n.T("Параметр транспорта Clash не поддерживается: ") + key);
                    }
                }
                result["transport"] = transport;
            }
            if (node.ContainsKey("udp") && !B(node, "udp")) result["network"] = "tcp";
            if (node.ContainsKey("tfo")) result["tcp_fast_open"] = B(node, "tfo");
            if (type == "hysteria2")
            {
                if (node.ContainsKey("obfs")) result["obfs"] = O("type", S(node, "obfs", ""), "password", S(node, "obfs-password", ""));
                if (node.ContainsKey("up")) result["up_mbps"] = N(node, "up", 0); if (node.ContainsKey("down")) result["down_mbps"] = N(node, "down", 0);
            }
            if (type == "tuic") { Optional(node, "congestion-controller", result, "congestion_control"); Optional(node, "udp-relay-mode", result, "udp_relay_mode"); if (node.ContainsKey("reduce-rtt")) result["zero_rtt_handshake"] = B(node, "reduce-rtt"); }
            return result;
        }
    }
}
