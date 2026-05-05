namespace MabiSkillEditor.Core.Models;

/// <summary>
/// 使用者對單一技能的待套用修改
/// null = 未修改（保留原值）
/// ""   = 清除屬性
/// </summary>
public class SkillEdit
{
    public SkillEntry Original { get; }

    public SkillEdit(SkillEntry original)
    {
        Original = original;
        // 預設帶入原始值，讓編輯表單一開始有值
        WeaponStringID   = original.WeaponStringID;
        TargetPreference = original.TargetPreference;
        IsHidden         = original.IsHidden;
        SkillType        = original.SkillType;
        TriggerType      = original.TriggerType;
        SkillCategory    = original.SkillCategory;
    }

    public string WeaponStringID   { get; set; }
    public string TargetPreference { get; set; }
    public bool   IsHidden         { get; set; }
    public string SkillType        { get; set; }
    public string TriggerType      { get; set; }
    public string SkillCategory    { get; set; }

    /// <summary>是否與原始值有任何差異</summary>
    public bool HasChanges =>
        WeaponStringID   != Original.WeaponStringID   ||
        TargetPreference != Original.TargetPreference ||
        IsHidden         != Original.IsHidden         ||
        SkillType        != Original.SkillType        ||
        TriggerType      != Original.TriggerType      ||
        SkillCategory    != Original.SkillCategory;
}
