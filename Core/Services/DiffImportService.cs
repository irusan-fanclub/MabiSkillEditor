using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MabiSkillEditor.Core.Services;

public static class DiffImportService
{
    public record SkillChange(int SkillId, string Field, string NewValue);

    /// <summary>
    /// 解析 EditSession.WriteDiff() 產生的 diff.txt，
    /// 回傳每筆欄位變更。
    /// </summary>
    public static List<SkillChange> Parse(string diffPath)
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
