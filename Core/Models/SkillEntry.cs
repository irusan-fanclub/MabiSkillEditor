using System.Collections.Generic;
using System.Linq;

namespace MabiSkillEditor.Core.Models;

/// <summary>
/// 對應 SkillInfo.xml 中一個 &lt;Skill&gt; 元素
/// </summary>
public class SkillEntry
{
    // ── 識別 ──────────────────────────────────────────
    public int    SkillID   { get; set; }
    public string EngName   { get; set; } = "";
    public string LocalName { get; set; } = "";   // 解析後的中文名

    // ── 版本 ──────────────────────────────────────────
    /// <summary>Season 值（用於版本篩選，取同 SkillID 最大值）</summary>
    public int Season   { get; set; }
    /// <summary>XML 中同 SkillID 出現的總列數（用於重複偵測）</summary>
    public int RowCount { get; set; } = 1;
    public bool HasDuplicates => RowCount > 1;

    /// <summary>在 XML 中的顯示行號（1-based）</summary>
    public int LineNumber => LineIndex + 1;

    // ── 種族 ──────────────────────────────────────────
    /// <summary>-1 = 屬性不存在；0 = 怪物；bitmask: 1人類 2精靈 4巨人</summary>
    public int AvailableRace { get; set; } = -1;

    public string AvailableRaceDisplay
    {
        get
        {
            if (AvailableRace < 0) return "（未設定）";
            if (AvailableRace == 0) return "無（怪物）";
            var parts = new System.Collections.Generic.List<string>();
            if ((AvailableRace & 1) != 0) parts.Add("人類");
            if ((AvailableRace & 2) != 0) parts.Add("精靈");
            if ((AvailableRace & 4) != 0) parts.Add("巨人");
            return string.Join("、", parts);
        }
    }

    // ── 顯示 ──────────────────────────────────────────
    public string ImageFile { get; set; } = "";
    public bool   IsHidden  { get; set; }

    // ── 武器 / 對象 ───────────────────────────────────
    public string WeaponStringID    { get; set; } = "";
    public string TargetPreference  { get; set; } = "";
    public string ProcessTargetType { get; set; } = "";

    // ── 職業 / 天賦（key = 欄位名稱，value = 原始字串）
    public Dictionary<string, string> TalentValues { get; set; } = new();

    // ── 原始行資訊（用於 line-preserving 輸出）
    public int    LineIndex    { get; set; }
    public string OriginalLine { get; set; } = "";

    /// <summary>顯示用名稱（中文優先，fallback 到英文）</summary>
    public string DisplayName => string.IsNullOrEmpty(LocalName) ? EngName : LocalName;
}
