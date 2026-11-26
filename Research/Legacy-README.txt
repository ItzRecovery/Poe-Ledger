Project Poe Ledger v0.24 — Windows / Python 3
Extract the entire ZIP, close previous servers, double-click Start Project Poe Ledger.bat.
The browser uses the existing bossledger-v4 local storage key on 127.0.0.1:8765.
Export a save from your old version first if possible. New session logs, research and price history are included in exports.
Starts offline for new V0.24 users. Enable market requests only after any existing API restriction expires. The pause button applies a 15-minute local cooldown; the backend also persists upstream rate-limit cooldowns and respects Retry-After seconds. Cached market data is reused for 15 minutes; cache retained on disk up to 24 hours. Requests are serialized and spaced; this reduces but cannot eliminate rate-limit risk. Do not run multiple server instances.
Seven tabs: original calculator, advanced rankings, risk/bankroll, tracker, research, price history, comparison. Record observed kills rather than item copies for drop-rate research. Blank counts are not observations. History is local and starts with available prices; no historical data is invented.
Risk simulation assumes immediate loot sales and existing loot-table probabilities. The capital floor is a ten-run budget, not a guarantee. Historical rates and incomplete loot tables are unchanged from v0.23. No live API requests were made during development.

V0.25: Economy toolkit and PoE clipboard item import. All new tools use manual or unambiguous saved boss prices only. Trade search link generation is offline; opening links uses the external website. No live API requests during development. Same localStorage key and server port. Copy market-cache-v24.json and market-cooldown-v24.txt from old folder if present.

V0.26: PoE Item Import now accepts local PoB export codes or XML. Gear sets and tree jewels have individual trade links. Rare modifiers are not matched automatically; remote build URLs are not fetched.

V0.27: PoB shopping list, manual price and purchased tracking per gear set, optional saved unique prices, total and remaining known cost. Limited built-in life/resistance stat templates; import saved official Trade stats JSON for more exact single-value matches. Complex and ambiguous modifiers require manual review. Direct pobb.in import uses an explicit remote request to pobb.in only, not the PoE API; live URL retrieval untested. Local code/XML import remains available. No live API requests during development.

V0.28: PoB shopping upgrades. Adjust individual supported stat minimums or use the global percentage. Budget links relax matched numeric stats to 70% and omit strict item constraints. Saved prices are generic; online quotes use the selected Trade filters and are only requested after clicking Quote with market requests enabled. Unknown affixes still need review. Skill gem prices are manual and separate from equipment totals. Save build snapshots to compare known equipment costs. Previous imported shopping lists are retained when switching builds in this version. No live API testing was performed.

V0.29: advanced Trade options, weights, influence tags, gem filters and offline candidate optimizer. Imported gear totals are flat contributions only. Optimizer requires user-priced candidates, checks supplied targets and budget, and limits search to 200000 nodes. Check compatibility manually. No market requests needed. Existing storage keys unchanged.

V0.31: Covens of Umbra removes Scarabs. Farming defaults now use optional Threads; old Scarab rows are archived in saved calculator data and excluded from totals, with new generic Thread prices blank. Scarab exchange category removed. No live requests during development.

V0.31: Price search scope: All bosses, Top 5 by profit/hour or profit/kill. Preview uses saved complete rankings. Selected bosses are frozen for the bulk refresh and matching official Trade fallback. poe.ninja category feeds still download whole categories; only chosen boss rows update. Existing cache and storage keys unchanged.

V0.32 — Professional Economy Terminal
- New full-width workspace, collapsible left navigation and global boss/drop/entry search (press / to focus).
- Display settings: Midnight, Graphite, Emerald or Amber theme; comfortable/compact density; adjustable sidebar, boss selector and dashboard column widths.
- Dashboard uses only existing saved data. Toggle dashboard cards; drag their bottom edge to resize. Incomplete bosses remain excluded from the top five.
- Larger item thumbnails when market artwork is available online; offline uses generated emblems. Boss artwork is a generated emblem by default, with optional local PNG/JPEG/WebP upload (750 KB max). These are not official boss portraits.
- UI preferences use a separate bossledger-ui32 localStorage key. Existing bossledger-v4 state and all prior calculator storage are unchanged. Export your original save before upgrading. Continue using http://127.0.0.1:8765/ to retain same-origin saves. UI preferences are browser-local and not part of the existing save export.
- No live API requests were made during development. Existing API/cooldown behavior retained. To preserve on-disk cache and cooldown, copy market-cache-v24.json and market-cooldown-v24.txt from the previous folder if present.

V0.33.3: Seven new workspaces. Atlas catalog uses historical Covens diagnostics snapshot; import newer diagnostics locally. Existing save keys unchanged.

V0.33.4: Dashboard Data coverage missing-entry editor; selected saved-price and protected trade fallback; observed rates require confirmation. No API requests during development.

V0.33.5: Removed the dedicated Crafting Lab workspace. Existing crafting calculator and saved data retained.

V0.33.7: Boss portraits are optional remote references, not bundled image files. Enable Load web portraits on a boss page to use them. Custom local uploads take priority; missing or failed images fall back to emblems. No market API calls are required for artwork. Existing save keys and port unchanged.

Long boss loot tables now scroll within a height-limited panel with sticky column headings. Short tables keep their natural height.

V0.33.7: Delve session workspace with historical fossil/resonator prices, custom rewards, depth, sulphite, time and local session history. No automatic network requests.

V0.33.8: Heist and Delve economy panels. Historical 3.25 Delve boss estimates (100 kills each) from PoE Wiki; unidentified/variant prices require manual verification. Offline only new calculators; existing data retained.
