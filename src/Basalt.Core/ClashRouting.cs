using System;
using System.Collections;
using System.Collections.Generic;
namespace Basalt.Core
{
 public static class ClashRouting
 {
  static Dictionary<string,object> M(object o) { return ProviderRouting.Map(o); }
  static string S(Dictionary<string,object> o,string k,string f) { return ProviderRouting.S(o,k,f); }
  static string Target(string target) { return target=="DIRECT"?"__clash_direct":target; }
  public static Dictionary<string,object> Convert(Dictionary<string,object> config,string selected)
  {
   var outs=new List<object>(); foreach(var node in ProviderRouting.List(config,"proxies")) outs.Add(StructuredSubscriptions.Clash(M(node)));
   outs.Add(Json.Obj("tag","__clash_direct","type","direct"));
   foreach(var item in ProviderRouting.List(config,"proxy-groups")) {
    var group=M(item); string type=S(group,"type","select"); if(type!="select" && type!="url-test" && type!="fallback") throw new FormatException(L10n.T("Clash: группа ")+type+L10n.T(" пока не поддерживается."));
    if(group.ContainsKey("use")) throw new FormatException(L10n.T("Clash proxy-providers не загружены; импорт остановлен."));
    var nodes=new List<string>(); foreach(var value in ProviderRouting.List(group,"proxies")) { string name=System.Convert.ToString(value); if(name=="REJECT") throw new FormatException(L10n.T("Clash REJECT внутри selector не поддерживается.")); nodes.Add(Target(name)); }
    if(nodes.Count==0) throw new FormatException(L10n.T("Clash: пустая группа."));
    var g=Json.Obj("tag",S(group,"name",""),"type",type=="url-test"?"urltest":"selector","outbounds",nodes);
    if(type=="fallback") throw new FormatException(L10n.T("Clash fallback: требуется явный selector/url-test для сохранения семантики."));
    if(type=="select") g["default"]=nodes.Contains(selected)?selected:nodes[0]; else { g["url"]=S(group,"url","https://www.gstatic.com/generate_204"); g["interval"]=S(group,"interval","300")+"s"; }
    outs.Add(g);
   }
   var rules=new List<object>(); var sets=new List<object>();
   foreach(var raw in ProviderRouting.List(config,"rules")) {
    string[] parts=System.Convert.ToString(raw).Split(','); if(parts.Length<2) throw new FormatException(L10n.T("Некорректное правило Clash."));
    string kind=parts[0].Trim().ToUpperInvariant(); bool match=kind=="MATCH"; if(!match && parts.Length<3) throw new FormatException(L10n.T("Clash: пропущено направление."));
    string value=parts[1].Trim(), target=(match?parts[1]:parts[2]).Trim();
    var r=Json.Obj();
    if(target=="REJECT" || target=="REJECT-DROP") { r["action"]="reject"; if(target=="REJECT-DROP") r["method"]="drop"; } else r["outbound"]=Target(target);
    if(parts.Length>3 && (parts.Length!=4 || parts[3].Trim()!="no-resolve")) throw new FormatException(L10n.T("Clash: неизвестный модификатор правила."));
    if ((kind=="IP-CIDR" || kind=="IP-CIDR6" || kind=="GEOIP") && parts.Length==3) rules.Add(Json.Obj("action","resolve"));
    switch(kind) {
     case "MATCH": break;
     case "DOMAIN": r["domain"]=new[]{value}; break;
     case "DOMAIN-SUFFIX": r["domain_suffix"]=new[]{value}; break;
     case "DOMAIN-KEYWORD": r["domain_keyword"]=new[]{value}; break;
     case "DOMAIN-REGEX": r["domain_regex"]=new[]{value}; break;
     case "IP-CIDR": case "IP-CIDR6": r["ip_cidr"]=new[]{value}; break;
     case "SRC-IP-CIDR": r["source_ip_cidr"]=new[]{value}; break;
     case "NETWORK": r["network"]=value.ToLowerInvariant(); break;
     case "PROCESS-NAME": r["process_name"]=new[]{value}; break;
     case "PROCESS-PATH": r["process_path"]=new[]{value}; break;
     case "DST-PORT": case "SRC-PORT": { var ports=new List<int>(); var ranges=new List<string>(); foreach(string p in value.Split('/')) { int n; if(Int32.TryParse(p,out n)) ports.Add(n); else ranges.Add(p.Replace('-',':')); } string prefix=kind=="SRC-PORT"?"source_":""; if(ports.Count>0) r[prefix+"port"]=ports; if(ranges.Count>0) r[prefix+"port_range"]=ranges; break; }
     case "GEOIP": case "GEOSITE": {
      if(kind=="GEOIP" && value.Equals("private",StringComparison.OrdinalIgnoreCase)) { r["ip_is_private"]=true; break; }
      if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"^[a-zA-Z0-9_!@.\-]+$")) throw new FormatException(L10n.T("Clash: некорректная geo-категория."));
      string tag="geo-"+kind.ToLowerInvariant()+"-"+value.ToLowerInvariant(); r["rule_set"]=new[]{tag};
      bool exists=false; foreach(var set in sets) if(S(M(set),"tag","")==tag) exists=true;
      if(!exists) sets.Add(Json.Obj("type","remote","tag",tag,"format","binary","url","https://raw.githubusercontent.com/MetaCubeX/meta-rules-dat/sing/geo/"+kind.ToLowerInvariant()+"/"+value.ToLowerInvariant()+".srs","download_detour","__clash_direct")); break;
     }
     case "RULE-SET": {
      object providers; if(!config.TryGetValue("rule-providers",out providers)) throw new FormatException(L10n.T("Clash: отсутствует rule-provider."));
      object spec; if(!M(providers).TryGetValue(value,out spec)) throw new FormatException(L10n.T("Clash: неизвестный rule-provider.")); var provider=M(spec);
      object payload; if(!provider.TryGetValue("basalt-payload",out payload)) throw new FormatException(L10n.T("Clash: rule-provider ещё не загружен."));
      var inner=new List<object>(); string behavior=S(provider,"behavior","domain");
      foreach(var entry in (IList)payload) { string pattern=System.Convert.ToString(entry); if(behavior=="domain") { if(pattern.StartsWith("+.")) inner.Add(Json.Obj("domain_suffix",new[]{pattern.Substring(2)})); else if(pattern.StartsWith("*.")) inner.Add(Json.Obj("domain_suffix",new[]{pattern.Substring(2)})); else inner.Add(Json.Obj("domain",new[]{pattern})); } else if(behavior=="ipcidr") inner.Add(Json.Obj("ip_cidr",new[]{pattern})); else throw new FormatException(L10n.T("Clash classical rule-provider пока не поддерживается.")); }
      r["type"]="logical"; r["mode"]="or"; r["rules"]=inner; break;
     }
     default: throw new FormatException(L10n.T("Clash: неподдерживаемое правило ")+kind);
    }
    rules.Add(r);
   }
   object dnsConfig; var dns = config.TryGetValue("dns",out dnsConfig) ? ClashDns.Convert(M(dnsConfig)) : null;
   var result=Json.Obj("outbounds",outs,"route",Json.Obj("rules",rules,"rule_set",sets,"final",selected)); if(dns!=null) result["dns"]=dns; return result;
  }
 }
}
