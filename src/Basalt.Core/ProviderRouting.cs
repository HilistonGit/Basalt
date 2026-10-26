using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
namespace Basalt.Core
{
 public static class ProviderRouting
 {
  public static Profile Resolve(Profile p,Settings s) {
   if(!String.IsNullOrEmpty(p.ProviderConfig) || String.IsNullOrEmpty(p.ProviderDocumentId)) return p;
   string document; if(s.RouteDocuments==null || !s.RouteDocuments.TryGetValue(p.ProviderDocumentId,out document)) throw new FormatException(L10n.T("Отсутствует сохранённая маршрутизация подписки. Обновите подписку."));
   var copy=Json.Serializer().Deserialize<Profile>(Json.Write(p)); copy.ProviderConfig=document; return copy;
  }
  public static void StoreDocuments(Settings settings,ImportResult result) {
   if(settings.RouteDocuments==null) settings.RouteDocuments=new Dictionary<string,string>();
   foreach(var p in result.Profiles) { if(String.IsNullOrEmpty(p.ProviderConfig)) continue;
    using(var sha=System.Security.Cryptography.SHA256.Create()) p.ProviderDocumentId=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(p.ProviderConfig))).Replace("-","");
    settings.RouteDocuments[p.ProviderDocumentId]=p.ProviderConfig; p.ProviderConfig=null;
   }
   var used=new HashSet<string>(); foreach(var p in settings.Profiles) if(p.ProviderDocumentId!=null) used.Add(p.ProviderDocumentId);
   foreach(var key in new List<string>(settings.RouteDocuments.Keys)) if(!used.Contains(key)) settings.RouteDocuments.Remove(key);
  }
  public static Dictionary<string,object> Map(object value) { var m=value as Dictionary<string,object>; if(m==null) throw new FormatException(L10n.T("Маршрутизация: ожидался JSON-объект.")); return m; }
  public static string S(Dictionary<string,object> m,string k,string fallback) { object v; return m.TryGetValue(k,out v) && v!=null ? Convert.ToString(v) : fallback; }
  public static IList List(Dictionary<string,object> m,string k) { object v; if(!m.TryGetValue(k,out v)) return new object[0]; var a=v as IList; if(a==null) throw new FormatException(L10n.T("Маршрутизация: нужен список ")+k); return a; }
  public static void Attach(Profile p,Dictionary<string,object> config,string format,string tag)
  {
   p.SourceTag=tag;
   if(!config.ContainsKey("routing") && !config.ContainsKey("route") && !config.ContainsKey("rules") && !config.ContainsKey("dns")) return;
   p.ProviderFormat=format; p.ProviderConfig=Json.Write(config);
   if(String.IsNullOrEmpty(tag)) throw new FormatException(L10n.T("Маршрутизация подписки требует уникальные tag/name серверов."));
  }
  static bool HasNativeRules(Profile p) {
   if(String.IsNullOrEmpty(p.ProviderConfig) || p.ProviderFormat=="happ") return false;
   var c=Json.Read(p.ProviderConfig); object route;
   if(p.ProviderFormat=="clash") return List(c,"rules").Count>0;
   return c.TryGetValue(p.ProviderFormat=="xray"?"routing":"route",out route) && List(Map(route),"rules").Count>0;
  }
  public static void AttachHapp(ImportResult result,string link)
  {
   try {
    if(link=="happ://routing/off") { foreach(var p in result.Profiles) { if(p.ProviderFormat=="happ") { p.ProviderConfig=null; p.ProviderFormat=null; } p.HappRoutingConfig=null; } return; }
    string prefix=link.StartsWith("happ://routing/onadd/") ? "happ://routing/onadd/" : "happ://routing/add/";
    if(!link.StartsWith(prefix)) throw new FormatException(L10n.T("Неизвестная команда маршрутизации Happ."));
    string b=Uri.UnescapeDataString(link.Substring(prefix.Length)).Replace('-','+').Replace('_','/');
    string json=Encoding.UTF8.GetString(Convert.FromBase64String(b.PadRight((b.Length+3)/4*4,'='))); var config=Json.Read(json);
    if(config==null) throw new FormatException(L10n.T("Пустой профиль маршрутизации Happ."));
    foreach(var p in result.Profiles) { p.HappRoutingConfig=Json.Write(config); if(HasNativeRules(p)) continue; p.ProviderFormat="happ"; p.ProviderConfig=p.HappRoutingConfig; }
   } catch(Exception e) { if(!(e is FormatException || e is ArgumentException)) throw; result.Errors.Add(L10n.T("Некорректный профиль маршрутизации Happ.")); }
  }
  public static IEnumerable<string> Servers(Profile p)
  {
   yield return p.Server;
   if(String.IsNullOrEmpty(p.ProviderConfig) || p.ProviderFormat=="happ") yield break;
   var c=Json.Read(p.ProviderConfig);
   foreach(var value in List(c,p.ProviderFormat=="clash" ? "proxies":"outbounds")) {
    var o=Map(value); string host=S(o,"server",""); if(host!="") { yield return host; continue; }
    object parameters; if(!o.TryGetValue("settings",out parameters)) continue; var s=Map(parameters);
    host=S(s,"address",""); if(host!="") yield return host;
    foreach(string key in new[]{"vnext","servers"}) foreach(var v in List(s,key)) { host=S(Map(v),"address",""); if(host!="") yield return host; }
   }
  }
  static void Pin(Dictionary<string,object> outbound,TunnelEnvironment tunnel)
  {
   if(tunnel==null || tunnel.ResolvedServers==null) return;
   string ip, host=S(outbound,"server",""); if(tunnel.ResolvedServers.TryGetValue(host,out ip)) { object tls; if(outbound.TryGetValue("tls",out tls) && !Map(tls).ContainsKey("server_name")) Map(tls)["server_name"]=host; outbound["server"]=ip; }
   object parameters; if(!outbound.TryGetValue("settings",out parameters)) return; var s=Map(parameters); var servers=new List<object>();
   if(s.ContainsKey("address")) servers.Add(s); foreach(var key in new[]{"vnext","servers"}) foreach(var item in List(s,key)) servers.Add(item);
   foreach(var item in servers) { var server=Map(item); host=S(server,"address",""); if(!tunnel.ResolvedServers.TryGetValue(host,out ip)) continue;
    object stream; if(outbound.TryGetValue("streamSettings",out stream)) { object tls; if(Map(stream).TryGetValue("tlsSettings",out tls) && String.IsNullOrEmpty(S(Map(tls),"serverName",""))) Map(tls)["serverName"]=host; }
    server["address"]=ip;
   }
  }
  static void LocalTargets(IList rules,string proxy,string direct,bool xray)
  {
   foreach(var item in rules) { var rule=Map(item); string key=xray?"outboundTag":"outbound", value=S(rule,key,""); if(value=="proxy") rule[key]=proxy; if(value=="direct") rule[key]=direct; }
  }
  public static void Apply(Profile p,Settings settings,TunnelEnvironment tunnel,ConnectionPlan plan)
  {
   if(!settings.UseSubscriptionRouting || String.IsNullOrEmpty(p.ProviderConfig)) return;
   if(tunnel!=null && tunnel.ResolvedServers==null) tunnel=new TunnelEnvironment { InterfaceName=tunnel.InterfaceName, ServerAddresses=tunnel.ServerAddresses, ResolvedServers=new Dictionary<string,string>{{p.Server,tunnel.ServerAddresses[0]}} };
   var source=Json.Read(p.ProviderConfig); var sb=Json.Read(plan.SingBoxJson);
   if(p.ProviderFormat=="happ") { ApplyHapp(p,settings,tunnel,plan,sb,source); return; }
   if(p.ProviderFormat=="clash") source=ClashRouting.Convert(source,p.SourceTag);
   bool xray=p.ProviderFormat=="xray";
   var target=xray?Json.Read(plan.XrayJson):sb;
   if(xray) foreach(string key in new[]{"observatory","burstObservatory","policy","fakedns"}) { object value; if(source.TryGetValue(key,out value)) target[key]=value; }
   var outbounds=new List<object>(); var tags=new HashSet<string>();
   foreach(var value in List(source,"outbounds")) { var o=Map(value); string tag=S(o,"tag",""); if(tag.StartsWith("__basalt") || tag=="" || !tags.Add(tag)) throw new FormatException(L10n.T("Маршрутизация: повторяющийся/служебный tag outbound.")); if(tag==p.SourceTag) { o=Configuration.Outbound(p); o["tag"]=tag; } Pin(o,tunnel); if(tag==p.SourceTag) outbounds.Insert(0,o); else outbounds.Add(o); }
   if(!tags.Contains(p.SourceTag)) throw new FormatException(L10n.T("Маршрутизация: выбранный outbound отсутствует."));
   outbounds.Add(xray?Json.Obj("tag","__basalt_direct","protocol","freedom"):Json.Obj("tag","__basalt_direct","type","direct")); target["outbounds"]=outbounds;
   string routeKey=xray?"routing":"route"; object routeValue; var route=source.TryGetValue(routeKey,out routeValue)?Map(routeValue):Json.Obj();
   var rules=new List<object>();
   if(xray) {
    foreach(var rule in Configuration.Rules(settings.RouteEntries,"unused")) { if(settings.RouteMode==0) break; var xr=Json.Obj("type","field","outboundTag",settings.RouteMode==1?p.SourceTag:"__basalt_direct"); foreach(var pair in rule) if(pair.Key=="ip_cidr") xr["ip"]=pair.Value; else if(pair.Key=="domain") { var domains=new List<string>(); foreach(var v in (IList)pair.Value) domains.Add("full:"+v); xr["domain"]=domains; } else if(pair.Key=="domain_suffix") { var domains=new List<string>(); foreach(var v in (IList)pair.Value) domains.Add("domain:"+v); xr["domain"]=domains; } rules.Add(xr); }
   } else {
    var generated=Map(sb["route"]); var local=List(generated,"rules"); LocalTargets(local,p.SourceTag,"__basalt_direct",false); foreach(var r in local) rules.Add(r);
    route["auto_detect_interface"]=settings.TunMode;
    if(!route.ContainsKey("final")) route["final"]=p.SourceTag;
    if(generated.ContainsKey("default_domain_resolver") && !source.ContainsKey("dns")) route["default_domain_resolver"]=generated["default_domain_resolver"];
    foreach(var item in List(route,"rule_set")) { var set=Map(item); if(S(set,"type","")=="local") throw new FormatException(L10n.T("Подписка не может читать локальные rule-set; используйте HTTPS или inline.")); if(S(set,"type","")=="remote") { Uri url; if(!Uri.TryCreate(S(set,"url",""),UriKind.Absolute,out url) || url.Scheme!="https") throw new FormatException(L10n.T("Rule-set должен использовать HTTPS.")); string path; if(p.ManagedRuleSets!=null && p.ManagedRuleSets.TryGetValue(S(set,"tag",""),out path)) { set["type"]="local"; set["path"]=path; set.Remove("url"); set.Remove("download_detour"); set.Remove("update_interval"); set.Remove("http_client"); } } }
   }
   foreach(var item in List(route,"rules")) { var r=Map(item); if(r.ContainsKey(xray?"inboundTag":"inbound")) throw new FormatException(L10n.T("Правила подписки с привязкой к её inbound требуют перенастройки; импорт остановлен.")); rules.Add(r); }
   route["rules"]=rules; target[routeKey]=route;
   object dns; if(source.TryGetValue("dns",out dns)) { target["dns"]=dns; if(!xray && !route.ContainsKey("default_domain_resolver")) { var dnsServers=List(Map(dns),"servers"); if(dnsServers.Count>0) route["default_domain_resolver"]=S(Map(dnsServers[0]),"tag",""); } }
   if(xray) { plan.XrayJson=Json.Write(target); if(source.ContainsKey("dns")) BridgeDns(sb,target); plan.XrayJson=Json.Write(target); }
   else plan.SingBoxJson=Json.Write(target);
   if(xray) plan.SingBoxJson=Json.Write(sb);
  }
  static void BridgeDns(Dictionary<string,object> sb,Dictionary<string,object> xr)
  {
   Map(sb["route"])["default_domain_resolver"]="dns-direct";
   var outs=new List<object>(); foreach(var o in List(xr,"outbounds")) outs.Add(o); outs.Add(Json.Obj("tag","__basalt_dns","protocol","dns")); xr["outbounds"]=outs;
   var route=Map(xr["routing"]); var rules=new List<object>{Json.Obj("type","field","inboundTag",new[]{"bridge"},"port",53,"network","udp","outboundTag","__basalt_dns")}; foreach(var r in List(route,"rules")) rules.Add(r); route["rules"]=rules;
   sb["dns"]=Json.Obj("servers",new[]{Json.Obj("type","udp","tag","dns-direct","server","1.1.1.1"),Json.Obj("type","udp","tag","dns-remote","server","1.1.1.1","detour","proxy")},"final","dns-remote","reverse_mapping",true);
  }
  static void ApplyHapp(Profile p,Settings settings,TunnelEnvironment tunnel,ConnectionPlan plan,Dictionary<string,object> sb,Dictionary<string,object> happ)
  {
   var rules=new List<object>();
   foreach(string action in new[]{"Block","Direct","Proxy"}) foreach(string field in new[]{"Sites","Ip"}) { var values=List(happ,action+field); if(values.Count==0) continue; rules.Add(Json.Obj("type","field",field=="Sites"?"domain":"ip",values,"outboundTag",action=="Block"?"__basalt_block":action=="Direct"?"__basalt_direct":"proxy")); }
   var local=Configuration.Rules(settings.RouteEntries,settings.RouteMode==1?"proxy":"__basalt_direct");
   if(settings.RouteMode!=0) foreach(var r in local) { var xr=Json.Obj("type","field","outboundTag",r["outbound"]); foreach(var pair in r) { if(pair.Key=="ip_cidr") xr["ip"]=pair.Value; if(pair.Key=="domain" || pair.Key=="domain_suffix") { var v=new List<string>(); foreach(var d in (IList)pair.Value) v.Add((pair.Key=="domain"?"full:":"domain:")+d); xr["domain"]=v; } } rules.Insert(0,xr); }
   bool global=!S(happ,"GlobalProxy","true").Equals("false",StringComparison.OrdinalIgnoreCase);
   // Xray falls back to the first outbound only AFTER IPIfNonMatch resolution. A catch-all rule prevents that second pass.
   Dictionary<string,object> xrConfig;
   if(plan.XrayJson!=null) xrConfig=Json.Read(plan.XrayJson);
   else {
    if(settings.NativeBridgePort==settings.ProxyPort || settings.NativeBridgePort==settings.HttpPort || settings.NativeBridgePort==settings.BridgePort) throw new FormatException(L10n.T("Порт внутреннего моста маршрутизации занят другим inbound."));
    var native=Map(List(sb,"outbounds")[0]); native["tag"]="__basalt_native";
    sb["outbounds"]=new object[]{Json.Obj("type","socks","tag","proxy","server","127.0.0.1","server_port",settings.BridgePort,"version","5"),native,Json.Obj("type","direct","tag","direct")};
    var ins=new List<object>(); foreach(var i in List(sb,"inbounds")) ins.Add(i); ins.Add(Json.Obj("type","socks","tag","__basalt_native_in","listen","127.0.0.1","listen_port",settings.NativeBridgePort)); sb["inbounds"]=ins;
    var sr=Map(sb["route"]); var rr=new List<object>{Json.Obj("inbound",new[]{"__basalt_native_in"},"outbound","__basalt_native")}; foreach(var r in List(sr,"rules")) rr.Add(r); sr["rules"]=rr;
    xrConfig=Json.Obj("inbounds",new[]{Json.Obj("tag","bridge","listen","127.0.0.1","port",settings.BridgePort,"protocol","socks","settings",Json.Obj("udp",true))},"outbounds",new[]{Json.Obj("tag","proxy","protocol","socks","settings",Json.Obj("servers",new[]{Json.Obj("address","127.0.0.1","port",settings.NativeBridgePort)}))});
    plan.NativeBridge=true;
   }
   var outbounds=new List<object>(); foreach(var o in List(xrConfig,"outbounds")) outbounds.Add(o); outbounds.Add(Json.Obj("tag","__basalt_direct","protocol","freedom")); outbounds.Add(Json.Obj("tag","__basalt_block","protocol","blackhole")); if(!global) { var direct=outbounds[outbounds.Count-2]; outbounds.RemoveAt(outbounds.Count-2); outbounds.Insert(0,direct); } xrConfig["outbounds"]=outbounds;
   xrConfig["routing"]=Json.Obj("domainStrategy",S(happ,"DomainStrategy","IPIfNonMatch"),"rules",rules);
   var dns=Json.Obj(); object hosts; if(happ.TryGetValue("DnsHosts",out hosts)) dns["hosts"]=hosts;
   var directDomains=List(happ,"DirectSites");
   string remote=HappDns(happ,"Remote","1.1.1.1"), domestic=HappDns(happ,"Domestic","8.8.8.8");
   dns["servers"]=new object[]{Json.Obj("address",domestic,"domains",directDomains,"skipFallback",true),remote}; dns["queryStrategy"]=settings.IPv6?"UseIP":"UseIPv4"; xrConfig["dns"]=dns;
   if(S(happ,"FakeDNS","false").Equals("true",StringComparison.OrdinalIgnoreCase)) {
    xrConfig["fakedns"]=new[]{Json.Obj("ipPool","198.18.0.0/15","poolSize",65535)};
    dns["servers"]=new object[]{"fakedns",Json.Obj("address",domestic,"domains",directDomains,"skipFallback",true),remote};
    foreach(var value in List(xrConfig,"inbounds")) Map(value)["sniffing"]=Json.Obj("enabled",true,"destOverride",new[]{"fakedns","http","tls"},"routeOnly",false);
   }
   BridgeDns(sb,xrConfig); plan.XrayJson=Json.Write(xrConfig); plan.SingBoxJson=Json.Write(sb);
  }
  static string HappDns(Dictionary<string,object> h,string prefix,string fallback) { string type=S(h,prefix+"DNSType","DoU"); if(type=="DoH") return S(h,prefix+"DNSDomain",""); if(type=="DoU" || type=="" ) return S(h,prefix+"DNSIP",fallback); throw new FormatException(L10n.T("Happ DNS: неизвестный тип ")+type); }
 }
}
