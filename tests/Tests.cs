using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Basalt.Core;

static class Tests
{
    static int count;
    static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    static void Reject(Action action, string name) { bool failed = false; try { action(); } catch (FormatException) { failed = true; } catch (ArgumentException) { failed = true; } Assert(failed, name); }
    static Dictionary<string, object> Obj(object o) { return (Dictionary<string, object>)o; }
    static object First(object o) { return ((IList)o)[0]; }
    static int Main()
    {
        try
        {
            L10n.SetLanguage("ru");
            var identity = new Settings(); var identityReload = Json.Serializer().Deserialize<Settings>(Json.Write(identity));
            identity.MigrateSubscriptionDefaults(); Assert(identity.SubscriptionUserAgent == "Happ/1.0" && identity.SubscriptionHwidMode == "auto", "New global subscription preferences initialized");
            var migrated = new Settings(); migrated.Subscriptions.Add(new Subscription { UserAgent = "Custom/2", HwidMode = "never" }); migrated.MigrateSubscriptionDefaults();
            var migratedReload = Json.Serializer().Deserialize<Settings>(Json.Write(migrated)); migratedReload.MigrateSubscriptionDefaults();
            Assert(migratedReload.SubscriptionUserAgent == "Custom/2" && migratedReload.SubscriptionHwidMode == "never", "Global subscription preferences migrate and persist without resetting");
            Assert(identity.DeviceId == identityReload.DeviceId, "Installation HWID survives settings roundtrip");
            var happ = new Subscription { UserAgent = "Happ/1.0" };
            Assert(Subscriptions.DeviceHeaders(happ, identity.DeviceId, false)["X-Hwid"] == identity.DeviceId && Subscriptions.DeviceHeaders(happ, identity.DeviceId, false)["X-Device-Model"] == "Basalt 386", "Happ auto sends stable HWID and x86 device model");
            happ.HwidMode = "never"; Assert(Subscriptions.DeviceHeaders(happ, identity.DeviceId, true).Count == 0, "HWID opt-out overrides Happ");
            happ.UserAgent = "Custom/1.0"; happ.HwidMode = "always"; Assert(Subscriptions.DeviceHeaders(happ, identity.DeviceId, true).Count == 3, "Custom user-agent supports explicit HWID");
            happ.HwidMode = "auto"; Assert(Subscriptions.DeviceHeaders(happ, identity.DeviceId, true).Count == 0, "Other identities do not send HWID automatically");
            var metadata = SubscriptionMetadata.Read("upload=1024; download=2048; total=4096; expire=1790000000", "12", "https://example.org/help");
            var subscriptionMetadata = new Subscription(); metadata.Apply(subscriptionMetadata);
            Assert(subscriptionMetadata.UploadBytes == 1024 && subscriptionMetadata.DownloadBytes == 2048 && subscriptionMetadata.TotalBytes == 4096 && subscriptionMetadata.ExpireUnix == 1790000000 && subscriptionMetadata.UpdateHours == 12, "Provider quota expiry and interval parsed");
            var missing = SubscriptionMetadata.Read("total=0; download=-1; expire=999999999999999", "0", "javascript:alert(1)");
            Assert(missing.Total == 0 && !missing.Download.HasValue && !missing.Expire.HasValue && missing.SupportUrl == null, "Unlimited quota distinct from missing and invalid metadata rejected");
            const string vless = "vless://11111111-1111-4111-8111-111111111111@example.com:443?security=reality&sni=example.org&pbk=test-public-key&sid=12&flow=xtls-rprx-vision#Test";
            var p = Subscriptions.ParseUri(vless); Assert(p.Name == "Test" && p.Engine == "Xray + sing-box", "REALITY profile uses dual engine");
            var settings = new Settings(); var plan = Configuration.Build(p, settings);
            var jsonImported = Subscriptions.Parse("[" + plan.XrayJson + "]");
            Assert(jsonImported.Errors.Count == 0 && jsonImported.Profiles.Count == 1, "Happ Xray JSON array imports VPN outbound");
            var jsonTun = Configuration.Build(jsonImported.Profiles[0], new Settings { TunMode = true }, new TunnelEnvironment { InterfaceName = "Basalt-test", ServerAddresses = new[] { "192.0.2.9" } });
            Assert(jsonTun.XrayJson.Contains("192.0.2.9") && jsonTun.XrayJson.Contains("realitySettings"), "JSON outbound preserves transport and supports TUN endpoint substitution");
            var xr = Json.Read(plan.XrayJson); var sb = Json.Read(plan.SingBoxJson);
            Assert((string)Obj(First(xr["inbounds"]))["listen"] == "127.0.0.1", "Xray bridge loopback only");
            Assert((string)Obj(First(sb["inbounds"]))["listen"] == "127.0.0.1", "Public mixed proxy loopback only");
            Assert((int)Obj(First(sb["outbounds"]))["server_port"] == settings.BridgePort, "sing-box targets Xray bridge");
            Assert((string)Obj(Obj(First(xr["outbounds"]))["streamSettings"])["security"] == "reality", "REALITY not downgraded");
            var hy = Subscriptions.ParseUri("hy2://secret@example.net:443?sni=example.net&obfs=salamander&obfs-password=test#Hysteria");
            Assert(Configuration.Build(hy, settings).XrayJson == null, "Hysteria2 uses sing-box directly");
            var tunSettings = new Settings { TunMode = true, ApplicationBypass = "example.exe" };
            Reject(delegate { Configuration.Build(p, tunSettings); }, "TUN requires resolved endpoint environment");
            var tunnel = new TunnelEnvironment { InterfaceName = "Basalt-test", ServerAddresses = new[] { "192.0.2.1", "2001:db8::1" } };
            var tunPlan = Configuration.Build(p, tunSettings, tunnel); var tunConfig = Json.Read(tunPlan.SingBoxJson);
            var tunInbound = Obj(((IList)tunConfig["inbounds"])[2]);
            Assert((string)tunInbound["type"] == "tun" && (bool)tunInbound["auto_route"] && (bool)tunInbound["strict_route"] && ((IList)tunInbound["route_exclude_address"]).Count == 2, "TUN routes enabled and all endpoint IPs excluded");
            var tunXray = Json.Read(tunPlan.XrayJson);
            Assert((string)Obj(First(Obj(Obj(First(tunXray["outbounds"]))["settings"])["vnext"]))["address"] == "192.0.2.1", "Separate Xray process uses pre-resolved endpoint to avoid loops");
            var tunRules = (IList)Obj(tunConfig["route"])["rules"];
            Assert((string)Obj(tunRules[2])["action"] == "hijack-dns" && (string)Obj(tunRules[3])["action"] == "reject" && (int)Obj(tunRules[3])["ip_version"] == 6, "DNS hijacked and disabled IPv6 explicitly rejected");
            Assert((string)Obj(tunConfig["dns"])["final"] == "dns-remote", "Default TUN DNS goes through VPN");
            tunSettings.AllowLan = true; Reject(delegate { Configuration.Build(p, tunSettings, tunnel); }, "LAN access requires proxy authentication");
            tunSettings.ProxyUsername = "test"; tunSettings.ProxyPassword = "secret";
            Assert(Obj(First(Json.Read(Configuration.Build(hy, tunSettings, tunnel).SingBoxJson)["inbounds"])).ContainsKey("users"), "LAN authentication emitted");
            var ss = Subscriptions.ParseUri("ss://" + Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-128-gcm:p:a:ss")) + "@example.net:8388#SS");
            Assert((string)Configuration.Outbound(ss)["password"] == "p:a:ss", "SIP002 password preserves colons");
            var xhttp = Subscriptions.ParseUri("vless://11111111-1111-4111-8111-111111111111@example.com:443?security=tls&type=xhttp&mode=packet-up");
            Assert((string)Obj(Obj(Configuration.Outbound(xhttp)["streamSettings"])["xhttpSettings"])["mode"] == "packet-up", "XHTTP share link mode preserved");
            Reject(delegate { Configuration.Build(hy, new Settings { SystemProxy = true, ProxyUsername = "name", ProxyPassword = "secret" }); }, "System proxy cannot silently enable unusable authentication");
            var imported = Subscriptions.Parse(Convert.ToBase64String(Encoding.UTF8.GetBytes(vless + "\n" + vless + "\n" + "bad://value")));
            Assert(imported.Profiles.Count == 1 && imported.Errors.Count == 1, "Base64 import deduplicates and reports invalid entry");
            Reject(delegate { Subscriptions.ParseUri(vless + "&foo=bar"); /* fragment is not a query */ Subscriptions.ParseUri(vless.Replace("#Test", "&foo=bar#Test")); }, "Unknown option rejected");
            Reject(delegate { Subscriptions.ParseUri(vless.Replace("#Test", "&type=xhttp#Test")); }, "Unsupported transport rejected");
            Reject(delegate { Subscriptions.ParseUri(vless.Replace("#Test", "&insecure=1#Test")); }, "TLS validation cannot be disabled");
            Reject(delegate { Subscriptions.ParseUri(vless.Replace("#Test", "&security=tls#Test")); }, "Duplicate security parameter rejected");
            var rules = Configuration.Rules("https://example.org/path\n*.example.com\n192.168.0.0/16\n::1", "direct");
            Assert(rules.Count == 4 && rules[1].ContainsKey("domain_suffix") && rules[3].ContainsKey("ip_cidr"), "Route normalization supports URL wildcard IPv4 IPv6");
            Reject(delegate { Configuration.Rules("10.0.0.0/33", "direct"); }, "Invalid CIDR rejected");
            Reject(delegate { Configuration.Rules("example.*.com", "direct"); }, "Invalid wildcard rejected");
            settings.RouteMode = 1; settings.RouteEntries = "*.example.com";
            sb = Json.Read(Configuration.Build(hy, settings).SingBoxJson);
            Assert((string)Obj(sb["route"])["final"] == "direct" && (string)Obj(First(Obj(sb["route"])["rules"]))["outbound"] == "proxy", "Include mode has direct default and proxy rule");
            settings.RouteMode = 2; sb = Json.Read(Configuration.Build(hy, settings).SingBoxJson);
            Assert((string)Obj(sb["route"])["final"] == "proxy" && (string)Obj(First(Obj(sb["route"])["rules"]))["outbound"] == "direct", "Exclude mode has proxy default and direct rule");
            settings.ProxyPort = settings.BridgePort; Reject(delegate { Configuration.Build(p, settings); }, "Port collisions rejected");
            var data = new Settings(); var one = Subscriptions.Parse(vless); Subscriptions.Merge(data, one, "subscription"); data.SelectedId = data.Profiles[0].Id; string id = data.SelectedId;
            data.Profiles[0].Favorite = true;
            Subscriptions.Merge(data, Subscriptions.Parse(vless.Replace("#Test", "#Renamed")), "subscription");
            Assert(data.Profiles.Count == 1 && data.Profiles[0].Id == id && data.SelectedId == id && data.Profiles[0].Name == "Renamed", "Subscription update preserves identity and selection");
            Assert(data.Profiles[0].Favorite, "Subscription refresh preserves favorites");
            var viewProfiles = new List<Profile> { data.Profiles[0], hy, ss };
            var timings = new Dictionary<string, int> { { hy.Id, 35 }, { ss.Id, -1 } };
            Assert(ProfileView.Filter(viewProfiles, "example.NET", null, false, false, timings).Count == 2, "Search is case-insensitive and includes server address");
            Assert(ProfileView.Filter(viewProfiles, "", "subscription", true, false, timings).Count == 1, "Group and favorite filters compose");
            Assert(ProfileView.Filter(viewProfiles, "", "manual", false, true, timings)[0].Id == hy.Id, "Latency sorting puts measured results before failures");
            Assert(ProfileView.Filter(viewProfiles, "not-found", null, false, false, timings).Count == 0, "Search can return empty without changing profiles");
            Reject(delegate { Subscriptions.Merge(data, imported, "subscription"); }, "Partial subscription update rejected atomically");
            Assert(data.Profiles.Count == 1 && data.Profiles[0].Name == "Renamed", "Failed update preserves previous profiles");
            var clash = Subscriptions.Parse(FormatFixtures.Clash);
            Assert(clash.Errors.Count == 0 && clash.Profiles.Count == 6, "Clash block/flow YAML, anchors and six protocols imported");
            Assert(clash.Profiles[1].Secret == "p:a#ss" && clash.Profiles[0].Name == "VLESS # TLS", "Quoted YAML hashes and colons preserved");
            var native = Subscriptions.Parse(FormatFixtures.SingBox);
            Assert(native.Errors.Count == 0 && native.Profiles.Count == 1 && native.Profiles[0].Engine == "sing-box", "sing-box policy entries excluded and native VLESS selected");
            var nativePlan = Configuration.Build(native.Profiles[0], new Settings { TunMode = true }, tunnel);
            Assert(nativePlan.XrayJson == null && nativePlan.SingBoxJson.Contains("example.org") && nativePlan.SingBoxJson.Contains("192.0.2.1"), "Native TUN resolves endpoint but preserves TLS server name");
            Assert(Subscriptions.Parse(FormatFixtures.Clash.Replace("password: test}", "password: test, plugin: unknown}")).Errors.Count > 0, "Unsupported Clash plugin rejected explicitly");
            Reject(delegate { Subscriptions.Parse("proxies: &loop [*loop]"); }, "Cyclic YAML aliases rejected");
            Reject(delegate { Subscriptions.Parse("proxies: []\nproxies: []"); }, "Duplicate YAML keys rejected");
            var scheduled = new Subscription { Updated = "2026-01-01T00:00:00Z", UpdateHours = 6 };
            var now = new DateTime(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc);
            Assert(SubscriptionSchedule.Due(scheduled, now) && !SubscriptionSchedule.Due(scheduled, now.AddSeconds(-1)), "Provider refresh interval triggers at boundary");
            scheduled.UpdateIntervalHours = 24; Assert(!SubscriptionSchedule.Due(scheduled, now), "Custom refresh interval overrides provider");
            SubscriptionSchedule.Failure(scheduled, now); Assert(SubscriptionSchedule.Next(scheduled) == now.AddMinutes(5), "First failure backs off five minutes");
            SubscriptionSchedule.Failure(scheduled, now); Assert(SubscriptionSchedule.Next(scheduled) == now.AddMinutes(10), "Repeated failure uses exponential backoff");
            SubscriptionSchedule.Success(scheduled, now); Assert(scheduled.UpdateFailures == 0 && scheduled.LastUpdateError == null, "Successful refresh resets failure state");
            scheduled.AutoUpdate = false; Assert(!SubscriptionSchedule.Due(scheduled, DateTime.MaxValue), "Disabled subscription never scheduled");
            var failedImport = new ImportResult { Format = "Share-ссылки" };
            failedImport.Profiles.Add(p); failedImport.Errors.Add("Строка 2: Параметр пока не поддерживается: extra");
            try { Subscriptions.Merge(new Settings(), failedImport, "new-subscription"); throw new Exception("Expected diagnostic exception"); }
            catch (SubscriptionImportException error) { Assert(error.Report.Contains("extra") && error.Report.Contains("Распознано серверов: 1") && error.Report.Contains("не добавлена"), "Partial import exposes exact cause and accepted count"); }
            failedImport.Profiles.Clear();
            try { Subscriptions.Merge(new Settings(), failedImport, "new-subscription"); throw new Exception("Expected empty import diagnostic"); }
            catch (SubscriptionImportException error) { Assert(error.Report.Contains("extra") && error.Report.Contains("Распознано серверов: 0"), "Fully rejected import retains parser diagnostics"); }
            var extraImport = Subscriptions.Parse(FormatFixtures.XhttpExtra);
            Assert(extraImport.Errors.Count == 0 && extraImport.Profiles.Count == 1, "XHTTP URI extra imports");
            var extraObject = Obj(Obj(Obj(Configuration.Outbound(extraImport.Profiles[0])["streamSettings"])["xhttpSettings"])["extra"]);
            Assert((bool)extraObject["noGRPCHeader"] && (string)Obj(extraObject["xmux"])["maxConcurrency"] == "16-32", "Nested extra remains typed JSON");
            Assert(Subscriptions.Parse(Convert.ToBase64String(Encoding.UTF8.GetBytes(FormatFixtures.XhttpExtra))).Profiles.Count == 1, "Base64 XHTTP extra imports");
            Assert(Subscriptions.Parse(FormatFixtures.XhttpExtra.Replace("type=xhttp", "type=ws")).Errors.Count == 1, "Extra on wrong transport rejected");
            Assert(Subscriptions.Parse(FormatFixtures.XhttpExtra.Split(new[] { "&extra=" }, StringSplitOptions.None)[0] + "&extra=%5B%5D").Errors.Count == 1, "Non-object extra rejected");
            var xrayHy = Subscriptions.Parse(FormatFixtures.XrayHysteria);
            Assert(xrayHy.Errors.Count == 0 && xrayHy.Profiles.Count == 1 && xrayHy.Profiles[0].Engine == "Xray + sing-box", "Xray Hysteria 2 retains native engine");
            var hyPlan = Configuration.Build(xrayHy.Profiles[0], new Settings { TunMode = true }, tunnel);
            var hyOutbound = Obj(First(Json.Read(hyPlan.XrayJson)["outbounds"]));
            var hyStream = Obj(hyOutbound["streamSettings"]);
            Assert((string)Obj(hyStream["hysteriaSettings"])["auth"] == "test-auth" && (string)Obj(hyOutbound["settings"])["address"] == "192.0.2.1" && (string)Obj(hyStream["tlsSettings"])["serverName"] == "example.org", "Native Hysteria auth and TLS name preserved with TUN IP pinning");
            Assert(Subscriptions.Parse(FormatFixtures.XrayHysteria.Replace("\"version\":2", "\"version\":1")).Errors.Count == 1, "Hysteria v1 rejected");
            var advancedHy = Subscriptions.Parse(FormatFixtures.XrayHysteria.Replace("\"auth\":", "\"udphop\":{},\"auth\":"));
            Assert(advancedHy.Errors.Count == 0 && advancedHy.Profiles[0].XrayOutbound.Contains("udphop"), "Advanced Hysteria settings preserved for native Xray validation");
            var mixedSubscription = Subscriptions.Parse("[" + FormatFixtures.XrayHysteria + "," + Configuration.Build(p, new Settings()).XrayJson + "]");
            var mixedSettings = new Settings(); Subscriptions.Merge(mixedSettings, mixedSubscription, "mixed");
            Assert(mixedSettings.Profiles.Count == 2 && ProfileView.Filter(mixedSettings.Profiles, "hysteria2", null, false, false, new Dictionary<string,int>()).Count == 1, "Hysteria survives mixed Xray subscription merge and UI filtering");
            var happRouting = Subscriptions.Parse(new SubscriptionDownload { Body=FormatFixtures.XhttpExtra, Routing=FormatFixtures.HappRoute });
            Assert(happRouting.Errors.Count==0 && happRouting.Profiles[0].ProviderFormat=="happ", "Happ HTTP routing header binds to profile");
            var routedPlan=Configuration.Build(happRouting.Profiles[0],new Settings());
            Assert(routedPlan.XrayJson.Contains("domain:example.net") && routedPlan.XrayJson.Contains("__basalt_block"), "Happ direct and reject rules reach Xray");
            var routedSettings=new Settings(); Subscriptions.Merge(routedSettings,happRouting,"routed");
            Assert(routedSettings.RouteDocuments.Count==1 && routedSettings.Profiles[0].ProviderConfig==null, "Routing document stored once instead of repeated for every server");
            var routedReload=Json.Serializer().Deserialize<Settings>(Json.Write(routedSettings));
            Assert(Configuration.Build(routedReload.Profiles[0],routedReload).XrayJson.Contains("domain:example.net"), "Routing survives persistence and reload");
            routedReload.UseSubscriptionRouting=false; Assert(!Configuration.Build(routedReload.Profiles[0],routedReload).XrayJson.Contains("__basalt_block"), "Provider routing toggle removes provider policy");
            var malformed=Subscriptions.Parse(new SubscriptionDownload {Body=FormatFixtures.XhttpExtra,Routing="happ://routing/onadd/invalid"});
            Reject(delegate { Subscriptions.Merge(routedSettings,malformed,"routed"); }, "Malformed provider policy prevents atomic replacement");
            Assert(routedSettings.Profiles.Count==1 && routedSettings.RouteDocuments.Count==1,"Failed policy update preserves profiles and policy");
            var nativeRoute=Subscriptions.Parse(FormatFixtures.SingRoutes); var nativeRouted=Configuration.Build(nativeRoute.Profiles[0],new Settings());
            Assert(nativeRouted.SingBoxJson.Contains("example.net") && nativeRouted.SingBoxJson.Contains("select"),"Native policy retains rules and selector graph");
            var edited=Json.Read(nativeRoute.Profiles[0].SingBoxOutbound); edited["server"]="edited.example.org"; nativeRoute.Profiles[0].SingBoxOutbound=Json.Write(edited); nativeRoute.Profiles[0].Server="edited.example.org"; Assert(Configuration.Build(nativeRoute.Profiles[0],new Settings()).SingBoxJson.Contains("edited.example.org"),"Edited outbound overrides stored provider graph"); var policyRules=ProviderRouting.List(ProviderRouting.Map(Json.Read(routedPlan.XrayJson)["routing"]),"rules"); bool catchAll=false; foreach(var r in policyRules) {var rule=ProviderRouting.Map(r); if(ProviderRouting.S(rule,"network","")=="tcp,udp") catchAll=true;} Assert(!catchAll,"Happ fallback does not suppress IPIfNonMatch second pass");
            var directDefault=Subscriptions.Parse(FormatFixtures.Happ).Profiles[0]; var directPolicy=Json.Read(directDefault.ProviderConfig);directPolicy["GlobalProxy"]="false";directDefault.ProviderConfig=Json.Write(directPolicy);var directPlan=Configuration.Build(directDefault,new Settings()); Assert(ProviderRouting.S(ProviderRouting.Map(ProviderRouting.List(Json.Read(directPlan.XrayJson),"outbounds")[0]),"tag","")=="__basalt_direct","Happ direct fallback uses first outbound without catch-all");
            Assert(Obj(tunRules[0]).ContainsKey("process_path") && (string)Obj(tunRules[0])["outbound"]=="direct" && (string)((System.Collections.IList)Obj(tunRules[0])["inbound"])[0]=="tun","Xray routed sockets bypass recursive TUN policy");
            Assert(routedPlan.XrayJson.Contains("sniffing") && routedPlan.XrayJson.Contains("routeOnly"),"Xray can match TLS domains received as IP destinations");
            foreach(var body in new[]{FormatFixtures.XrayRoutes,FormatFixtures.SingRoutes,FormatFixtures.ClashRoutes}) {
 var plain=Subscriptions.Parse(body); var combined=Subscriptions.Parse(new SubscriptionDownload{Body=body,Routing=FormatFixtures.HappRoute});
 Assert(combined.Errors.Count==0 && combined.Profiles[0].ProviderFormat==plain.Profiles[0].ProviderFormat && combined.Profiles[0].ProviderConfig==plain.Profiles[0].ProviderConfig,"Happ header preserves complete native routing document: "+plain.Profiles[0].ProviderFormat);
 Assert(!String.IsNullOrEmpty(combined.Profiles[0].HappRoutingConfig),"Happ metadata retained separately for geodata");
}
var dnsProfile=Subscriptions.Parse(FormatFixtures.XrayRoutes).Profiles[0]; string dnsSource=dnsProfile.ProviderConfig;
var dnsSettings=new Settings{TunMode=true,IPv6=false}; var dnsPlan=Configuration.Build(dnsProfile,dnsSettings,tunnel);
Assert(ProviderRouting.S(Obj(Json.Read(dnsPlan.SingBoxJson)["dns"]),"strategy","")=="ipv4_only","Native subscription DNS respects disabled IPv6 in sing-box");
Assert(ProviderRouting.S(Obj(Json.Read(dnsPlan.XrayJson)["dns"]),"queryStrategy","")=="UseIPv4","Xray DNS does not return unusable AAAA in IPv4-only TUN");
Assert(dnsProfile.ProviderConfig==dnsSource,"DNS family override does not modify stored provider config");
dnsSettings.IPv6=true; var dualPlan=Configuration.Build(dnsProfile,dnsSettings,tunnel); Assert(ProviderRouting.S(Obj(Json.Read(dualPlan.XrayJson)["dns"]),"queryStrategy","")!="UseIPv4","Enabling IPv6 preserves provider DNS query strategy");
Assert(L10n.Resolve("auto",new System.Globalization.CultureInfo("ru-RU"))=="ru"&&L10n.Resolve("auto",new System.Globalization.CultureInfo("de-DE"))=="en","System language defaults to Russian or English");
Assert(L10n.Resolve("en",new System.Globalization.CultureInfo("ru-RU"))=="en","Explicit language overrides system");L10n.SetLanguage("en");Assert(L10n.T("Подписки")=="Subscriptions"&&L10n.T("Не подключено")=="Disconnected","English UI catalog resolves labels");L10n.SetLanguage("ru");
var boot=new Settings{AutoConnect=true};var bootSub=new Subscription{Id="boot",AutoUpdate=true};boot.Subscriptions.Add(bootSub);var cached=Subscriptions.Parse(FormatFixtures.XhttpExtra);Subscriptions.Merge(boot,cached,"boot");string cachedId=boot.SelectedId;int fetched=0;
var offline=StartupPreparation.Prepare(boot,delegate(Subscription s){fetched++;throw new System.IO.IOException("offline");},null,System.Threading.CancellationToken.None);
Assert(fetched==1&&offline.Profiles.Count==1&&offline.SelectedId==cachedId,"Startup refresh failure preserves cached selected server");
var bootReload=Json.Serializer().Deserialize<Settings>(Json.Write(offline));Assert(bootReload.AutoConnect&&bootReload.Profiles.Count==1,"Autoconnect and servers survive disk serialization");
var emptyBoot=new Settings();emptyBoot.Subscriptions.Add(new Subscription{Id="new"});var filled=StartupPreparation.Prepare(emptyBoot,delegate(Subscription s){return Subscriptions.Parse(FormatFixtures.XhttpExtra);},null,System.Threading.CancellationToken.None);Assert(filled.Profiles.Count==1&&filled.SelectedId!=null&&emptyBoot.Profiles.Count==0,"Startup refresh populates an empty list without mutating saved state prematurely");
var partial=StartupPreparation.Prepare(boot,delegate(Subscription s){var r=Subscriptions.Parse(FormatFixtures.XhttpExtra);r.Errors.Add("failed row");return r;},null,System.Threading.CancellationToken.None);Assert(partial.Profiles[0].Id==cachedId,"Partial startup update cannot remove cached servers");
var cts=new System.Threading.CancellationTokenSource();cts.Cancel();bool cancelled=false;try{StartupPreparation.Prepare(boot,delegate(Subscription s){throw new Exception("Must not fetch");},null,cts.Token);}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"Startup cancellation prevents fetching and connection");
string release=Json.Write(Json.Obj("tag_name","v9.0.0","assets",new[]{Json.Obj("name","Basalt-9.0.0-test.zip","size",1024,"browser_download_url",Updates.Repository+"/releases/download/v9.0.0/Basalt-9.0.0-test.zip"),Json.Obj("name","Basalt-9.0.0-test-x86.zip","size",512,"browser_download_url",Updates.Repository+"/releases/download/v9.0.0/Basalt-9.0.0-test-x86.zip"),Json.Obj("name","SHA256SUMS.txt","browser_download_url",Updates.Repository+"/releases/download/v9.0.0/SHA256SUMS.txt")}));
Assert(Updates.Parse(release,new Version(0,6,7),true).FileName.EndsWith("-x86.zip"),"Updater selects x86 package");Assert(Updates.Parse(release,new Version(9,0,0),false)==null,"Updater ignores current version");Reject(delegate{Updates.Parse(release.Replace(Updates.Repository,"https://example.org/evil"),new Version(0,0,1),false);},"Updater rejects foreign asset URLs");
Assert(Updates.ExpectedHash(new string('a',64)+"  Basalt.zip","Basalt.zip")==new string('a',64),"Updater parses exact asset checksum");Reject(delegate{Updates.ExpectedHash(new string('a',64)+"  Other.zip","Basalt.zip");},"Updater rejects missing checksum");
Console.WriteLine(count + " checks passed."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
