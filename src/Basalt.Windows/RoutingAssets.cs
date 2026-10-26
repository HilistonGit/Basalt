using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Basalt.Core;
namespace Basalt.Windows {
 static class RoutingAssets {
  static string Hash(byte[] b) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b)).Replace("-","").ToLowerInvariant(); }
  static byte[] Download(string address,int limit) {
   Uri url; if(!Uri.TryCreate(address,UriKind.Absolute,out url)||url.Scheme!="https"||url.UserInfo!="") throw new FormatException(L10n.T("Файлы маршрутизации требуют HTTPS."));
   for(int hop=0;hop<6;hop++) { var request=(HttpWebRequest)WebRequest.Create(url); request.Proxy=null; request.AllowAutoRedirect=false; request.Timeout=30000; request.ReadWriteTimeout=30000;
    using(var response=(HttpWebResponse)request.GetResponse()) {
     if((int)response.StatusCode>=300&&(int)response.StatusCode<400) { url=new Uri(url,response.Headers["Location"]); if(url.Scheme!="https"||url.UserInfo!="") throw new IOException(L10n.T("Небезопасное перенаправление geodata.")); continue; }
     using(var input=response.GetResponseStream()) using(var output=new MemoryStream()) { byte[] buffer=new byte[65536]; int n; while((n=input.Read(buffer,0,buffer.Length))>0) { if(output.Length+n>limit) throw new IOException(L10n.T("Файл маршрутизации превышает лимит.")); output.Write(buffer,0,n); } return output.ToArray(); }
    }
   } throw new IOException(L10n.T("Слишком много перенаправлений geodata."));
  }
  public static void Prepare(ImportResult result,string dataDirectory) {
   var prepared=new Dictionary<string,string>();
   foreach(var p in result.Profiles) {
    if(String.IsNullOrEmpty(p.ProviderConfig)) continue;
    var config=Json.Read(p.ProviderConfig);
    if(p.ProviderFormat=="clash") {
     object providers; if(config.TryGetValue("rule-providers",out providers)) foreach(var item in ProviderRouting.Map(providers)) {
      var spec=ProviderRouting.Map(item.Value); if(ProviderRouting.S(spec,"type","")!="http") throw new FormatException(L10n.T("Clash rule-provider требует http и HTTPS URL."));
      string format=ProviderRouting.S(spec,"format","yaml"), text=Encoding.UTF8.GetString(Download(ProviderRouting.S(spec,"url",""),4*1024*1024));
      if(format=="text") spec["basalt-payload"]=text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
      else if(format=="yaml") spec["basalt-payload"]=ProviderRouting.Map(BoundedYaml.Read(text))["payload"];
      else throw new FormatException(L10n.T("Clash MRS: нужен YAML/text provider."));
     } p.ProviderConfig=Json.Write(config); PrepareSets(p,ClashRouting.Convert(config,p.SourceTag),dataDirectory,prepared); continue;
    }
    if(p.ProviderFormat=="sing-box") { PrepareSets(p,config,dataDirectory,prepared); continue; }
    if(p.ProviderFormat!="happ") { if(p.ProviderFormat!="xray" || String.IsNullOrEmpty(p.HappRoutingConfig)) continue; config=Json.Read(p.HappRoutingConfig); }
    string ip=ProviderRouting.S(config,"Geoipurl",""), site=ProviderRouting.S(config,"Geositeurl","");
    bool needIp=p.ProviderConfig.Contains("geoip:"), needSite=p.ProviderConfig.Contains("geosite:");
    if(!needIp&&!needSite) continue; if(ip==""&&site=="") continue;
    string key=Hash(Encoding.UTF8.GetBytes(ip+"\n"+site+"\n"+ProviderRouting.S(config,"LastUpdated","")+"\n"+(DateTime.UtcNow.Date.Ticks/TimeSpan.TicksPerDay/7))), folder;
    if(prepared.TryGetValue(key,out folder)) { p.RouteAssetDirectory=folder; continue; }
    folder=Path.Combine(dataDirectory,"routing-assets",key); Directory.CreateDirectory(folder); if(File.Exists(Path.Combine(folder,"hashes.json"))) Verify(folder); var hashes=new Dictionary<string,object>();
    foreach(string name in new[]{"geoip","geosite"}) { string url=name=="geoip"?ip:site; bool needed=name=="geoip"?needIp:needSite; string path=Path.Combine(folder,name+".dat");
     if(!File.Exists(path)) { if(needed&&url!="") SettingsStore.AtomicWrite(path,Download(url,64*1024*1024)); else File.Copy(Path.Combine(CoreCatalog.DirectoryPath,name+".dat"),path); }
     hashes[name+".dat"]=Hash(File.ReadAllBytes(path));
    }
    SettingsStore.AtomicWrite(Path.Combine(folder,"hashes.json"),Encoding.UTF8.GetBytes(Json.Write(hashes))); prepared[key]=folder; p.RouteAssetDirectory=folder;
   }
  }
  static void PrepareSets(Profile p,Dictionary<string,object> config,string root,Dictionary<string,string> prepared) {
   object route; if(!config.TryGetValue("route",out route)) return;
   foreach(var item in ProviderRouting.List(ProviderRouting.Map(route),"rule_set")) {
    var set=ProviderRouting.Map(item); if(ProviderRouting.S(set,"type","")!="remote") continue;
    string url=ProviderRouting.S(set,"url",""),path; if(!prepared.TryGetValue(url,out path)) {
     byte[] bytes=Download(url,64*1024*1024); string folder=Path.Combine(root,"routing-assets","rules"); Directory.CreateDirectory(folder);
     path=Path.Combine(folder,Hash(bytes)+(ProviderRouting.S(set,"format","binary")=="source"?".json":".srs")); if(!File.Exists(path)) SettingsStore.AtomicWrite(path,bytes); prepared[url]=path;
    }
    if(p.ManagedRuleSets==null) p.ManagedRuleSets=new Dictionary<string,string>(); p.ManagedRuleSets[ProviderRouting.S(set,"tag","")]=path;
   }
  }
  public static void VerifyRules(Profile p) { if(p.ManagedRuleSets==null) return; foreach(var path in p.ManagedRuleSets.Values) if(Hash(File.ReadAllBytes(path))!=Path.GetFileNameWithoutExtension(path)) throw new IOException(L10n.T("Повреждён rule-set подписки.")); }
  public static void Verify(string directory) { if(String.IsNullOrEmpty(directory)) return; var hashes=Json.Read(File.ReadAllText(Path.Combine(directory,"hashes.json"))); foreach(string name in new[]{"geoip.dat","geosite.dat"}) if(!hashes.ContainsKey(name)||Hash(File.ReadAllBytes(Path.Combine(directory,name)))!=(string)hashes[name]) throw new IOException(L10n.T("Повреждены файлы маршрутизации подписки.")); }
 }
}
