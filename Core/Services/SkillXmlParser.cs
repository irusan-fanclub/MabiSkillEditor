using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.Core.Services;

public class SkillXmlParser
{
    private static readonly HashSet<string> NonTalentAttrs = new()
    {
        "SkillID","SkillEngName","SkillLocalName","SkillType","SkillCategory",
        "Season","Version","DescName","UIType","MaxStackNum","AutoStack",
        "StackLimitTime","UseType","RaceBasic","BasicType","IsHidden",
        "IsSpecialAction","LvZeroUsable","OnceALife","TransformType","ParentSkill",
        "TargetRange","TargetPreparedType","ProcessTargetType","ImageFile",
        "PositionX","PositionY","SkillDesc","MasterTitle","DecreaseDuraByBrionac",
        "AvailableRace","PublicSeason","Public","HowToGetDesc","ClosedDesc",
        "AutoGetLevel","StringID","WeaponStringID","TargetPreference","UseQuickOption",
        "Feature","SkillPoint","SkillPointRank",
        "Var1","Var2","Var3","Var4","Var5","Var6","Var7","Var8","Var9","Var10",
        "Var11","Var12","Var13","Var14","Var15","Var16","Var17","Var18","Var19",
        "Var20","Var21","Var22","Var23","Var24","Var25","Var26","Var27","Var28",
        "AttackerAction","CompleteLock","CoolDownTimeType","DecreaseDagdaCount",
        "IsMovingSkill","IsUntrainEnable","MultiClassType","NeedToSave",
        "PrepareLock","ProcessLock","Showicon","SkillTypeRebalance",
        "TriggerType","WaitLock","locale",
    };

    private readonly LocalizationService _loc;

    public SkillXmlParser(LocalizationService loc) => _loc = loc;

    // ── 解析 ─────────────────────────────────────────

    public (List<SkillEntry> Skills, List<string> TalentKeys) Parse(string xmlPath)
    {
        var lines = File.ReadAllLines(xmlPath, Encoding.Unicode);

        // ── 第一階段：收集所有 Skill 行 ──────────────
        // (SkillID, lineIndex, season, attrs)
        var candidates = new List<(int Id, int LineIdx, int Season, Dictionary<string, string> Attrs)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.TrimStart().StartsWith("<Skill ")) continue;

            var attrs = ParseAttrs(line);
            if (!attrs.TryGetValue("SkillID", out var idStr) || !int.TryParse(idStr, out var id))
                continue;

            var season = attrs.TryGetValue("Season", out var s) && int.TryParse(s, out var sv) ? sv : 0;
            candidates.Add((id, i, season, attrs));
        }

        // ── 第二階段：每個 SkillID 只保留檔案中最後出現的那列 ──
        var allTalentKeys = new HashSet<string>();
        var skills = new List<SkillEntry>();

        foreach (var group in candidates.GroupBy(c => c.Id))
        {
            var rowCount = group.Count();
            var best     = group.OrderByDescending(c => c.LineIdx).First();
            var attrs    = best.Attrs;

            var raceVal = attrs.TryGetValue("AvailableRace", out var rs)
                          && int.TryParse(rs, out var rv) ? rv : -1;

            var entry = new SkillEntry
            {
                LineIndex         = best.LineIdx,
                OriginalLine      = lines[best.LineIdx],
                SkillID           = best.Id,
                Season            = best.Season,
                RowCount          = rowCount,
                AvailableRace     = raceVal,
                EngName           = attrs.GetValueOrDefault("SkillEngName", ""),
                ImageFile         = attrs.GetValueOrDefault("ImageFile", ""),
                IsHidden          = attrs.GetValueOrDefault("IsHidden", "") == "True",
                WeaponStringID    = attrs.GetValueOrDefault("WeaponStringID", ""),
                TargetPreference  = attrs.GetValueOrDefault("TargetPreference", ""),
                ProcessTargetType = attrs.GetValueOrDefault("ProcessTargetType", ""),
                SkillType         = attrs.GetValueOrDefault("SkillType", ""),
                TriggerType       = attrs.GetValueOrDefault("TriggerType", ""),
                SkillCategory     = attrs.GetValueOrDefault("SkillCategory", ""),
                LocalName         = _loc.Resolve(attrs.GetValueOrDefault("SkillLocalName", "")),
            };

            foreach (var kv in attrs)
            {
                if (!NonTalentAttrs.Contains(kv.Key))
                {
                    entry.TalentValues[kv.Key] = kv.Value;
                    allTalentKeys.Add(kv.Key);
                }
            }

            skills.Add(entry);
        }

        return (skills.OrderBy(s => s.SkillID).ToList(), allTalentKeys.OrderBy(k => k).ToList());
    }

    // ── 儲存（line-preserving）+ 產生 diff ───────────

    public record DiffLine(string Field, string OldVal, string NewVal);
    public record DiffEntry(SkillEntry Skill, List<DiffLine> Lines);

    public List<DiffEntry> Save(
        string srcXmlPath,
        string dstXmlPath,
        IEnumerable<SkillEdit> edits)
    {
        var lines = File.ReadAllLines(srcXmlPath, Encoding.Unicode).ToList();
        var diffs = new List<DiffEntry>();

        foreach (var edit in edits.Where(e => e.HasChanges))
        {
            var skill     = edit.Original;
            var line      = lines[skill.LineIndex];
            var diffLines = new List<DiffLine>();

            line = ApplyField(line, "WeaponStringID",
                skill.WeaponStringID, edit.WeaponStringID, diffLines);

            line = ApplyField(line, "TargetPreference",
                skill.TargetPreference, edit.TargetPreference, diffLines);

            line = ApplyField(line, "SkillType",
                skill.SkillType, edit.SkillType, diffLines);

            line = ApplyField(line, "TriggerType",
                skill.TriggerType, edit.TriggerType, diffLines);

            line = ApplyField(line, "SkillCategory",
                skill.SkillCategory, edit.SkillCategory, diffLines);

            var oldHidden = skill.IsHidden ? "True" : "False";
            var newHidden = edit.IsHidden  ? "True" : "False";
            if (oldHidden != newHidden)
            {
                line = SetAttr(line, "IsHidden", newHidden);
                diffLines.Add(new DiffLine("IsHidden", oldHidden, newHidden));
            }

            lines[skill.LineIndex] = line;
            if (diffLines.Count > 0)
                diffs.Add(new DiffEntry(skill, diffLines));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dstXmlPath)!);
        File.WriteAllLines(dstXmlPath, lines, Encoding.Unicode);
        return diffs;
    }

    // ── 屬性操作 ─────────────────────────────────────

    private static string ApplyField(string line, string attr,
        string oldVal, string newVal, List<DiffLine> diffs)
    {
        if (oldVal == newVal) return line;
        diffs.Add(new DiffLine(attr, oldVal, newVal));
        return string.IsNullOrEmpty(newVal)
            ? RemoveAttr(line, attr)
            : SetAttr(line, attr, newVal);
    }

    private static string SetAttr(string line, string attr, string value)
    {
        var pattern = $@"{Regex.Escape(attr)}=""[^""]*""";
        var replacement = $@"{attr}=""{value}""";
        if (Regex.IsMatch(line, pattern))
            return Regex.Replace(line, pattern, replacement);
        return line.Replace("/>", $@" {attr}=""{value}""/>");
    }

    private static string RemoveAttr(string line, string attr)
        => Regex.Replace(line, $@"\s+{Regex.Escape(attr)}=""[^""]*""", "");

    private static Dictionary<string, string> ParseAttrs(string line)
    {
        var result = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(line, @"(\w+)=""([^""]*)"""))
            result[m.Groups[1].Value] = m.Groups[2].Value;
        return result;
    }
}
