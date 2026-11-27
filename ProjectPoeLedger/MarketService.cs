using System.Net.Http;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace ProjectPoeLedger;
internal sealed class MarketService : IDisposable
{
    static readonly HashSet<string> Allowed = new(){"trade-stats","leagues","exchange/current/overview","stash/current/item/overview","stash/current/currency/overview"};
    readonly HttpClient client = new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(25)};
    readonly SemaphoreSlim gate=new(1,1);
    readonly string cacheFile,cooldownFile;
    readonly Dictionary<string,Entry> cache=new();
    long blockedUntil;
    DateTimeOffset lastRequest=DateTimeOffset.MinValue;
    volatile bool online;
    readonly object cancelLock=new();
    CancellationTokenSource cancelSource=new();
    long requests, failures, hits; int queued; const int MaxQueue50=32;
    internal void Cancel(){online=false;lock(cancelLock){cancelSource.Cancel();cancelSource=new CancellationTokenSource();}}
    CancellationToken CurrentToken(){lock(cancelLock)return cancelSource.Token;}
    readonly AsyncLocal<CancellationToken> requestToken=new();
    internal bool Online {get=>online;set{online=value;if(!value)Cancel();}}
    record Entry(long Time,string Json);
    static long Now=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    internal string Status=>$"Market: {(Online?"enabled":"offline")}\nCooldown remaining: {Math.Max(0,Interlocked.Read(ref blockedUntil)-Now)} seconds\nCache hits: {Interlocked.Read(ref hits)}\nUpstream attempts: {Interlocked.Read(ref requests)}\nFailed operations: {Interlocked.Read(ref failures)}\nQueued: {Volatile.Read(ref queued)}\nCache: {cacheFile}";
    public MarketService(string folder){
        cacheFile=Path.Combine(folder,"desktop-market-cache.json");cooldownFile=Path.Combine(folder,"desktop-cooldown.txt");
        try{var data=JsonSerializer.Deserialize<Dictionary<string,Entry>>(File.ReadAllText(cacheFile));if(data!=null)foreach(var x in data)cache[x.Key]=x.Value;}catch{}
        try{blockedUntil=long.Parse(File.ReadAllText(cooldownFile),CultureInfo.InvariantCulture);}catch{}
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ProjectPoeLedger/0.52.2 (personal desktop economy calculator)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }
    public void Pause(int seconds){long target=Now+seconds,prev;do{prev=Interlocked.Read(ref blockedUntil);if(prev>=target)break;}while(Interlocked.CompareExchange(ref blockedUntil,target,prev)!=prev);try{File.WriteAllText(cooldownFile,Interlocked.Read(ref blockedUntil).ToString(CultureInfo.InvariantCulture));}catch{}}
    void Store(string key,string value){cache[key]=new(Now,value);try{foreach(var k in cache.Where(x=>Now-x.Value.Time>86400).Select(x=>x.Key).ToArray())cache.Remove(k);File.WriteAllText(cacheFile+".tmp",JsonSerializer.Serialize(cache));File.Move(cacheFile+".tmp",cacheFile,true);}catch{}}
    string? Cached(string key){if(cache.TryGetValue(key,out var x)&&Now-x.Time<900){Interlocked.Increment(ref hits);return x.Json;}return null;}
    sealed class ApiException(string message,int status):Exception(message){public int Status{get;}=status;}
    void Check(){if(Interlocked.Read(ref blockedUntil)>Now)throw new ApiException("API cooldown active",429);if(!Online)throw new ApiException("Desktop offline mode: enable Network > Allow online market requests first.",503);}
    async Task<string> Request(string url,JsonNode? payload=null,int spacing=1500,bool json=true,string? bearer=null){
        var ct=requestToken.Value;ct.ThrowIfCancellationRequested();Check();var delay=spacing-(DateTimeOffset.UtcNow-lastRequest).TotalMilliseconds;if(delay>0)await Task.Delay((int)Math.Ceiling(delay),ct);Check();
        using var req=new HttpRequestMessage(payload==null?HttpMethod.Get:HttpMethod.Post,url);
        if(bearer!=null)req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",bearer);
        if(payload!=null)req.Content=new StringContent(payload.ToJsonString(),Encoding.UTF8,"application/json");
        lastRequest=DateTimeOffset.UtcNow;
        Interlocked.Increment(ref requests);using var r=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);
        if(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests){var seconds=r.Headers.RetryAfter?.Delta?.TotalSeconds??(r.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow)?.TotalSeconds??900;Pause((int)Math.Clamp(Math.Ceiling(seconds),900,86400*7));throw new ApiException($"Upstream HTTP {(int)r.StatusCode}: cooldown activated",429);}
        if(r.StatusCode==HttpStatusCode.BadRequest)throw new ApiException("Upstream HTTP 400: invalid or unsupported query filters; not retried.",400);
        if(!r.IsSuccessStatusCode)throw new ApiException($"Upstream HTTP {(int)r.StatusCode}",502);
        await using var stream=await r.Content.ReadAsStreamAsync(ct);using var ms=new MemoryStream();var buffer=new byte[8192];int n;while((n=await stream.ReadAsync(buffer.AsMemory(),ct))>0){if(ms.Length+n>64*1024*1024)throw new ApiException("Upstream response too large",502);ms.Write(buffer,0,n);}var text=Encoding.UTF8.GetString(ms.ToArray());if(json)using(JsonDocument.Parse(text)){}return text;
    }
    IResult Error(HttpContext ctx,Exception e){Interlocked.Increment(ref failures);int status=e is OperationCanceledException?499:e is ApiException a?a.Status:502;if(status==429)ctx.Response.Headers["Retry-After"]=Math.Max(1,Interlocked.Read(ref blockedUntil)-Now).ToString(CultureInfo.InvariantCulture);return Results.Json(new{error=e.Message},statusCode:status);}
    public async Task<IResult> Get(string path,HttpContext ctx){if(!Allowed.Contains(path))return Results.NotFound();var ct=CurrentToken();if(Interlocked.Increment(ref queued)>MaxQueue50){Interlocked.Decrement(ref queued);return Results.Json(new{error="Local request queue full; wait for current requests to finish."},statusCode:503);}try{await gate.WaitAsync(ct);}catch(Exception e){return Error(ctx,e);}finally{Interlocked.Decrement(ref queued);}requestToken.Value=ct;try{ct.ThrowIfCancellationRequested();
        var query=string.Join("&",ctx.Request.Query.Where(k=>k.Key!="fresh").SelectMany(k=>k.Value.Select(v=>Uri.EscapeDataString(k.Key)+"="+Uri.EscapeDataString(v??""))));
        var url=path=="trade-stats"?"https://www.pathofexile.com/api/trade/data/stats":"https://poe.ninja/poe1/api/economy/"+path+(query.Length>0?"?"+query:"");
        var data=Cached(url);if(data==null){data=await Request(url);Store(url,data);}return Results.Text(data,"application/json");
    }catch(Exception e){return Error(ctx,e);}finally{gate.Release();}}
    public async Task<IResult> Post(string path,HttpContext ctx){if(path is not ("trade-quote" or "pob-import" or "account-stash"))return Results.NotFound();if(ctx.Request.Headers.Origin.ToString()!=Program.Origin)return Results.StatusCode(403);if(ctx.Request.ContentLength is >20000)return Results.BadRequest(new{error="Request too large"});var ct=CurrentToken();Interlocked.Increment(ref queued);try{await gate.WaitAsync(ct);}catch(Exception e){return Error(ctx,e);}finally{Interlocked.Decrement(ref queued);}requestToken.Value=ct;try{ct.ThrowIfCancellationRequested();
        using var reader=new StreamReader(ctx.Request.Body);var content=await reader.ReadToEndAsync();if(content.Length>20000)throw new ApiException("Request too large",400);var body=JsonNode.Parse(content)??throw new ApiException("Invalid request",400);
        if(path=="account-stash"){
            ctx.Response.Headers["Cache-Control"]="no-store";
            var bearer=Str(body["token"]);var lg=Str(body["league"]);var id=Str(body["stash"]);
            if(!Regex.IsMatch(bearer,@"^[A-Za-z0-9._~+/=-]{16,4096}$")||lg.Length<1||lg.Length>100||!Regex.IsMatch(id,@"^[A-Za-z0-9_-]{0,128}$"))throw new ApiException("Invalid access token, league or stash ID",400);
            var stashUrl="https://api.pathofexile.com/stash/"+Uri.EscapeDataString(lg)+(id.Length>0?"/"+Uri.EscapeDataString(id):"");
            return Results.Text(await Request(stashUrl,spacing:3000,bearer:bearer),"application/json");
        }
        if(path=="pob-import"){
            var id=Str(body["id"]);if(!Regex.IsMatch(id,@"^[A-Za-z0-9_-]{1,128}$"))throw new ApiException("Invalid pobb.in ID",400);
            var text=(await Request("https://pobb.in/"+id+"/raw",json:false)).Trim();if(text.Length>8000000||!(text.StartsWith("<PathOfBuilding")||text.StartsWith("<?xml")||Regex.IsMatch(text,@"^[A-Za-z0-9_+/=\-\s]+$")))throw new ApiException("No valid export returned. Paste a PoB export code instead.",502);return Results.Json(new{code=text});
        }
        var key="trade:"+body.ToJsonString();var cached=Cached(key);if(cached!=null)return Results.Text(cached,"application/json");
        var league=Str(body["league"]);if(league.Length==0||league.Length>100)throw new ApiException("Invalid league",400);
        var q=body["query"]?.DeepClone()??throw new ApiException("Missing trade query",400);var url="https://www.pathofexile.com/api/trade/search/"+Uri.EscapeDataString(league);
        var search=JsonNode.Parse(await Request(url,q,4500))!;
        var ids=Arr(search["result"]).Take(10).Select(Str).Where(x=>x.Length>0).ToArray();var sid=Str(search["id"]);
        if(ids.Length==0||sid.Length==0){q["query"]!["status"]=new JsonObject{["option"]="securable"};search=JsonNode.Parse(await Request(url,q,4500))!;ids=Arr(search["result"]).Take(10).Select(Str).Where(x=>x.Length>0).ToArray();sid=Str(search["id"]);}
        if(ids.Length==0||sid.Length==0)throw new ApiException("No matching listings for selected filters",404);
        var result=JsonNode.Parse(await Request("https://www.pathofexile.com/api/trade/fetch/"+string.Join(",",ids.Select(Uri.EscapeDataString))+"?query="+Uri.EscapeDataString(sid),spacing:4500))!;
        var quote=Sample(body,Arr(result["result"]));var output=JsonSerializer.Serialize(quote);Store(key,output);return Results.Text(output,"application/json");
    }catch(Exception e){return Error(ctx,e);}finally{gate.Release();}}
    static IEnumerable<JsonNode?> Arr(JsonNode? n)=>n is JsonArray a?a:Enumerable.Empty<JsonNode?>();
    static string Str(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var s)?s:n?.ToString()??"";
    static double Num(JsonNode? n,double fallback=0)=>double.TryParse(Str(n),NumberStyles.Float,CultureInfo.InvariantCulture,out var x)&&double.IsFinite(x)?x:fallback;
    static bool? Bool(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<bool>(out var b)?b:null;
    static bool Eq(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    static object Sample(JsonNode body,IEnumerable<JsonNode?> rows){
        string name=Str(body["base"]),variant=Str(body["variant"]);double rate=Num(body["rate"]);bool unid=Bool(body["unidentified"])==true,pob=Str(body["mode"])=="pob28";
        var prices=new Dictionary<string,double>(); string icon="";
        foreach(var row in rows){var item=row?["item"];var listing=row?["listing"];var price=listing?["price"];if(item==null||price==null)continue;
            string actual=Str(item["name"]),type=Str(item["typeLine"]);
            if(!pob){
                if(unid&&Bool(item["identified"])!=false)continue;
                if(name is not ("Chronicle of Atzoatl" or "Sandy Seabed Chart")&&!Eq(name,actual)&&!Eq(name,type)&&!(unid&&Str(body["query"]?["query"]?["name"])==name&&actual==""))continue;
                bool natural=name is "Forbidden Flame" or "Forbidden Flesh" or "Impossible Escape";
                if(natural?Bool(item["corrupted"])!=true:Bool(item["corrupted"])==true)continue;
                if(natural&&Arr(item["implicitMods"]).Any())continue;
                var ilvl=body["query"]?["query"]?["filters"]?["misc_filters"]?["filters"]?["ilvl"];
                if(ilvl!=null&&(Num(item["ilvl"])<Num(ilvl["min"])||Num(item["ilvl"])>Num(ilvl["max"],100)))continue;
                if(name=="Watcher's Eye"&&item["ilvl"]==null)continue;
                if(name=="Stormblood"&&type!=variant+" Flask")continue;
                if(name=="Pearl of Tsoatha"&&(!int.TryParse(variant.Split(' ')[0],out var count)||Arr(item["explicitMods"]).Count(m=>Regex.IsMatch(Str(m),@"\bsupported by\b",RegexOptions.IgnoreCase))!=count))continue;
                if(name=="Sandy Seabed Chart"&&!item.ToJsonString().Contains(variant,StringComparison.OrdinalIgnoreCase))continue;
            }
            var amount=Num(price["amount"]);var multiplier=Str(price["currency"]) switch{"chaos"=>1,"divine"=>rate,_=>0};var value=amount*multiplier;
            var seller=Str(listing?["account"]?["name"]);if(seller.Length==0)seller=Str(row?["id"]);if(seller.Length==0||value<=0||!double.IsFinite(value))continue;
            prices[seller]=Math.Min(prices.GetValueOrDefault(seller,value),value);if(icon.Length==0)icon=Str(item["icon"]);
        }
        var vals=prices.Values.OrderBy(x=>x).Take(5).ToArray();if(vals.Length==0)throw new ApiException("Listings found but no safely matched Chaos/Divine prices",422);
        return new{price=(vals[(vals.Length-1)/2]+vals[vals.Length/2])/2,sample=vals.Length,matchedSellers=prices.Count,low=vals[0],high=vals[^1],quotedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),icon};
    }
    public async Task<int> ImportLegacy(string file){await gate.WaitAsync();try{
        using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(file));int count=0;
        foreach(var category in new[]{"market","trade"})if(doc.RootElement.TryGetProperty(category,out var entries)&&entries.ValueKind==JsonValueKind.Object){
            foreach(var p in entries.EnumerateObject()){
                if(p.Value.ValueKind!=JsonValueKind.Array||p.Value.GetArrayLength()!=2||!p.Value[0].TryGetDouble(out var t)||!double.IsFinite(t)||t>Now||Now-t>=900)continue;
                if(category=="market"&&!p.Name.StartsWith("https://poe.ninja/poe1/api/economy/",StringComparison.Ordinal))continue;
                var key=category=="market"?p.Name:"trade:"+p.Name;var json=category=="market"?p.Value[1].GetString():p.Value[1].GetRawText();if(json==null)continue;
                using var valid=JsonDocument.Parse(json);if(!cache.TryGetValue(key,out var old)||old.Time<t){cache[key]=new((long)t,json);count++;}
            }
        }
        var cooldown=Path.Combine(Path.GetDirectoryName(file)!,"market-cooldown-v24.txt");
        if(File.Exists(cooldown)&&double.TryParse(await File.ReadAllTextAsync(cooldown),NumberStyles.Float,CultureInfo.InvariantCulture,out var until)&&double.IsFinite(until)&&until>Now)Pause((int)Math.Min(int.MaxValue,Math.Ceiling(until-Now)));
        File.WriteAllText(cacheFile+".tmp",JsonSerializer.Serialize(cache));File.Move(cacheFile+".tmp",cacheFile,true);return count;
    }finally{gate.Release();}}
    public void Dispose(){client.Dispose();gate.Dispose();}
}
