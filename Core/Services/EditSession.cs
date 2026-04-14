using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.Core.Services;

/// <summary>
/// 追蹤所有待套用的技能修改
/// </summary>
public class EditSession
{
    private readonly Dictionary<int, SkillEdit> _edits = new();

    // ── 修改操作 ─────────────────────────────────────

    /// <summary>取得或建立某技能的 SkillEdit（第一次存取時複製原始值）</summary>
    public SkillEdit GetOrCreate(SkillEntry skill)
    {
        if (!_edits.TryGetValue(skill.SkillID, out var edit))
        {
            edit = new SkillEdit(skill);
            _edits[skill.SkillID] = edit;
        }
        return edit;
    }

    /// <summary>提交對某技能的編輯；若無任何變更則從清單移除</summary>
    public void Commit(SkillEdit edit)
    {
        if (edit.HasChanges)
            _edits[edit.Original.SkillID] = edit;
        else
            _edits.Remove(edit.Original.SkillID);
    }

    public void Remove(int skillId) => _edits.Remove(skillId);

    public void Clear() => _edits.Clear();

    // ── 查詢 ─────────────────────────────────────────

    public bool HasEdit(int skillId) =>
        _edits.TryGetValue(skillId, out var e) && e.HasChanges;

    public SkillEdit? TryGet(int skillId) =>
        _edits.TryGetValue(skillId, out var e) ? e : null;

    public IReadOnlyList<SkillEdit> AllEdits =>
        _edits.Values.Where(e => e.HasChanges).ToList();

    // ── 輸出 diff.txt ─────────────────────────────────

    public void WriteDiff(string path, IEnumerable<SkillXmlParser.DiffEntry> diffs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MabiSkillEditor - 修改記錄");
        sb.AppendLine(new string('=', 50));

        foreach (var entry in diffs)
        {
            var skill = entry.Skill;
            sb.AppendLine();
            sb.AppendLine($"[{skill.SkillID}] {skill.DisplayName} ({skill.EngName})");
            foreach (var line in entry.Lines)
            {
                var oldDisplay = string.IsNullOrEmpty(line.OldVal) ? "（無）" : line.OldVal;
                var newDisplay = string.IsNullOrEmpty(line.NewVal) ? "（刪除）" : line.NewVal;
                sb.AppendLine($"  {line.Field}: {oldDisplay} → {newDisplay}");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
