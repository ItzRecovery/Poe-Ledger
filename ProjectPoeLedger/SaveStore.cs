using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace ProjectPoeLedger;
internal sealed class SaveStore : IDisposable
{
    readonly SqliteConnection db;
    public SaveStore(string folder)
    {
        db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(folder,"ledger.sqlite3")}.ToString()); db.Open();
        using var cmd=db.CreateCommand(); cmd.CommandText="PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS ledger_save (key TEXT PRIMARY KEY, value TEXT NOT NULL, updated_utc TEXT NOT NULL); CREATE TABLE IF NOT EXISTS ledger_meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";cmd.ExecuteNonQuery();
    }
    public Dictionary<string,string> Read(){using var cmd=db.CreateCommand();cmd.CommandText="SELECT key,value FROM ledger_save";using var r=cmd.ExecuteReader();var data=new Dictionary<string,string>();while(r.Read())data[r.GetString(0)]=r.GetString(1);return data;}
    public bool Initialized {get{using var cmd=db.CreateCommand();cmd.CommandText="SELECT value FROM ledger_meta WHERE key='initialized'";return cmd.ExecuteScalar()!=null;}}
    public void Replace(Dictionary<string,string> values){
        foreach(var (k,v) in values){if(!k.StartsWith("bossledger",StringComparison.Ordinal))throw new InvalidDataException("Invalid save key");using var parsed=JsonDocument.Parse(v);}
        using var tx=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="DELETE FROM ledger_save";cmd.ExecuteNonQuery();
        foreach(var (k,v) in values){cmd.Parameters.Clear();cmd.CommandText="INSERT INTO ledger_save VALUES ($k,$v,$t)";cmd.Parameters.AddWithValue("$k",k);cmd.Parameters.AddWithValue("$v",v);cmd.Parameters.AddWithValue("$t",DateTime.UtcNow.ToString("O"));cmd.ExecuteNonQuery();}
        cmd.Parameters.Clear();cmd.CommandText="INSERT OR REPLACE INTO ledger_meta VALUES ('initialized','1')";cmd.ExecuteNonQuery();tx.Commit();
    }
    public void Backup(string path){if(File.Exists(path))throw new IOException("Choose a new backup filename.");using var dest=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path}.ToString());dest.Open();db.BackupDatabase(dest);}
    public void Dispose()=>db.Dispose();
}
