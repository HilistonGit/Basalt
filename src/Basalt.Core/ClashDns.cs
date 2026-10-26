using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
namespace Basalt.Core {
 static class ClashDns {
  static string S(Dictionary<string,object> c,string k,string f) { return ProviderRouting.S(c,k,f); }
  static Dictionary<string,object> Server(string value,string tag) {
   if(value.Contains("#")) throw new FormatException(L10n.T("Clash DNS: параметры после # требуют отдельной обработки."));
   if(value=="system") return Json.Obj("type","local","tag",tag);
   IPAddress ip; if(IPAddress.TryParse(value,out ip)) return Json.Obj("type","udp","tag",tag,"server",value);
   Uri uri; if(!Uri.TryCreate(value.Contains("://")?value:"udp://"+value,UriKind.Absolute,out uri)) throw new FormatException(L10n.T("Некорректный DNS-сервер Clash."));
   string type=uri.Scheme; if(type!="udp"&&type!="tcp"&&type!="tls"&&type!="https") throw new FormatException(L10n.T("Clash DNS: неподдерживаемый транспорт ")+type);
   var result=Json.Obj("type",type,"tag",tag,"server",uri.Host,"detour","__clash_direct");
   if(!IPAddress.TryParse(uri.Host,out ip)) result["domain_resolver"]="__clash_bootstrap";
   if(uri.Port>0) result["server_port"]=uri.Port; if(type=="https") result["path"]=uri.PathAndQuery;
   return result;
  }
  static IEnumerable<string> Strings(object value) { var list=value as IList; if(list==null) { yield return System.Convert.ToString(value); yield break; } foreach(var v in list) yield return System.Convert.ToString(v); }
  static Dictionary<string,object> Match(string name,string server) {
   var r=Json.Obj("server",server);
   if(name.StartsWith("geosite:")||name.StartsWith("rule-set:")) throw new FormatException(L10n.T("Clash DNS nameserver-policy: нужен явный домен вместо внешней категории."));
   if(name.StartsWith("+.")||name.StartsWith("*.")) r["domain_suffix"]=new[]{name.Substring(2)}; else r["domain"]=new[]{name}; return r;
  }
  public static Dictionary<string,object> Convert(Dictionary<string,object> dns) {
   if(ProviderRouting.List(dns,"fallback").Count>0) throw new FormatException(L10n.T("Clash fallback DNS требует переноса fallback-filter; импорт остановлен."));
   if(S(dns,"respect-rules","false").Equals("true",StringComparison.OrdinalIgnoreCase)) throw new FormatException(L10n.T("Clash respect-rules DNS пока не поддерживается."));
   string bootstrap="1.1.1.1"; var defaults=ProviderRouting.List(dns,"default-nameserver"); if(defaults.Count>0) bootstrap=System.Convert.ToString(defaults[0]);
   IPAddress ip; if(!IPAddress.TryParse(bootstrap,out ip)) throw new FormatException(L10n.T("Clash default-nameserver: нужен IP для bootstrap."));
   var servers=new List<object>{Json.Obj("type","udp","tag","__clash_bootstrap","server",bootstrap,"detour","__clash_direct")};
   var rules=new List<object>(); string final="__clash_bootstrap"; int index=0;
   foreach(var value in ProviderRouting.List(dns,"nameserver")) { string tag="__clash_dns"+(index++); servers.Add(Server(System.Convert.ToString(value),tag)); if(index==1) final=tag; }
   object policies; if(dns.TryGetValue("nameserver-policy",out policies)) foreach(var pair in ProviderRouting.Map(policies)) { string tag="__clash_dns"+(index++); string first=null; foreach(string value in Strings(pair.Value)) { if(first==null) first=value; } if(first==null) throw new FormatException(L10n.T("Пустая DNS policy.")); servers.Add(Server(first,tag)); foreach(var name in pair.Key.Split(',')) rules.Add(Match(name,tag)); }
   if(S(dns,"enhanced-mode","redir-host")=="fake-ip") {
    foreach(var value in ProviderRouting.List(dns,"fake-ip-filter")) rules.Add(Match(System.Convert.ToString(value),final));
    servers.Add(Json.Obj("type","fakeip","tag","__clash_fakeip","inet4_range",S(dns,"fake-ip-range","198.18.0.0/15"))); rules.Add(Json.Obj("query_type",new[]{"A"},"server","__clash_fakeip"));
   }
   return Json.Obj("servers",servers,"rules",rules,"final",final,"reverse_mapping",true,"strategy",S(dns,"ipv6","false").Equals("true",StringComparison.OrdinalIgnoreCase)?"prefer_ipv4":"ipv4_only");
  }
 }
}
