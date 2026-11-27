using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Reflection;
namespace ProjectPoeLedger;
internal static class ReleaseCheck {
 public static Version Current => typeof(ReleaseCheck).Assembly.GetName().Version ?? new Version(0,51,0,0);
 public sealed record Release(Version Version,string Notes,Uri? Download,string? Sha256=null,long? Size=null);
 public static Uri Https(string text) {
  if(!Uri.TryCreate(text.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps||!string.IsNullOrEmpty(uri.UserInfo)||string.IsNullOrEmpty(uri.Host)) throw new InvalidDataException("Enter an absolute HTTPS URL without credentials.");
  return uri;
 }
 public static Release Parse(string json) {
  using var doc=JsonDocument.Parse(json);var r=doc.RootElement;
  if(r.ValueKind!=JsonValueKind.Object||!r.TryGetProperty("version",out var v)||v.ValueKind!=JsonValueKind.String||!Version.TryParse(v.GetString(),out var version)) throw new InvalidDataException("Manifest must contain a numeric version, e.g. 0.38.0.");
  version=new Version(version.Major,version.Minor,Math.Max(0,version.Build),Math.Max(0,version.Revision));
  string notes=r.TryGetProperty("notes",out var n)&&n.ValueKind==JsonValueKind.String?n.GetString()??"":"";
  Uri? download=null;if(r.TryGetProperty("downloadUrl",out var d)&&d.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(d.GetString()))download=Https(d.GetString()!);
  string? hash=r.TryGetProperty("sha256",out var h)&&h.ValueKind==JsonValueKind.String?h.GetString():null;
  long? size=r.TryGetProperty("size",out var z)&&z.TryGetInt64(out var bytes)?bytes:null;
  if(hash!=null&&(hash.Length!=64||!hash.All(Uri.IsHexDigit)))throw new InvalidDataException("Invalid SHA-256.");
  return new Release(version,notes,download,hash,size);
 }
 static async Task<HttpResponseMessage> Get(HttpClient http,Uri uri,CancellationToken ct){
  for(int i=0;i<8;i++){
   var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);
   if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308){var next=response.Headers.Location;response.Dispose();if(next==null)throw new InvalidDataException("Missing redirect location.");uri=Https(new Uri(uri,next).AbsoluteUri);continue;}
   try{response.EnsureSuccessStatusCode();return response;}catch{response.Dispose();throw;}
  }throw new InvalidDataException("Too many redirects.");
 }
 static HttpClient Client(){var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};http.DefaultRequestHeaders.UserAgent.ParseAdd("PoeLedger/0.52.0");return http;}
 public static async Task<Release> Fetch(string url){using var http=Client();using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(30));using var response=await Get(http,Https(url),cts.Token);using var input=await response.Content.ReadAsStreamAsync(cts.Token);using var output=new MemoryStream();var buffer=new byte[8192];int n;while((n=await input.ReadAsync(buffer.AsMemory(),cts.Token))>0){if(output.Length+n>131072)throw new InvalidDataException("Manifest too large.");output.Write(buffer,0,n);}return Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()));}
 public static async Task<string> DownloadInstaller(Release r,IProgress<int> progress,CancellationToken ct){
  if(r.Version<=Current||r.Download==null||r.Sha256==null||r.Size is null or <=0 or >1073741824)throw new InvalidDataException("A newer release with HTTPS installer, SHA-256 and size is required.");
  var folder=Path.Combine(Program.DataDir,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var drive=new DriveInfo(Path.GetPathRoot(folder)!);if(drive.AvailableFreeSpace<r.Size.Value+268435456L){Directory.Delete(folder);throw new IOException("Insufficient free disk space for update.");}var path=Path.Combine(folder,"PoeLedger-Setup.exe");
  try{using var http=Client();using var response=await Get(http,r.Download,ct);using var input=await response.Content.ReadAsStreamAsync(ct);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long total=0;var buffer=new byte[65536];int n;
   using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){while((n=await input.ReadAsync(buffer.AsMemory(),ct))>0){total+=n;if(total>r.Size.Value)throw new InvalidDataException("Installer exceeds expected size.");hash.AppendData(buffer,0,n);await file.WriteAsync(buffer.AsMemory(0,n),ct);progress.Report((int)(100*total/r.Size.Value));}}
   if(total!=r.Size.Value||!Convert.ToHexString(hash.GetHashAndReset()).Equals(r.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Installer checksum or size mismatch.");return path;
  }catch{try{Directory.Delete(folder,true);}catch{}throw;}
 }
 public static void LaunchAfterExit(string path,string hash){
  var script=Path.Combine(AppContext.BaseDirectory,"ApplyUpdate.ps1");if(!File.Exists(script))throw new FileNotFoundException("Update helper missing.",script);
  var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false};foreach(var arg in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-ParentId",Environment.ProcessId.ToString(),"-Installer",path,"-Sha256",hash})info.ArgumentList.Add(arg);Process.Start(info);
 }
}
