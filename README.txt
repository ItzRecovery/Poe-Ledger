POE LEDGER V0.38.0

Build Desktop.bat, then Start Project Poe Ledger.bat or Install Desktop.bat. The old executable name, data folder, SQLite schema and save keys are unchanged. New shortcuts are named Poe Ledger.
Copy an item in-game with Ctrl+C, then use your existing Ctrl+Alt+P (or configured L/K) hotkey. The compact overlay opens with saved name matches; explicit comparable listings quotes require online permission and follow the shared market queue and cooldown. Unique and currency identity only; rare affixes, gems, unidentified and complex variants use the existing importer. Esc hides the overlay. No automatic requests on clipboard reads.
Development validation: JavaScript syntax, project XML, icon and ZIP structure only; Windows compilation and live quote behavior untested. No live PoE requests were made.

PROJECT POE LEDGER 0.37 — WINDOWS DESKTOP DEVELOPMENT SOURCE

1. Close the old application. Keep a full backup (File > Export all saved data).
2. Extract this release into a new folder. Install .NET 10 SDK and WebView2 Runtime if required.
3. Run Build Desktop.bat. NuGet restore uses the internet; no PoE calls are needed to build.
4. Run Start Project Poe Ledger.bat. Existing data in %LOCALAPPDATA%\ProjectPoeLedger is reused.
5. Optionally run Install Desktop.bat after a successful build. This per-user copy installer creates
   a Start menu shortcut and does not delete your data. If PowerShell policy blocks the script,
   continue using the existing Start BAT. No MSI or signed installer is included.

NATIVE TOOLS: View > Native WPF tools.
- Market database: indexed saved boss prices and existing history, filter by item name.
- Price check: copy an item in PoE and press Ctrl+Alt+P, or use Read clipboard. Saved exact-name
  quotes are not variant-specific appraisals; send the item to the full Trade importer for filters.
  The hotkey is fixed in this build and may conflict with other applications.
- Farming log: choose Client.txt and click Start tracking. Reads newly appended English
  'You have entered ...' messages. Area visits are logged separately from actual farming results;
  they are not verified kills, map completions, costs, or loot. Keep the application running.
- Alerts: exact-name saved price thresholds in Chaos, current league. Conflicting variant prices
  are skipped. One notification per alert; remove and recreate to rearm.
- Artwork: select boss/item, enter exact name, import a local image. The file is copied to the
  local artwork folder and used by matching boss portraits and loot icons. No artwork is bundled.
- API & updates: request counters, queue size, cache hits, cooldown, cancel and go offline.
  Update checking reads a manually selected local JSON manifest, not a remote release service.
- View > Minimize to tray; tray menu restores or exits. Clipboard is read only on explicit action.

NETWORK: disabled on launch. The top checkbox and native Network menu share the permission.
Cancellation turns network off and cancels pending upstream work. Existing cooldowns are retained.
No live API requests were made during development.

DATA: the original SQLite ledger_save JSON records remain authoritative for the existing UI.
New relational tables index item prices/history and hold native settings, area visits, alerts,
and artwork. Legacy calculator data and storage keys are retained. Native-only data is in the same
SQLite file but not included in the legacy JSON export. For a full backup, exit the app completely
and copy the entire %LOCALAPPDATA%\ProjectPoeLedger directory. Do not delete the old installation.

LIMITATIONS: this is a hybrid source release, not a full WPF rewrite. All existing pages are still
hosted by WebView2. Native price checks use cached data, log tracking measures area visits, and
updates are checked against local manifests. The code has not been compiled or run on Windows in
this environment. If build fails, send build-log.txt. Use the working 0.35.2 until verified.

V0.37 DEVELOPMENT ADDENDUM
- View > Native WPF tools now includes Bossing, Heist and Delve read-only ranking and reward grids.
  Click Reload saved data to update from SQLite; use Open full editor to modify strategies. Heist and
  Delve must have been initialized in the legacy workspace before their native view can show data.
- View > Price check hotkey supports Ctrl+Alt+P/L/K. The Price check tab has an optional topmost mode.
- Clipboard Trade importer has an explicit Quote matching listings button for supported identified
  uniques, gems and currency. It uses the existing guarded API endpoint and returns a median asking
  price sample, not a full listing grid. Rare affixes and special variants are not appraised by it.
- Farming log has conservative area classifications, not automatic confirmed kills or loot records.
- File > Backup SQLite database makes a consistent SQLite backup, including native tables. For restore,
  EXIT all Ledger processes, back up the entire data directory, then replace ledger.sqlite3 with the
  backup and remove the old ledger.sqlite3-wal and ledger.sqlite3-shm files if present. Only restore
  trusted backups made by this application. Artwork files and market cache are separate.
- ProjectPoeLedger.iss is an optional Inno Setup 6 installer recipe; compile it after publishing the
  Desktop folder. No installer executable or remote update service is included. Local manifest checking remains.
- All new C# code is uncompiled; JavaScript syntax, project XML and classification SQL were checked.
  No live API requests were made. Keep the working 0.35.2 release until this version has been tested.

V0.37.1 REMOTE UPDATE CHECKING
Help > Check for updates opens API & updates. Enter a trusted, direct HTTPS URL
hosting release-manifest.json, save it, and click Check for updates online.
No release server is configured or hosted by this package. For publishing,
host a JSON file with version, notes and optional downloadUrl (HTTPS) fields.
Use a direct URL (redirects are not followed). Numeric versions are compared
with the installed assembly version. A newer release can expose a link to open
in the default browser; nothing is downloaded or executed automatically.
Checks are explicit, independent of the PoE market offline/cooldown switch.
They send no saved data or PoE API requests. Keep the local manifest option
for offline testing. The C# code has not been compiled on Windows here.

V0.37.3 INSTALLER FIX
Install Desktop.bat uses a process-only PowerShell execution policy override.
No persistent execution policy changes are made. Only run trusted installer scripts.
Administrator-enforced Group Policy may still prevent execution.

V0.37.3 BUILD FIX
Explicit System.Net.Http import in MarketService; SQLite bundle override to 3.0.3. Windows build and dependency restore not verified here.
