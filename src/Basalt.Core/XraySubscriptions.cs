using System;
using System.Collections;
using System.Collections.Generic;

namespace Basalt.Core
{
    public static class XraySubscriptions
    {
        static Dictionary<string, object> Map(object value) { var map = value as Dictionary<string, object>; if (map == null) throw new FormatException(L10n.T("Ожидался объект Xray JSON.")); return map; }
        static string Text(Dictionary<string, object> map, string key, string fallback) { object value; return map.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : fallback; }
        public static ImportResult Parse(string text)
        {
            var result = new ImportResult { Format = "Xray JSON" }; object root = Json.Serializer().DeserializeObject(text);
            var documents = root as IList ?? new object[] { root }; int index = 0;
            foreach (var document in documents)
            {
                index++;
                try
                {
                    var config = Map(document); object list;
                    if (!config.TryGetValue("outbounds", out list) || !(list is IList)) throw new FormatException(L10n.T("Поддерживается Xray JSON с outbounds; sing-box JSON и Clash YAML пока недоступны."));
                    int before = result.Profiles.Count;
                    foreach (var value in (IList)list)
                    {
                        var outbound = Map(value); string protocol = Text(outbound, "protocol", "");
                        if (protocol == "freedom" || protocol == "blackhole" || protocol == "dns") continue;
                        if (protocol == "hysteria") { var hy = XrayHysteria.Parse(outbound, Text(config, "remarks", Text(outbound, "tag", "Hysteria 2"))); ProviderRouting.Attach(hy, config, "xray", Text(outbound, "tag", "")); result.Profiles.Add(hy); continue; }
                        if (protocol != "vless" && protocol != "trojan") throw new FormatException(L10n.T("Xray JSON: неподдерживаемый outbound ") + protocol);
                        if (outbound.ContainsKey("proxySettings")) throw new FormatException(L10n.T("Цепочки proxySettings в подписке пока не поддерживаются."));
                        object streamValue;
                        if (outbound.TryGetValue("streamSettings", out streamValue))
                        {
                            var streamMap = Map(streamValue); object tlsValue, socketValue;
                            if (streamMap.TryGetValue("tlsSettings", out tlsValue))
                            {
                                var tls = Map(tlsValue);
                                if (Text(tls, "allowInsecure", "false").Equals("true", StringComparison.OrdinalIgnoreCase) || tls.ContainsKey("certificates")) throw new FormatException(L10n.T("Небезопасный TLS или внешние сертификаты в подписке не поддерживаются."));
                            }
                            if (streamMap.TryGetValue("sockopt", out socketValue) && Map(socketValue).ContainsKey("dialerProxy")) throw new FormatException(L10n.T("Ссылки dialerProxy на другие outbounds не поддерживаются."));
                        }
                        var parameters = Map(outbound["settings"]); var servers = parameters[protocol == "vless" ? "vnext" : "servers"] as IList;
                        if (servers == null || servers.Count != 1) throw new FormatException(L10n.T("Ожидался один сервер на outbound."));
                        var server = Map(servers[0]); string secret;
                        if (protocol == "vless") { var users = server["users"] as IList; if (users == null || users.Count != 1) throw new FormatException(L10n.T("Ожидался один пользователь VLESS.")); secret = Text(Map(users[0]), "id", ""); Guid id; if (!Guid.TryParse(secret, out id)) throw new FormatException(L10n.T("Некорректный UUID VLESS.")); }
                        else secret = Text(server, "password", "");
                        int port; string address = Text(server, "address", "");
                        if (String.IsNullOrEmpty(secret) || Uri.CheckHostName(address) == UriHostNameType.Unknown || !Int32.TryParse(Text(server, "port", ""), out port) || port < 1 || port > 65535) throw new FormatException(L10n.T("Некорректный адрес, порт или ключ Xray."));
                        var profile = new Profile { Protocol = protocol, Server = address, Port = port, Secret = secret, Name = Text(config, "remarks", Text(outbound, "tag", address)), XrayOutbound = Json.Write(outbound) };
                        object stream; if (outbound.TryGetValue("streamSettings", out stream)) profile.Options["type"] = Text(Map(stream), "network", "tcp");
                        ProviderRouting.Attach(profile, config, "xray", Text(outbound, "tag", "")); result.Profiles.Add(profile);
                    }
                    if (before == result.Profiles.Count) throw new FormatException(L10n.T("В JSON нет поддерживаемых VPN-серверов."));
                }
                catch (Exception e) { if (!(e is FormatException || e is KeyNotFoundException || e is InvalidCastException)) throw; result.Errors.Add("JSON " + index + ": " + e.Message); }
            }
            return result;
        }
    }
}
