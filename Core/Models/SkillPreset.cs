namespace MabiSkillEditor.Core.Models;

/// <summary>
/// 內建快速範本。固定針對單一技能、只能改 WeaponStringID / TargetPreference。
/// null = 不改該欄位；空字串 = 改成空（清除屬性）。
/// </summary>
public class SkillPreset
{
    public string  Name             { get; set; } = "";
    public int     SkillID          { get; set; }
    public string? WeaponStringID   { get; set; }
    public string? TargetPreference { get; set; }

    /// <summary>至少要設一個欄位，且有 Name 與 SkillID</summary>
    public bool IsValid =>
        !string.IsNullOrEmpty(Name) &&
        SkillID > 0 &&
        (WeaponStringID != null || TargetPreference != null);
}
