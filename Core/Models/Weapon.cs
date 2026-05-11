using System.Collections.Generic;

namespace MabiSkillEditor.Core.Models;

/// <summary>itemDB_Weapon.xml 一筆武器資料（已解析）</summary>
public class Weapon
{
    public int    ID      { get; set; }
    public string Name    { get; set; } = "";  // 中文（_LT 解出）；無則 fallback 為 EngName
    public string EngName { get; set; } = "";  // Text_Name0
    public List<string> Tags { get; set; } = new();  // Category 拆出，無前後 slash

    /// <summary>UI 顯示用：中文名 + (英文)</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(EngName) || EngName == Name
            ? Name
            : $"{Name} ({EngName})";
}
