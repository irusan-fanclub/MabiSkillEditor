using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.Core.Services;

/// <summary>
/// 解析 itemDB_Weapon.xml + ItemDB.xml 等武器來源。
/// 多個來源用 ID 合併（第一個來源優先），最終過濾出 tag 含 "weapon" 的項目。
/// </summary>
public class WeaponDataService
{
    public record Source(string XmlPath, string LocTxtPath, string LtPrefix);

    private static readonly Regex AttrIdRegex       = new("\\bID=\"(\\d+)\"", RegexOptions.Compiled);
    private static readonly Regex AttrCategoryRegex = new("\\bCategory=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex AttrName0Regex    = new("\\bText_Name0=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex AttrName1Regex    = new("\\bText_Name1=\"([^\"]*)\"", RegexOptions.Compiled);

    public List<Weapon> Weapons { get; private set; } = new();

    /// <summary>tag (no slash) → 包含此 tag 的武器，按 ID 升序</summary>
    public Dictionary<string, List<Weapon>> ByTag { get; private set; } = new();

    public void Load(IEnumerable<Source> sources)
    {
        Log.Section("WeaponDataService.Load");
        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        var byId = new Dictionary<int, Weapon>();
        int beforeFilter = 0;
        foreach (var src in sources)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var locale = LoadLocalization(src.LocTxtPath);
            var ltRegex = new Regex(
                $@"_LT\[{Regex.Escape(src.LtPrefix)}\.(\d+)\]",
                RegexOptions.Compiled);

            int srcAdded = 0, srcSkipped = 0;
            foreach (var w in ParseWeapons(src.XmlPath, locale, ltRegex))
            {
                beforeFilter++;
                if (!w.Tags.Contains("weapon")) { srcSkipped++; continue; }
                if (byId.TryAdd(w.ID, w)) srcAdded++;
            }
            sw.Stop();
            Log.Info($"  {Path.GetFileName(src.XmlPath)} ({sw.ElapsedMilliseconds}ms): +{srcAdded} 新增、跳過 {srcSkipped} 非武器");
        }

        Weapons = byId.Values.OrderBy(w => w.ID).ToList();
        ByTag = BuildIndex(Weapons);

        totalSw.Stop();
        Log.Info($"Load OK ({totalSw.ElapsedMilliseconds}ms): {Weapons.Count} 武器、{ByTag.Count} unique tags（過濾前共 {beforeFilter} 筆）");
    }

    private static Dictionary<int, string> LoadLocalization(string txtPath)
    {
        var table = new Dictionary<int, string>();
        if (!File.Exists(txtPath)) return table;
        foreach (var line in File.ReadAllLines(txtPath, Encoding.UTF8))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var idx))
                table[idx] = parts[1].Trim();
        }
        return table;
    }

    private static IEnumerable<Weapon> ParseWeapons(
        string xmlPath, Dictionary<int, string> locale, Regex ltRegex)
    {
        if (!File.Exists(xmlPath)) yield break;

        var lines = File.ReadAllLines(xmlPath, Encoding.Unicode);
        foreach (var line in lines)
        {
            if (!line.TrimStart().StartsWith("<Mabi_Item ")) continue;

            var idM = AttrIdRegex.Match(line);
            if (!idM.Success || !int.TryParse(idM.Groups[1].Value, out var id)) continue;

            var catM   = AttrCategoryRegex.Match(line);
            var name0M = AttrName0Regex.Match(line);
            var name1M = AttrName1Regex.Match(line);

            var engName = name0M.Success ? name0M.Groups[1].Value : "";
            var name    = engName;
            if (name1M.Success)
            {
                var ltM = ltRegex.Match(name1M.Groups[1].Value);
                if (ltM.Success
                    && int.TryParse(ltM.Groups[1].Value, out var lt)
                    && locale.TryGetValue(lt, out var localName)
                    && !string.IsNullOrEmpty(localName))
                {
                    name = localName;
                }
            }
            if (string.IsNullOrEmpty(name)) name = engName;
            if (string.IsNullOrEmpty(name)) name = $"#{id}";

            var tags = new List<string>();
            if (catM.Success)
            {
                foreach (var t in catM.Groups[1].Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    var tt = t.Trim();
                    if (tt.Length > 0) tags.Add(tt);
                }
            }

            yield return new Weapon
            {
                ID      = id,
                Name    = name,
                EngName = engName,
                Tags    = tags,
            };
        }
    }

    private static Dictionary<string, List<Weapon>> BuildIndex(List<Weapon> weapons)
    {
        var index = new Dictionary<string, List<Weapon>>(StringComparer.Ordinal);
        foreach (var w in weapons)
        foreach (var t in w.Tags)
        {
            if (!index.TryGetValue(t, out var list))
            {
                list = new List<Weapon>();
                index[t] = list;
            }
            list.Add(w);
        }
        foreach (var key in index.Keys.ToList())
            index[key] = index[key].OrderBy(w => w.ID).ToList();
        return index;
    }
}
