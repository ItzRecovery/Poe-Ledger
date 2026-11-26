using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace ProjectPoeLedger;
internal sealed class NativeStore : IDisposable {
 readonly SqliteConnection db;
 public NativeStore(string folder){db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(folder,"ledger.sqlite3")}.ToString());db.Open();Exec("PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS native_settings(k TEXT PRIMARY KEY,v TEXT NOT NULL); CREATE TABLE IF NOT EXISTS item_prices(league TEXT,boss TEXT,kind TEXT,name TEXT,price REAL,seen TEXT,PRIMARY KEY(league,boss,kind,name)); CREATE TABLE IF NOT EXISTS price_history(league TEXT,boss TEXT,kind TEXT,name TEXT,stamp INTEGER,price REAL,PRIMARY KEY(league,boss,kind,name,stamp)); CREATE TABLE IF NOT EXISTS area_sessions(id INTEGER PRIMARY KEY,area TEXT,started TEXT,ended TEXT,seconds REAL); CREATE TABLE IF NOT EXISTS price_alerts(id INTEGER PRIMARY KEY,name TEXT,league TEXT,threshold REAL,direction TEXT,triggered INTEGER DEFAULT 0); CREATE TABLE IF NOT EXISTS artwork(kind TEXT,name TEXT,path TEXT,PRIMARY KEY(kind,name));");}
 public void Exec(string sql,params object[] args){using var c=Command(sql,args);c.ExecuteNonQuery();}
 SqliteCommand Command(string sql,object[] args){var c=db.CreateCommand();c.CommandText=sql;for(int i=0;i<args.Length;i++)c.Parameters.AddWithValue("$p"+i,args[i]??DBNull.Value);return c;}
 public List<Dictionary<string,object>> Rows(string sql,params object[] args){using var c=Command(sql,args);using var r=c.ExecuteReader();var a=new List<Dictionary<string,object>>();while(r.Read()){var d=new Dictionary<string,object>();for(int i=0;i<r.FieldCount;i++)d[r.GetName(i)]=r.GetValue(i);a.Add(d);}return a;}
 public string Setting(string k,string fallback="")=>Rows("SELECT v FROM native_settings WHERE k=$p0",k).FirstOrDefault()?.GetValueOrDefault("v")?.ToString()??fallback;
 public void SettingSet(string k,string v)=>Exec("INSERT OR REPLACE INTO native_settings VALUES($p0,$p1)",k,v);
 static string S(JsonElement e,string k)=>e.TryGetProperty(k,out var v)?v.ToString():"";
 static bool Num(JsonElement e,string k,out double n){n=0;return e.TryGetProperty(k,out var v)&&double.TryParse(v.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n)&&double.IsFinite(n)&&n>=0;}
 public void Index(Dictionary<string,string> values){if(!values.TryGetValue("bossledger-v4",out var raw))return;using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;var league=S(root,"league");if(!root.TryGetProperty("bosses",out var bosses)||bosses.ValueKind!=JsonValueKind.Array)return;
 using var tx=db.BeginTransaction();try{using(var clear=db.CreateCommand()){clear.Transaction=tx;clear.CommandText="DELETE FROM item_prices WHERE league=$l";clear.Parameters.AddWithValue("$l",league);clear.ExecuteNonQuery();}foreach(var b in bosses.EnumerateArray())foreach(var kind in new[]{"entry","drops"})if(b.TryGetProperty(kind,out var list)&&list.ValueKind==JsonValueKind.Array)foreach(var r in list.EnumerateArray()){if(!Num(r,"price",out var p)||S(r,"name")=="")continue;using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT OR REPLACE INTO item_prices VALUES($l,$b,$k,$n,$p,$s)";c.Parameters.AddWithValue("$l",league);c.Parameters.AddWithValue("$b",S(b,"name"));c.Parameters.AddWithValue("$k",kind);c.Parameters.AddWithValue("$n",S(r,"name"));c.Parameters.AddWithValue("$p",p);c.Parameters.AddWithValue("$s",S(r,"priceTime"));c.ExecuteNonQuery();}tx.Commit();}catch{tx.Rollback();throw;}
 if(root.TryGetProperty("features24",out var f)&&f.TryGetProperty("history",out var h)&&h.ValueKind==JsonValueKind.Object){using var histTx=db.BeginTransaction();foreach(var prop in h.EnumerateObject()){try{var keys=JsonSerializer.Deserialize<string[]>(prop.Name);if(keys?.Length!=4||prop.Value.ValueKind!=JsonValueKind.Array)continue;foreach(var point in prop.Value.EnumerateArray()){if(!Num(point,"time",out var t)||!Num(point,"price",out var p))continue;using var c=db.CreateCommand();c.Transaction=histTx;c.CommandText="INSERT OR REPLACE INTO price_history VALUES($l,$b,$k,$n,$t,$p)";for(int i=0;i<4;i++)c.Parameters.AddWithValue(new[]{"$l","$b","$k","$n"}[i],keys[i]);c.Parameters.AddWithValue("$t",(long)t);c.Parameters.AddWithValue("$p",p);c.ExecuteNonQuery();}}catch(JsonException){}}histTx.Commit();}}
 public void Dispose()=>db.Dispose();
}
