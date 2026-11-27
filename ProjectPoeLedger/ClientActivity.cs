using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ProjectPoeLedger;

// Read-only tail of a user-selected PoE 1 log. Only structured generation events
// are retained; never store chat lines or send log content over the network.
internal sealed class ClientActivity
{
    const long HistoryLimit = 64L * 1024 * 1024;
    const int PollLimit = 4 * 1024 * 1024;
    static readonly Regex Generation = new(@"^(?<day>\d{4}/\d{2}/\d{2}) \d{2}:\d{2}:\d{2} \d+ [0-9a-fA-F]+ \[(?:DEBUG|INFO) Client \d+\] Generating level (?<level>\d{1,3}) area ""(?<area>[A-Za-z0-9_]{1,120})"" with seed (?<seed>\d{1,20})\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    readonly HashSet<string> seen = new(StringComparer.Ordinal);
    readonly Dictionary<(string Day, string Activity, string Area, int Level), int> counts = new();
    long position;
    DateTime creation;
    string partial = "";
    bool discard, initialized, limited;
    public string PathName { get; }
    public ClientActivity(string path) { PathName = path; }
    internal static string? Category(string area, int level)
    {
        if (level < 60 || level > 100) return null;
        if (area.Equals("MapWorldsShapersRealm", StringComparison.OrdinalIgnoreCase)) return "Shaper arena";
        if (area.Equals("MapWorldsElderArenaUber", StringComparison.OrdinalIgnoreCase)) return "Uber Elder arena";
        if (area.StartsWith("MapWorlds", StringComparison.OrdinalIgnoreCase) || area.StartsWith("MapAtlas", StringComparison.OrdinalIgnoreCase)) return "Map / endgame area";
        if (area.StartsWith("Heist", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(area, "harbou?r|town|hideout|foyer", RegexOptions.IgnoreCase)) return "Heist area";
        if (area.StartsWith("Sanctum", StringComparison.OrdinalIgnoreCase) && !area.Contains("Foyer", StringComparison.OrdinalIgnoreCase)) return "Sanctum area";
        if (area.StartsWith("Delve", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(area, "town|hideout|encampment|camp", RegexOptions.IgnoreCase)) return "Delve area";
        if (area.StartsWith("MapSideArea", StringComparison.OrdinalIgnoreCase)) return "Vaal side area";
        return null;
    }
    void ProcessLine(string line)
    {
        var m = Generation.Match(line);
        if (!m.Success || !int.TryParse(m.Groups["level"].Value, out int level)) return;
        string area = m.Groups["area"].Value, seed = m.Groups["seed"].Value, day = m.Groups["day"].Value;
        string? category = Category(area, level);
        if (category == null || seed == "0" || seed == "1") return;
        // Same area + seed is the same inferred instance, including re-entry after midnight.
        if (!seen.Add(area + "|" + seed)) return;
        var key = (day, category, area, level);
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }
    public object Poll()
    {
        try
        {
            using var file = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var created = File.GetCreationTimeUtc(PathName);
            if (!initialized || created != creation || file.Length < position)
            {
                seen.Clear(); counts.Clear(); partial = ""; creation = created; initialized = true;
                position = Math.Max(0, file.Length - HistoryLimit); discard = position > 0; limited = discard;
            }
            file.Position = position;
            byte[] buffer = new byte[65536];
            int remaining = PollLimit;
            while (remaining > 0)
            {
                int read = file.Read(buffer, 0, Math.Min(remaining, buffer.Length));
                if (read == 0) break;
                remaining -= read; position += read;
                // Parsed fields are ASCII; non-ASCII chat is deliberately ignored.
                var lines = (partial + Encoding.ASCII.GetString(buffer, 0, read)).Split('\n');
                partial = lines[^1];
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    if (discard) { discard = false; continue; }
                    if (lines[i].Length <= 8192) ProcessLine(lines[i].TrimEnd('\r'));
                }
                if (partial.Length > 8192) { partial = ""; discard = true; }
            }
            return Snapshot(position < file.Length ? "Reading history..." : "Watching Client.txt", position < file.Length);
        }
        catch (Exception e) { return Snapshot("Cannot read Client.txt: " + e.GetType().Name, false); }
    }
    object Snapshot(string status, bool scanning)
    {
        return new {
            connected = true, status, scanning, limited,
            total = counts.Values.Sum(),
            today = counts.Where(x => x.Key.Day == DateTime.Now.ToString("yyyy/MM/dd")).Sum(x => x.Value),
            categories = counts.GroupBy(x => x.Key.Activity).Select(g => new { activity = g.Key, count = g.Sum(x => x.Value) }).OrderByDescending(x => x.count).ToArray(),
            areas = counts.GroupBy(x => (x.Key.Activity, x.Key.Area, x.Key.Level)).Select(g => new { activity = g.Key.Activity, area = g.Key.Area, level = g.Key.Level, count = g.Sum(x => x.Value) }).OrderByDescending(x => x.count).ThenBy(x => x.area).Take(200).ToArray(),
            days = counts.GroupBy(x => x.Key.Day).Select(g => new { day = g.Key, count = g.Sum(x => x.Value) }).OrderByDescending(x => x.day).Take(30).ToArray()
        };
    }
}
