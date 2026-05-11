using System.Collections.Generic;

namespace MabiSkillEditor.Core.Models;

/// <summary>diff.json 頂層</summary>
public class DiffFile
{
    public string  App         { get; set; } = "MabiSkillEditor";
    public string  AppVersion  { get; set; } = "";
    public int?    GameVersion { get; set; }
    public string  ExportedAt  { get; set; } = "";
    public List<DiffSkill> Skills { get; set; } = new();
}

public class DiffSkill
{
    public int     SkillID { get; set; }
    public string  Name    { get; set; } = "";
    public string  EngName { get; set; } = "";
    public Dictionary<string, DiffChange> Changes { get; set; } = new();
}

public class DiffChange
{
    public string Old { get; set; } = "";
    public string New { get; set; } = "";
}
