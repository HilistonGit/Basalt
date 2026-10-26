using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace Basalt.Core {
 public sealed class ReleaseUpdate {
  public string Version,PageUrl,DownloadUrl,ChecksumUrl,FileName; public long Size;
 }
 public static class Updates {
  public const string Repository="https://github.com/HilistonGit/Basalt";
  public const string Api="https://api.github.com/repos/HilistonGit/Basalt/releases/latest";
  public static ReleaseUpdate Parse(string json,Version current,bool x86) {
   var root=Json.Read(json); if(ProviderRouting.S(root,"draft","false").Equals("true",StringComparison.OrdinalIgnoreCase)||ProviderRouting.S(root,"prerelease","false").Equals("true",StringComparison.OrdinalIgnoreCase))return null;
   string tag=ProviderRouting.S(root,"tag_name",""); if(!Regex.IsMatch(tag,@"^v?\d+\.\d+\.\d+$"))throw new FormatException("Invalid release version."); Version version; if(!Version.TryParse(tag.TrimStart('v'),out version))throw new FormatException("Invalid release version."); if(version<=new Version(current.Major,current.Minor,Math.Max(0,current.Build)))return null;
   string filename="Basalt-"+version+"-test"+(x86?"-x86":"")+".zip"; var result=new ReleaseUpdate {Version=version.ToString(),FileName=filename,PageUrl=Repository+"/releases/tag/"+tag};
   foreach(var value in ProviderRouting.List(root,"assets")) {var asset=ProviderRouting.Map(value);string name=ProviderRouting.S(asset,"name","");if(name==filename){result.DownloadUrl=ProviderRouting.S(asset,"browser_download_url",""); long size;if(!Int64.TryParse(ProviderRouting.S(asset,"size","0"),out size)||size<=0||size>1024L*1024*1024)throw new FormatException("Invalid package size."); result.Size=size;} if(name=="SHA256SUMS.txt")result.ChecksumUrl=ProviderRouting.S(asset,"browser_download_url","");}
   if(result.DownloadUrl==null||result.ChecksumUrl==null)throw new FormatException("Release package or SHA256SUMS.txt is missing.");
   ValidateAsset(result.DownloadUrl,tag,result.FileName);ValidateAsset(result.ChecksumUrl,tag,"SHA256SUMS.txt");return result;
  }
  static void ValidateAsset(string url,string tag,string file){if(url!=Repository+"/releases/download/"+tag+"/"+file)throw new FormatException("Unexpected release asset URL.");}
  static bool Allowed(Uri u){return u.Scheme=="https"&&u.UserInfo==""&&(u.Host=="api.github.com"||u.Host=="github.com"||u.Host=="release-assets.githubusercontent.com"||u.Host=="objects.githubusercontent.com");}
  static void Fetch(string url,Stream output,long maximum,Action<long> progress) {
   var uri=new Uri(url); for(int hop=0;hop<6;hop++) {if(!Allowed(uri))throw new IOException("Untrusted update URL.");var req=(HttpWebRequest)WebRequest.Create(uri);req.UserAgent="Basalt-Updater";req.Accept="application/vnd.github+json";req.AllowAutoRedirect=false;req.Timeout=15000;req.ReadWriteTimeout=15000;
    using(var response=(HttpWebResponse)req.GetResponse()){int status=(int)response.StatusCode;if(status>=300&&status<400){uri=new Uri(uri,response.Headers["Location"]);continue;}if(response.ContentLength>maximum)throw new IOException("Update response exceeds size limit.");using(var input=response.GetResponseStream()){byte[] bytes=new byte[65536];long total=0;int n;while((n=input.Read(bytes,0,bytes.Length))>0){total+=n;if(total>maximum)throw new IOException("Update response exceeds size limit.");output.Write(bytes,0,n);if(progress!=null)progress(total);}return;}}
   }throw new IOException("Too many update redirects.");
  }
  static string FetchText(string url,int limit){using(var stream=new MemoryStream()){Fetch(url,stream,limit,null);return System.Text.Encoding.UTF8.GetString(stream.ToArray());}}
  public static ReleaseUpdate Check(Version current,bool x86){try{return Parse(FetchText(Api,2*1024*1024),current,x86);}catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return null;throw;}}
  public static string ExpectedHash(string text,string filename){string found=null;foreach(string line in text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var match=Regex.Match(line,@"^([a-fA-F0-9]{64})\s+\*?(.+)$");if(match.Success&&match.Groups[2].Value==filename){if(found!=null)throw new FormatException("Duplicate checksum.");found=match.Groups[1].Value;}}if(found==null)throw new FormatException("Package checksum missing.");return found;}
  public static string Download(ReleaseUpdate release,string directory,Action<long> progress){
   // Revalidate mutable metadata before touching disk. Only our fixed portable package names are accepted.
   if(!Regex.IsMatch(release.Version,@"^\d+\.\d+\.\d+$") || (release.FileName!="Basalt-"+release.Version+"-test.zip" && release.FileName!="Basalt-"+release.Version+"-test-x86.zip"))throw new FormatException("Invalid package name.");
   var uri=new Uri(release.DownloadUrl);string tag=uri.Segments[uri.Segments.Length-2].TrimEnd('/');ValidateAsset(release.DownloadUrl,tag,release.FileName);ValidateAsset(release.ChecksumUrl,tag,"SHA256SUMS.txt");
   if(release.Size<=0||release.Size>1024L*1024*1024)throw new FormatException("Invalid package size.");
   string expected=ExpectedHash(FetchText(release.ChecksumUrl,65536),release.FileName);Directory.CreateDirectory(directory);string output=Path.Combine(directory,release.FileName), temp=output+"."+Guid.NewGuid().ToString("N")+".partial";
   try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Fetch(release.DownloadUrl,file,release.Size,progress);if(file.Length!=release.Size)throw new IOException("Incomplete update download.");}using(var file=File.OpenRead(temp))using(var sha=SHA256.Create())if(!String.Equals(BitConverter.ToString(sha.ComputeHash(file)).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase))throw new IOException("SHA-256 verification failed.");if(File.Exists(output))File.Delete(output);File.Move(temp,output);return output;}finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
}
