using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
namespace ProjectPoeLedger;
internal static class EconomyViews {
 public record Summary(string Name,double Cost,double Loot,double Net,double? Hour,int Missing,bool Invalid,JsonElement Detail);
 public record Loot(string Item,double? Rate,double? Quantity,double? Price,double? Expected,string Group,bool Enabled);
 internal static JsonElement Get(JsonElement e,string k)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(k,out var v)?v:default;
 internal static string Text(JsonElement e,string k)=>Get(e,k).ToString();
 internal static IEnumerable<JsonElement> Arr(JsonElement e)=>e.ValueKind==JsonValueKind.Array?e.EnumerateArray().ToArray():Array.Empty<JsonElement>();
 internal static double? N(JsonElement e,string k)=>double.TryParse(Text(e,k),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)&&n>=0?n:null;
 internal static bool Flag(JsonElement e,string k)=>Get(e,k).ValueKind==JsonValueKind.True;
 public static List<Summary> Read(JsonElement root,string kind){var result=new List<Summary>();if(kind=="boss")foreach(var b in Arr(Get(root,"bosses"))){var entries=Arr(Get(b,"entry")).ToList();var drops=Arr(Get(b,"drops")).ToList();int missing=entries.Count(r=>N(r,"price")==null)+drops.Count(r=>Flag(r,"on")&&(N(r,"price")==null||N(r,"p")==null));var invalid=drops.Any(r=>N(r,"p")>100)||drops.Where(r=>Text(r,"g")!="").GroupBy(r=>Text(r,"g")).Any(g=>g.Sum(r=>N(r,"p")??0)>100.0001);double cost=(N(b,"extra")??0)+entries.Sum(r=>(N(r,"price")??0)*(N(r,"q")??0));double loot=Rows(b,kind).Where(r=>r.Enabled).Sum(r=>r.Expected??0);double net=loot-cost;result.Add(new(Text(b,"name"),cost,loot,net,net*60/Math.Max(.01,N(b,"minutes")??0),missing+(entries.Count==0||drops.Count==0?1:0),invalid,b.Clone()));}
 else {var w=Get(Get(Get(root,"workspaces33"),Text(root,"league")),"eco338");foreach(var s in Arr(Get(Get(w,kind),"strategies"))){var rows=Rows(s,kind);var success=N(s,"success")??0;var cost=N(s,"cost");var minutes=N(s,"minutes");var missing=rows.Count(r=>r.Expected==null||r.Rate>100)+(cost==null?1:0)+(minutes==null||minutes<=0?1:0);var invalid=rows.Where(r=>r.Group!="").GroupBy(r=>r.Group).Any(g=>g.Sum(r=>r.Rate??0)>100.0001)||success>100;double loot=rows.Sum(r=>r.Expected??0)*success/100;double net=loot-(cost??0);result.Add(new(Text(s,"name"),cost??0,loot,net,minutes>0?net*60/minutes:null,missing,invalid,s.Clone()));}}
 return result.OrderBy(x=>x.Missing>0||x.Invalid).ThenByDescending(x=>x.Hour).ToList();}
 public static List<Loot> Rows(JsonElement b,string kind){var rows=new List<Loot>();foreach(var r in Arr(Get(b,kind=="boss"?"drops":"rows"))){var rate=N(r,"p");var qty=N(r,kind=="boss"?"q":"qty");var price=N(r,"price");double factor=kind=="boss"&&Flag(r,"scaled")?1+(N(b,"quantity")??0)/100:1;double? ev=rate*qty*price*factor/100;rows.Add(new(Text(r,"name"),rate,qty,price,ev,Text(r,"g"),kind!="boss"||Flag(r,"on")));}return rows;}
}
