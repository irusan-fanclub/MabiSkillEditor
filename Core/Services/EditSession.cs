using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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

    // ── 輸出 diff.json ────────────────────────────────

    private static readonly JsonSerializerOptions _diffJsonOpts = new()
    {
        WriteIndented = true,
        Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void WriteDiff(
        string path,
        IEnumerable<SkillXmlParser.DiffEntry> diffs,
        string appVersion,
        int?   gameVersion)
    {
        var doc = new DiffFile
        {
            AppVersion  = appVersion,
            GameVersion = gameVersion,
            ExportedAt  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        foreach (var entry in diffs)
        {
            var skillDoc = new DiffSkill
            {
                SkillID = entry.Skill.SkillID,
                Name    = entry.Skill.DisplayName,
                EngName = entry.Skill.EngName,
            };
            foreach (var line in entry.Lines)
                skillDoc.Changes[line.Field] = new DiffChange { Old = line.OldVal, New = line.NewVal };
            doc.Skills.Add(skillDoc);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(doc, _diffJsonOpts);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
