using System;
using System.Collections.Generic;
using System.Net;

namespace Basalt.Core
{
    public sealed class ConnectionPlan
    {
        public string SingBoxJson;
        public string XrayJson;
        public bool NativeBridge;
    }
    public sealed class TunnelEnvironment
    {
        public string InterfaceName;
        public string[] ServerAddresses;
        public Dictionary<string, string> ResolvedServers;
    }
    public static class Configuration
    {
        static Dictionary<string, object> O(params object[] pairs) { return Json.Obj(pairs); }
        public static Dictionary<string, object> Outbound(Profile p)
        {
            if (!String.IsNullOrEmpty(p.SingBoxOutbound)) { var imported = Json.Read(p.SingBoxOutbound); imported["tag"] = "proxy"; return imported; }
            if (!String.IsNullOrEmpty(p.XrayOutbound)) { var imported = Json.Read(p.XrayOutbound); imported["tag"] = "proxy"; return imported; }
            var allowed = new HashSet<string>(new[] { "security", "type", "sni", "peer", "fp", "pbk", "sid", "spx", "flow", "encryption", "host", "path", "serviceName", "alpn", "allowInsecure", "insecure", "obfs", "obfs-password", "mode" });
            allowed.Add("extra");
            foreach (string key in p.Options.Keys) if (!allowed.Contains(key)) throw new FormatException(L10n.T("Параметр пока не поддерживается: ") + key);
            if (p.Option("allowInsecure", "0") != "0" || p.Option("insecure", "0") != "0") throw new FormatException(L10n.T("Отключение проверки TLS не поддерживается."));
            if (p.Protocol == "hysteria2" || p.Protocol == "ss")
            {
                var specific = p.Protocol == "ss" ? new HashSet<string>() : new HashSet<string>(new[] { "sni", "peer", "alpn", "insecure", "allowInsecure", "obfs", "obfs-password" });
                foreach (string key in p.Options.Keys) if (!specific.Contains(key)) throw new FormatException(L10n.T("Параметр не поддерживается для ") + p.Protocol + ": " + key);
                var outbound = O("type", p.Protocol == "ss" ? "shadowsocks" : "hysteria2", "tag", "proxy", "server", p.Server, "server_port", p.Port);
                if (p.Protocol == "ss")
                {
                    int colon = p.Secret.IndexOf(':');
                    if (colon <= 0 || colon == p.Secret.Length - 1) throw new FormatException(L10n.T("Shadowsocks: требуется method:password."));
                    outbound["method"] = p.Secret.Substring(0, colon); outbound["password"] = p.Secret.Substring(colon + 1);
                }
                else
                {
                    outbound["password"] = p.Secret;
                    var tls = O("enabled", true, "server_name", p.Option("sni", p.Option("peer", p.Server)));
                    if (p.Option("alpn", "") != "") tls["alpn"] = p.Options["alpn"].Split(',');
                    outbound["tls"] = tls;
                    if (p.Option("obfs", "") != "")
                    {
                        if (p.Options["obfs"] != "salamander" || p.Option("obfs-password", "") == "") throw new FormatException(L10n.T("Hysteria2: некорректный obfs."));
                        outbound["obfs"] = O("type", "salamander", "password", p.Options["obfs-password"]);
                    }
                }
                return outbound;
            }
            if (p.Protocol != "vless" && p.Protocol != "trojan") throw new FormatException(L10n.T("Неподдерживаемый протокол."));
            foreach (string key in p.Options.Keys)
                if (key == "obfs" || key == "obfs-password" || key == "peer") throw new FormatException(L10n.T("Параметр не поддерживается для ") + p.Protocol + ": " + key);
            string transport = p.Option("type", "tcp");
            if (transport != "tcp" && transport != "ws" && transport != "grpc" && transport != "xhttp") throw new FormatException(L10n.T("Доступны TCP, WebSocket, gRPC и XHTTP."));
            string security = p.Option("security", p.Protocol == "trojan" ? "tls" : "none");
            if (security != "none" && security != "tls" && security != "reality") throw new FormatException(L10n.T("Неподдерживаемый TLS режим."));
            if (p.Protocol == "trojan" && security == "none") throw new FormatException(L10n.T("Trojan требует TLS."));
            string flow = p.Option("flow", "");
            if (flow != "" && (flow != "xtls-rprx-vision" || transport != "tcp" || security == "none" || p.Protocol != "vless")) throw new FormatException(L10n.T("Vision требует VLESS TCP TLS/REALITY."));
            if (p.Option("encryption", "none") != "none") throw new FormatException(L10n.T("VLESS encryption пока поддерживает только none."));
            var stream = O("network", transport, "security", security);
            if (security == "tls")
            {
                var tls = O("serverName", p.Option("sni", p.Server), "fingerprint", p.Option("fp", "chrome"));
                if (p.Option("alpn", "") != "") tls["alpn"] = p.Options["alpn"].Split(',');
                stream["tlsSettings"] = tls;
            }
            if (security == "reality")
            {
                if (p.Option("pbk", "") == "" || p.Option("sni", "") == "") throw new FormatException(L10n.T("REALITY требует pbk и sni."));
                stream["realitySettings"] = O("serverName", p.Options["sni"], "fingerprint", p.Option("fp", "chrome"), "publicKey", p.Options["pbk"], "shortId", p.Option("sid", ""), "spiderX", p.Option("spx", "/"));
            }
            if (transport == "ws") stream["wsSettings"] = O("path", p.Option("path", "/"), "headers", O("Host", p.Option("host", p.Server)));
            if (transport == "grpc") stream["grpcSettings"] = O("serviceName", p.Option("serviceName", ""));
            if (transport == "xhttp")
            {
                string mode = p.Option("mode", "auto");
                if (mode != "auto" && mode != "packet-up" && mode != "stream-up" && mode != "stream-one") throw new FormatException(L10n.T("Неизвестный режим XHTTP."));
                stream["xhttpSettings"] = O("path", p.Option("path", "/"), "host", p.Option("host", p.Server), "mode", mode);
                if (p.Options.ContainsKey("extra"))
                {
                    Dictionary<string, object> extra;
                    try { extra = Json.Serializer().DeserializeObject(p.Options["extra"]) as Dictionary<string, object>; }
                    catch (ArgumentException) { throw new FormatException(L10n.T("XHTTP extra: требуется корректный JSON-объект.")); }
                    if (extra == null) throw new FormatException(L10n.T("XHTTP extra: требуется JSON-объект."));
                    ((Dictionary<string, object>)stream["xhttpSettings"])["extra"] = extra;
                }
            }
            else if (p.Options.ContainsKey("mode")) throw new FormatException(L10n.T("Параметр mode доступен только для XHTTP."));
            if (transport != "xhttp" && p.Options.ContainsKey("extra")) throw new FormatException(L10n.T("Параметр extra доступен только для XHTTP."));
            object settings;
            if (p.Protocol == "vless") settings = O("vnext", new[] { O("address", p.Server, "port", p.Port, "users", new[] { O("id", p.Secret, "encryption", "none", "flow", flow) }) });
            else settings = O("servers", new[] { O("address", p.Server, "port", p.Port, "password", p.Secret) });
            return O("tag", "proxy", "protocol", p.Protocol, "settings", settings, "streamSettings", stream);
        }
        public static List<Dictionary<string, object>> Rules(string entries, string outbound)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (string raw in (entries ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string value = raw.Trim(); if (value == "" || value.StartsWith("#")) continue;
                Uri url; if (value.Contains("://") && Uri.TryCreate(value, UriKind.Absolute, out url)) value = url.DnsSafeHost;
                IPAddress ip; string field;
                if (IPAddress.TryParse(value, out ip)) { value = ip + (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "/32" : "/128"); field = "ip_cidr"; }
                else if (value.Contains("/"))
                {
                    string[] parts = value.Split('/'); int prefix;
                    if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out ip) || !Int32.TryParse(parts[1], out prefix) || prefix < 0 || prefix > (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128)) throw new FormatException(L10n.T("Некорректная подсеть: ") + value);
                    field = "ip_cidr";
                }
                else
                {
                    field = value.StartsWith("*.") || value.StartsWith(".") ? "domain_suffix" : "domain";
                    value = value.TrimStart('*', '.');
                    if (Uri.CheckHostName(value) != UriHostNameType.Dns) throw new FormatException(L10n.T("Некорректный домен: ") + value);
                    value = new System.Globalization.IdnMapping().GetAscii(value).ToLowerInvariant();
                }
                result.Add(O(field, new[] { value }, "outbound", outbound));
            }
            return result;
        }
        public static ConnectionPlan Build(Profile profile, Settings settings)
        { return Build(profile, settings, null); }
        public static ConnectionPlan Build(Profile profile, Settings settings, TunnelEnvironment tunnel)
        {
            profile = ProviderRouting.Resolve(profile,settings);
            if (settings.ProxyPort < 1024 || settings.ProxyPort > 65535 || settings.BridgePort < 1024 || settings.BridgePort > 65535 || settings.ProxyPort == settings.BridgePort) throw new ArgumentException(L10n.T("Порты должны быть разными и находиться в диапазоне 1024–65535."));
            if (settings.RouteMode < 0 || settings.RouteMode > 2) throw new ArgumentException(L10n.T("Неизвестный режим маршрутизации."));
            if (settings.HttpPort < 1024 || settings.HttpPort > 65535 || settings.HttpPort == settings.ProxyPort || settings.HttpPort == settings.BridgePort) throw new ArgumentException(L10n.T("HTTP, SOCKS и порт моста должны различаться."));
            if (settings.AllowLan && (String.IsNullOrWhiteSpace(settings.ProxyUsername) || String.IsNullOrEmpty(settings.ProxyPassword))) throw new ArgumentException(L10n.T("Для доступа из LAN задайте логин и пароль локального прокси."));
            if (settings.TunMode && (tunnel == null || tunnel.ServerAddresses == null || tunnel.ServerAddresses.Length == 0 || String.IsNullOrEmpty(tunnel.InterfaceName))) throw new ArgumentException(L10n.T("Для TUN требуется подготовленное сетевое окружение."));
            if (settings.SystemProxy && !settings.TunMode && !String.IsNullOrEmpty(settings.ProxyUsername)) throw new ArgumentException(L10n.T("Системный прокси с авторизацией требует настройки в приложениях. Выберите TUN или локальный режим."));
            var outbound = Outbound(profile); var plan = new ConnectionPlan();
            if (String.IsNullOrEmpty(profile.SingBoxOutbound) && (!String.IsNullOrEmpty(profile.XrayOutbound) || profile.Protocol == "vless" || profile.Protocol == "trojan"))
            {
                if (settings.TunMode)
                {
                    var parameters = (Dictionary<string, object>)outbound["settings"];
                    if ((string)outbound["protocol"] == "hysteria") parameters["address"] = tunnel.ServerAddresses[0];
                    else
                    {
                        var servers = (System.Collections.IList)parameters[profile.Protocol == "vless" ? "vnext" : "servers"];
                        ((Dictionary<string, object>)servers[0])["address"] = tunnel.ServerAddresses[0];
                    }
                }
                plan.XrayJson = Json.Write(O("log", O("loglevel", "warning"), "inbounds", new[] { O("tag", "bridge", "listen", "127.0.0.1", "port", settings.BridgePort, "protocol", "socks", "settings", O("auth", "noauth", "udp", true)) }, "outbounds", new[] { outbound }));
                outbound = O("type", "socks", "tag", "proxy", "server", "127.0.0.1", "server_port", settings.BridgePort, "version", "5");
            }
            var rules = Rules(settings.RouteEntries, settings.RouteMode == 1 ? "proxy" : "direct");
            if (settings.RouteMode == 0) rules.Clear();
            string listen = settings.AllowLan ? "0.0.0.0" : "127.0.0.1";
            var mixed = O("type", "mixed", "tag", "local", "listen", listen, "listen_port", settings.ProxyPort);
            var http = O("type", "http", "tag", "http", "listen", listen, "listen_port", settings.HttpPort);
            if (!String.IsNullOrEmpty(settings.ProxyUsername)) { var users = new[] { O("username", settings.ProxyUsername, "password", settings.ProxyPassword ?? "") }; mixed["users"] = users; http["users"] = users; }
            var inbounds = new List<Dictionary<string, object>> { mixed, http };
            var config = O("log", O("level", "warn", "timestamp", true), "inbounds", inbounds, "outbounds", new[] { outbound, O("type", "direct", "tag", "direct") }, "route", O("rules", rules, "final", settings.RouteMode == 1 ? "direct" : "proxy"));
            if (settings.TunMode)
            {
                if (settings.TunMtu < 1280 || settings.TunMtu > 9000) throw new ArgumentException(L10n.T("MTU должен быть от 1280 до 9000."));
                IPAddress directDns, remoteDns;
                if (!IPAddress.TryParse(settings.DirectDns, out directDns) || !IPAddress.TryParse(settings.RemoteDns, out remoteDns)) throw new ArgumentException(L10n.T("DNS-серверы задаются IP-адресами."));
                var excludes = new List<string>(); foreach (string address in tunnel.ServerAddresses) { var ip = IPAddress.Parse(address); excludes.Add(ip + (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "/32" : "/128")); }
                inbounds.Add(O("type", "tun", "tag", "tun", "interface_name", tunnel.InterfaceName, "address", new[] { "172.31.255.1/30", "fdfe:ba5a:17::1/126" }, "mtu", settings.TunMtu, "auto_route", true, "strict_route", true, "route_exclude_address", excludes, "stack", "system"));
                // Endpoint IPs bypass the TUN at the OS level, including Xray's separate process.
                if (plan.XrayJson == null) outbound["server"] = tunnel.ServerAddresses[0];
                var route = (Dictionary<string, object>)config["route"]; route["auto_detect_interface"] = true; route["default_domain_resolver"] = "dns-direct";
                var dnsRules = new List<Dictionary<string, object>>();
                if (settings.RouteMode != 0) foreach (var rule in Rules(settings.RouteEntries, "unused"))
                { if (!rule.ContainsKey("ip_cidr")) { rule.Remove("outbound"); rule["server"] = settings.RouteMode == 1 ? "dns-remote" : "dns-direct"; dnsRules.Add(rule); } }
                config["dns"] = O("servers", new[] { O("type", "udp", "tag", "dns-direct", "server", settings.DirectDns), O("type", "udp", "tag", "dns-remote", "server", settings.RemoteDns, "detour", "proxy") }, "rules", dnsRules, "final", settings.RouteMode == 1 ? "dns-direct" : "dns-remote", "strategy", settings.IPv6 ? "prefer_ipv4" : "ipv4_only", "reverse_mapping", true);
                if (settings.BypassLan) rules.Insert(0, O("ip_is_private", true, "outbound", "direct"));
                if (!String.IsNullOrWhiteSpace(settings.ApplicationBypass)) rules.Insert(0, O("process_name", settings.ApplicationBypass.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), "outbound", "direct"));
                if (!settings.IPv6) rules.Insert(0, O("ip_version", 6, "action", "reject"));
                rules.Insert(0, O("protocol", "dns", "action", "hijack-dns"));
                rules.Insert(0, O("inbound", new[] { "tun" }, "action", "sniff"));
            }
            plan.SingBoxJson = Json.Write(config);
            ProviderRouting.Apply(profile, settings, tunnel, plan);
            if(plan.XrayJson!=null) {
                var xr=Json.Read(plan.XrayJson);
                foreach(var value in ProviderRouting.List(xr,"inbounds")) { var inbound=ProviderRouting.Map(value); if(!inbound.ContainsKey("sniffing")) inbound["sniffing"]=O("enabled",true,"destOverride",new[]{"http","tls","quic"},"routeOnly",true); }
                plan.XrayJson=Json.Write(xr);
                if(settings.TunMode) {
                    // Xray's already-routed sockets must leave via sing-box's physical interface,
                    // rather than re-enter the provider policy through the TUN again.
                    var sb=Json.Read(plan.SingBoxJson); var route=ProviderRouting.Map(sb["route"]); var finalRules=new List<object>();
                    var coreBypass=O("inbound",new[]{"tun"},"process_path",new[]{System.IO.Path.GetFullPath(settings.XrayPath)},"outbound","direct"); if(!settings.IPv6) coreBypass["ip_version"]=4; finalRules.Add(coreBypass);
                    foreach(var rule in ProviderRouting.List(route,"rules")) finalRules.Add(rule); route["rules"]=finalRules;
                    plan.SingBoxJson=Json.Write(sb);
                }
            }
            // Provider DNS must respect the local TUN address-family switch.
            if(settings.TunMode && !settings.IPv6) {
                var sb=Json.Read(plan.SingBoxJson); object dns;
                if(sb.TryGetValue("dns",out dns)) ProviderRouting.Map(dns)["strategy"]="ipv4_only";
                plan.SingBoxJson=Json.Write(sb);
                if(plan.XrayJson!=null) {var xr=Json.Read(plan.XrayJson); if(xr.TryGetValue("dns",out dns)) ProviderRouting.Map(dns)["queryStrategy"]="UseIPv4"; plan.XrayJson=Json.Write(xr);}
            }
            return plan;
        }
    }
}
