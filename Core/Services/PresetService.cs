using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.Core.Services;

public static class PresetService
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static PresetsConfig Load()
    {
        var path = ConfigService.PresetsPath;
        if (!File.Exists(path))
        {
            var def = CreateDefault();
            try { File.WriteAllText(path, JsonSerializer.Serialize(def, _opts)); }
            catch (Exception ex) { Log.Error("寫入預設 presets.json 失敗", ex); }
            Log.Info($"presets.json 不存在，建立預設檔 ({def.Presets.Count} 個範本)");
            return def;
        }
        try
        {
            var json = File.ReadAllText(path);
            var cfg  = JsonSerializer.Deserialize<PresetsConfig>(json) ?? new PresetsConfig();
            var valid = new List<SkillPreset>();
            foreach (var p in cfg.Presets)
            {
                if (p.IsValid) valid.Add(p);
                else Log.Warn($"略過無效範本: Name='{p.Name}' SkillID={p.SkillID}");
            }
            cfg.Presets = valid;
            Log.Info($"presets.json 載入 {valid.Count} 個範本");
            return cfg;
        }
        catch (Exception ex)
        {
            Log.Error("presets.json 解析失敗", ex);
            return new PresetsConfig();
        }
    }

    private static PresetsConfig CreateDefault() => new()
    {
        Presets = new()
        {
            new SkillPreset
            {
                Name             = "範例：冰凍術允許任何武器",
                SkillID          = 271,
                WeaponStringID   = "",
                TargetPreference = null,
            },
        },
    };
}
