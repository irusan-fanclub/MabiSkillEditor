using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.Core.Services;

public static class DiffImportService
{
    public record SkillChange(int SkillId, string Field, string NewValue);

    /// <summary>依副檔名分派：.json → ParseJson；其他 → 舊版 .txt 解析。</summary>
    public static List<SkillChange> Parse(string diffPath)
    {
        var ext = Path.GetExtension(diffPath);
        return string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase)
            ? ParseJson(diffPath)
            : ParseLegacyTxt(diffPath);
    }

    public static List<SkillChange> ParseJson(string path)
    {
        var json   = File.ReadAllText(path, Encoding.UTF8);
        var doc    = JsonSerializer.Deserialize<DiffFile>(json) ?? new DiffFile();
        var result = new List<SkillChange>();
        foreach (var skill in doc.Skills)
            foreach (var kv in skill.Changes)
                result.Add(new SkillChange(skill.SkillID, kv.Key, kv.Value.New));
        return result;
    }

    /// <summary>
    /// 解析 v0.1.x 舊版 diff.txt（純文字格式）。
    /// </summary>
    public static List<SkillChange> ParseLegacyTxt(string diffPath)
    {
        var result = new List<SkillChange>();
        var lines  = File.ReadAllLines(diffPath, Encoding.UTF8);

        int currentId = -1;
        foreach (var line in lines)
        {
            // 技能標頭：[271] 閃光彈 (Flash Bomb)
            var header = Regex.Match(line, @"^\[(\d+)\]");
            if (header.Success)
            {
                currentId = int.Parse(header.Groups[1].Value);
                continue;
            }

            // 欄位變更行：  WeaponStringID: （無） → /staff/
            if (currentId >= 0 && line.StartsWith("  "))
            {
                var change = Regex.Match(line.Trim(), @"^(\w+): (.+) → (.+)$");
                if (change.Success)
                {
                    var newVal = change.Groups[3].Value;
                    if (newVal == "（刪除）") newVal = "";
                    result.Add(new SkillChange(currentId, change.Groups[1].Value, newVal));
                }
            }
        }

        return result;
    }
}
