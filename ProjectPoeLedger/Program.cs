using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Application=System.Windows.Forms.Application;
using MessageBox=System.Windows.Forms.MessageBox;
using Size=System.Drawing.Size;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ProjectPoeLedger;
internal static class Program
{
    internal static (string Path,string Hash)? PendingUpdate;
    internal const string Origin = "http://127.0.0.1:8766";
    internal static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectPoeLedger");
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory(DataDir);
        using var mutex = new Mutex(true, "Local\\ProjectPoeLedgerDesktop", out bool first);
        if (!first) { MessageBox.Show("Poe Ledger is already running."); return; }
        using var market = new MarketService(DataDir);
        string token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(Origin);
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Use(async (ctx, next) => {
            if (ctx.Request.Host.Host != "127.0.0.1" || ctx.Request.Host.Port != 8766) { ctx.Response.StatusCode=403; return; }
            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Request.Headers["X-Ledger-Token"].ToString() != token) { ctx.Response.StatusCode=403; return; }
            await next(ctx);
        });
        app.MapGet("/", () => Results.File(Path.Combine(AppContext.BaseDirectory,"Web","index.html"),"text/html; charset=utf-8"));
        app.MapGet("/poe-ledger.png", () => Results.File(Path.Combine(AppContext.BaseDirectory,"Web","poe-ledger.png"),"image/png"));
        app.MapGet("/index.html", () => Results.Redirect("/"));
        app.MapGet("/boss-art/{filename}", (string filename) => {
            if(!System.Text.RegularExpressions.Regex.IsMatch(filename,@"^(manifest\.json|[a-z0-9-]+\.png)$"))return Results.NotFound();
            var path=Path.Combine(AppContext.BaseDirectory,"Web","boss-art",filename);
            return File.Exists(path)?Results.File(path,filename.EndsWith(".json")?"application/json":"image/png"):Results.NotFound();
        });
        app.MapGet("/artwork/{filename}", (string filename) => {
            if(!System.Text.RegularExpressions.Regex.IsMatch(filename,@"^[a-f0-9-]{36}\.(png|jpg|jpeg|webp)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return Results.NotFound();
            string path=Path.Combine(DataDir,"artwork",filename);if(!File.Exists(path))return Results.NotFound();
            string mime=Path.GetExtension(path).ToLowerInvariant() switch{ ".png"=>"image/png",".webp"=>"image/webp",_=>"image/jpeg"};return Results.File(path,mime);
        });
        app.MapGet("/api/{**path}", async (string path, HttpContext ctx) => await market.Get(path,ctx));
        app.MapPost("/api/{**path}", async (string path, HttpContext ctx) => await market.Post(path,ctx));
        try { app.StartAsync().GetAwaiter().GetResult(); }
        catch(Exception e) { MessageBox.Show("Cannot start desktop service on port 8766.\n"+e.Message); return; }
        try { Application.Run(new LedgerWindow(market,token)); }
        finally { app.StopAsync().GetAwaiter().GetResult(); app.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        if(PendingUpdate is { } update){try{ReleaseCheck.LaunchAfterExit(update.Path,update.Hash);}catch(Exception e){MessageBox.Show("Cannot start update: "+e.Message);}}
    }
}
internal sealed class LedgerWindow : Form
{
    readonly WebView2 web = new() { Dock=DockStyle.Fill };
    readonly MarketService market;
    readonly SaveStore saves;
    readonly NativeStore native;
    NativeTools? tools;
    PriceOverlay? overlay;
    readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="Poe Ledger"};
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
    string League(){try{using var d=JsonDocument.Parse(saves.Read().GetValueOrDefault("bossledger-v4","{}"));return d.RootElement.TryGetProperty("league",out var l)?l.GetString()??"Covens":"Covens";}catch{return "Covens";}}
    void DailyBackup50(){try{if(!saves.Initialized)return;var folder=Path.Combine(Program.DataDir,"backups");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"daily-"+DateTime.UtcNow.ToString("yyyy-MM-dd")+".sqlite3");if(!File.Exists(path)){var temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{saves.Backup(temp);File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}foreach(var f in Directory.GetFiles(folder,"daily-????-??-??.sqlite3").OrderByDescending(x=>x,StringComparer.Ordinal).Skip(14)){try{File.Delete(f);}catch{}}}catch(Exception e){Notify("Automatic database backup failed: "+e.Message);}}
    void Notify(string text){tray.Visible=true;tray.ShowBalloonTip(5000,"Poe Ledger",text,ToolTipIcon.Info);}
    NativeTools Tools(){if(tools==null){tools=new NativeTools(native,market,Notify,League,()=>{online.Checked=false;market.Cancel();SyncNetwork();},SyncArt,OpenClipboardTrade,saves.Read,OpenEconomyPage,PrepareUpdate);System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(tools);}return tools;}
    void SetPriceHotkey(string key){if(key is not ("P" or "L" or "K"))key="P";var old=native.Setting("priceHotkey","P");UnregisterHotKey(Handle,36);if(RegisterHotKey(Handle,36,0x4003,key[0])){native.SettingSet("priceHotkey",key);Notify("Price check: Ctrl+Alt+"+key);}else{if(old is "P" or "L" or "K")RegisterHotKey(Handle,36,0x4003,old[0]);Notify("Requested hotkey unavailable. Previous shortcut retained if available.");}}
    protected override void WndProc(ref Message m){if(m.Msg==0x0312&&m.WParam.ToInt32()==36){try{ShowOverlay();}catch(Exception e){MessageBox.Show(e.Message);}}base.WndProc(ref m);}
    void ShowOverlay(){if(overlay==null){overlay=new PriceOverlay(native,market,League,saves.Read,token,OpenClipboardTrade);overlay.Closed+=(_,_)=>overlay=null;System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(overlay);}overlay.ShowItem();}
    void ShowTools(){try{var t=Tools();t.Show();t.Activate();}catch(Exception e){MessageBox.Show(e.Message);}}
    void SyncArt(){if(!ready||web.CoreWebView2==null)return;var rows=native.Rows("SELECT kind,name,path FROM artwork");var map=rows.ToDictionary(r=>r["kind"]+":"+r["name"],r=>"/artwork/"+Path.GetFileName(r["path"].ToString()));web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{ledgerArtwork=map}));}
    async void OpenClipboardTrade(string text){if(!ready)return;Show();WindowState=FormWindowState.Normal;Activate();try{await web.ExecuteScriptAsync("(()=>{showPage24('import');const p=document.getElementById('paste25');if(p){p.value="+JsonSerializer.Serialize(text)+";p.focus();}})()");}catch(Exception e){MessageBox.Show(e.Message);}}

    async void OpenEconomyPage(string page){if(!ready)return;Show();WindowState=FormWindowState.Normal;Activate();try{await web.ExecuteScriptAsync("showPage24("+JsonSerializer.Serialize(page)+")");}catch(Exception e){MessageBox.Show(e.Message);}}
    void SyncNetwork(){if(ready&&web.CoreWebView2!=null)web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{ledgerOnline=online.Checked}));}

    (string Path,string Hash)? stagedUpdate;
    void PrepareUpdate(string path,string hash){if(!ready)throw new InvalidOperationException("Wait until the main workspace has loaded.");stagedUpdate=(path,hash);Close();}
    bool saveWarning;
    bool closingAfterSave;
    readonly string token;
    readonly ToolStripMenuItem online = new("Allow online market requests") { CheckOnClick=true };
    bool ready;
    public LedgerWindow(MarketService market,string token)
    {
        this.market=market; this.token=token; saves=new SaveStore(Program.DataDir);native=new NativeStore(Program.DataDir);try{native.Index(saves.Read());}catch{}
        FormClosed+=(_,_)=>{UnregisterHotKey(Handle,36);overlay?.Close();tools?.Shutdown();tray.Dispose();native.Dispose();saves.Dispose();};
        FormClosing+=async (_,e)=>{
            if(!ready||closingAfterSave)return;
            e.Cancel=true;
            try {var raw=await web.ExecuteScriptAsync("JSON.stringify(Object.fromEntries(Object.keys(localStorage).filter(k=>k.startsWith('bossledger')).map(k=>[k,localStorage.getItem(k)])))");
                saves.Replace(JsonSerializer.Deserialize<Dictionary<string,string>>(JsonSerializer.Deserialize<string>(raw)??"{}")??throw new InvalidDataException("Invalid save snapshot"));
                if(stagedUpdate is { } update){var folder=Path.Combine(Program.DataDir,"backups");Directory.CreateDirectory(folder);saves.Backup(Path.Combine(folder,"before-update-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".sqlite3"));Program.PendingUpdate=update;stagedUpdate=null;}
            }catch(Exception ex){if(stagedUpdate!=null){stagedUpdate=null;Program.PendingUpdate=null;MessageBox.Show("Update stopped: save or backup failed.\n"+ex.Message);return;}if(MessageBox.Show("Could not save SQLite snapshot. Close anyway?\n"+ex.Message,"Save error",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;}
            closingAfterSave=true;Close();
        };
        Text="Poe Ledger — Desktop 0.52.1"; Width=1440; Height=900; MinimumSize=new Size(900,600);
        StartPosition=FormStartPosition.CenterScreen;
        try{Icon=new Icon(Path.Combine(AppContext.BaseDirectory,"PoeLedger.ico"));tray.Icon=Icon;}catch{}
        var menu=new MenuStrip();
        var file=new ToolStripMenuItem("File");
        file.DropDownItems.Add("Export all saved data",null,async (_,_)=>await ExportData());
        file.DropDownItems.Add("Import desktop or browser backup",null,async (_,_)=>await ImportData());
        file.DropDownItems.Add("Import legacy market cache",null,async (_,_)=>await ImportCache());
        file.DropDownItems.Add("Backup SQLite database",null,(_,_)=>{try{using var dlg=new SaveFileDialog{Filter="SQLite database|*.sqlite3",FileName="ledger-backup.sqlite3"};if(dlg.ShowDialog()==DialogResult.OK){saves.Backup(dlg.FileName);MessageBox.Show("Database backed up. Copy artwork and cache separately if required.");}}catch(Exception ex){MessageBox.Show(ex.Message);}});
        file.DropDownItems.Add("Open data folder",null,(_,_)=>OpenExternal(Program.DataDir));
        file.DropDownItems.Add("Exit",null,(_,_)=>Close());
        var network=new ToolStripMenuItem("Network");
        online.CheckedChanged+=(_,_)=>{market.Online=online.Checked;SyncNetwork();};
        network.DropDownItems.Add(online);
        network.DropDownItems.Add("Pause for 15 minutes",null,(_,_)=>{market.Pause(900); online.Checked=false; MessageBox.Show("Market requests paused. Cooldown is saved.");});
        network.DropDownItems.Add("Status",null,(_,_)=>MessageBox.Show(market.Status));
        menu.Items.Add(file);menu.Items.Add(network);
        var view=new ToolStripMenuItem("View");
        view.DropDownItems.Add("Reload",null,(_,_)=>{if(ready)web.Reload();});
        view.DropDownItems.Add("Quick price overlay",null,(_,_)=>ShowOverlay());
        view.DropDownItems.Add("Native WPF tools",null,(_,_)=>ShowTools());
        var hotkeys=new ToolStripMenuItem("Price check hotkey");
        foreach(var key in new[]{"P","L","K"}){var selected=key;hotkeys.DropDownItems.Add("Ctrl+Alt+"+selected,null,(_,_)=>SetPriceHotkey(selected));}view.DropDownItems.Add(hotkeys);
        view.DropDownItems.Add("Minimize to tray",null,(_,_)=>{tray.Visible=true;Hide();});
        var trayMenu=new ContextMenuStrip();trayMenu.Items.Add("Restore",null,(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();});trayMenu.Items.Add("Native tools",null,(_,_)=>ShowTools());trayMenu.Items.Add("Exit",null,(_,_)=>Close());tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();};tray.Visible=true;
        network.DropDownItems.Add("Cancel network requests",null,(_,_)=>{online.Checked=false;market.Cancel();SyncNetwork();});
        menu.Items.Add(view);
        var help=new ToolStripMenuItem("Help");help.DropDownItems.Add("Check for updates",null,(_,_)=>{try{Tools().ShowUpdates();}catch(Exception ex){MessageBox.Show(ex.Message);}});menu.Items.Add(help);
        Controls.Add(web);Controls.Add(menu);MainMenuStrip=menu;
        Shown+=async (_,_)=>{DailyBackup50();await InitializeWeb();SetPriceHotkey(native.Setting("priceHotkey","P"));try{Tools();}catch(Exception e){Notify("Native tools initialization failed: "+e.Message); }};
    }
    async Task InitializeWeb()
    {
        try {
            var env=await CoreWebView2Environment.CreateAsync(userDataFolder:Path.Combine(Program.DataDir,"WebView2"));
            await web.EnsureCoreWebView2Async(env);
            var core=web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled=true;
            core.Settings.AreDevToolsEnabled=false;
            core.Settings.IsWebMessageEnabled=true;
            core.WebMessageReceived+=(_,e)=>{
                if(!Local(e.Source))return;
                try {
                    using var msg=JsonDocument.Parse(e.WebMessageAsJson);
                    if(msg.RootElement.TryGetProperty("ledgerOnline",out var flag)&&flag.ValueKind is JsonValueKind.True or JsonValueKind.False){online.Checked=flag.GetBoolean();return;}
                    var data=JsonSerializer.Deserialize<Dictionary<string,string>>(e.WebMessageAsJson);
                    if(data!=null){saves.Replace(data);try{native.Index(data);tools?.MarkDirty();}catch{}}
                }catch(Exception ex){if(!saveWarning){saveWarning=true;MessageBox.Show("SQLite save failed. Browser storage is retained. Export a backup.\n"+ex.Message);}}
            };
            // On first run keep existing WebView storage; on later runs restore the committed database snapshot.
            string seed=JsonSerializer.Serialize(saves.Read());
            string initialized=saves.Initialized?"true":"false";
            await core.AddScriptToExecuteOnDocumentCreatedAsync("(()=>{if(location.origin!=="+JsonSerializer.Serialize(Program.Origin)+")return;const p=Storage.prototype;const set=p.setItem,remove=p.removeItem,clear=p.clear;const isKey=k=>String(k).startsWith('bossledger');const seed="+seed+";if("+initialized+"&&sessionStorage.getItem('ledger-db-session')!=="+JsonSerializer.Serialize(token)+"){for(const k of Object.keys(localStorage).filter(isKey))remove.call(localStorage,k);for(const [k,v] of Object.entries(seed))set.call(localStorage,k,v);}sessionStorage.setItem('ledger-db-session',"+JsonSerializer.Serialize(token)+");const snapshot=()=>{try{window.chrome.webview.postMessage(Object.fromEntries(Object.keys(localStorage).filter(isKey).map(k=>[k,localStorage.getItem(k)])));}catch{}};p.setItem=function(k,v){set.call(this,k,v);if(this===localStorage&&isKey(k))snapshot()};p.removeItem=function(k){remove.call(this,k);if(this===localStorage&&isKey(k))snapshot()};p.clear=function(){clear.call(this);if(this===localStorage)snapshot()};window.addEventListener('DOMContentLoaded',snapshot);})();");
            core.NavigationStarting+=(_,e)=>{if(!Local(e.Uri)){e.Cancel=true;OpenWeb(e.Uri);}};
            core.NewWindowRequested+=(_,e)=>{e.Handled=true;OpenWeb(e.Uri);};
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            core.WebResourceRequested+=(_,e)=>{
                if(Local(e.Request.Uri) || e.Request.Uri.StartsWith("data:") || e.Request.Uri.StartsWith("blob:")) return;
                // Do not let embedded pages bypass the central API manager. Artwork is opt-in.
                if(market.Online && Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var u) && u.Scheme=="https" && e.ResourceContext==CoreWebView2WebResourceContext.Image && (u.Host=="web.poecdn.com" || (u.Host=="cdn.poedb.tw" && u.AbsolutePath.StartsWith("/image/Art/2DItems/",StringComparison.Ordinal)) || u.Host.EndsWith(".poewiki.net") || u.Host=="poewiki.net" || u.Host=="poe.ninja")) return;
                e.Response=core.Environment.CreateWebResourceResponse(null,403,"Blocked by desktop network policy","");
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync("(()=>{const f=window.fetch.bind(window);window.fetch=(input,init={})=>{const u=new URL(typeof input==='string'?input:input.url??String(input),location.href);if(u.origin===location.origin&&u.pathname.startsWith('/api/')){const h=new Headers(init.headers||(input instanceof Request?input.headers:undefined));h.set('X-Ledger-Token',"+JsonSerializer.Serialize(token)+");return f(input,{...init,headers:h});}return f(input,init);};})();");
            core.NavigationCompleted+=(_,_)=>{SyncNetwork();SyncArt();};core.Navigate(Program.Origin+"/");ready=true;
        }catch(Exception e){MessageBox.Show("WebView2 initialization failed. Install the Microsoft Edge WebView2 Runtime.\n"+e.Message);}
    }
    static bool Local(string u)=>Uri.TryCreate(u,UriKind.Absolute,out var a)&&a.Scheme=="http"&&a.Host=="127.0.0.1"&&a.Port==8766;
    static void OpenExternal(string u){try{Process.Start(new ProcessStartInfo(u){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message);}}
    static void OpenWeb(string u){if(Uri.TryCreate(u,UriKind.Absolute,out var a)&&a.Scheme=="https")OpenExternal(u);}
    async Task ImportCache(){try{
        using var dlg=new OpenFileDialog{Filter="Legacy market cache|market-cache-v24.json|JSON files|*.json"};if(dlg.ShowDialog()!=DialogResult.OK)return;
        online.Checked=false;
        int count=await market.ImportLegacy(dlg.FileName);
        MessageBox.Show($"Imported {count} fresh legacy market records. Any saved legacy cooldown was retained. Network remains disabled.");
    }catch(Exception e){MessageBox.Show(e.Message);}}
    async Task ExportData(){if(!ready)return;try{
        var raw=await web.ExecuteScriptAsync("JSON.stringify(Object.fromEntries(Object.keys(localStorage).filter(k=>k.startsWith('bossledger')).map(k=>[k,localStorage.getItem(k)])))");
        string json=JsonSerializer.Deserialize<string>(raw)??"{}";
        using var dlg=new SaveFileDialog{Filter="JSON backup|*.json",FileName="project-poe-ledger-desktop-backup.json"};
        if(dlg.ShowDialog()==DialogResult.OK) await File.WriteAllTextAsync(dlg.FileName,json);
    }catch(Exception e){MessageBox.Show(e.Message);}}
    async Task ImportData(){if(!ready)return;try{
        using var dlg=new OpenFileDialog{Filter="JSON backup|*.json"};if(dlg.ShowDialog()!=DialogResult.OK)return;
        using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(dlg.FileName));
        var root=doc.RootElement;var values=new Dictionary<string,string>();
        if(root.ValueKind!=JsonValueKind.Object)throw new Exception("Invalid backup.");
        if(root.TryGetProperty("bosses",out var b)&&b.ValueKind==JsonValueKind.Array) values["bossledger-v4"]=root.GetRawText();
        else foreach(var p in root.EnumerateObject())if(p.Name.StartsWith("bossledger")&&p.Value.ValueKind==JsonValueKind.String){var value=p.Value.GetString()!;using var check=JsonDocument.Parse(value);if(p.Name=="bossledger-v4"&&(!check.RootElement.TryGetProperty("bosses",out var bosses)||bosses.ValueKind!=JsonValueKind.Array))throw new Exception("Invalid boss save.");values[p.Name]=value;}
        if(values.Count==0)throw new Exception("No recognized Ledger data.");
        if(MessageBox.Show("Import will replace matching saved data keys. Export a backup first if needed. Continue?","Import",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        string backup=await web.ExecuteScriptAsync("JSON.stringify(Object.fromEntries(Object.keys(localStorage).filter(k=>k.startsWith('bossledger')).map(k=>[k,localStorage.getItem(k)])))");
        await File.WriteAllTextAsync(Path.Combine(Program.DataDir,"before-import-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".json"),JsonSerializer.Deserialize<string>(backup)??"{}");
        var result=await web.ExecuteScriptAsync("(()=>{try{for(const [k,v] of Object.entries("+JsonSerializer.Serialize(values)+"))localStorage.setItem(k,v);return 'OK'}catch(e){return String(e)}})()");
        if(JsonSerializer.Deserialize<string>(result)!="OK")throw new Exception("Storage import failed: "+result);
        var merged=saves.Read();foreach(var (k,v) in values)merged[k]=v;saves.Replace(merged);
        web.Reload();
    }catch(Exception e){MessageBox.Show(e.Message);}}
}
