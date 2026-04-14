using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.Core.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    // ── AppConfig (config.json) ───────────────────────

    public static string ConfigPath =>
        Path.Combine(AppDir, "config.json");

    public static AppConfig LoadConfig()
    {
        AppConfig cfg;
        if (!File.Exists(ConfigPath))
            cfg = new AppConfig();
        else
        {
            var json = File.ReadAllText(ConfigPath);
            cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }

        // 若設定檔沒有遊戲資料夾，嘗試從 Registry 自動偵測
        if (string.IsNullOrEmpty(cfg.GameFolder))
            cfg.GameFolder = ReadGameFolderFromRegistry();

        return cfg;
    }

    /// <summary>
    /// 從 HKCU\SOFTWARE\Nexon\Mabinogi 讀取 LauncherPath
    /// </summary>
    public static string ReadGameFolderFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Nexon\Mabinogi");
            return key?.GetValue("LauncherPath") as string ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static void SaveConfig(AppConfig config)
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, _opts));
    }

    // ── SourcesConfig (sources.json) ─────────────────

    public static string SourcesPath =>
        Path.Combine(AppDir, "sources.json");

    public static SourcesConfig LoadSources()
    {
        if (!File.Exists(SourcesPath))
        {
            // 建立預設值
            var def = new SourcesConfig();
            File.WriteAllText(SourcesPath, JsonSerializer.Serialize(def, _opts));
            return def;
        }
        var json = File.ReadAllText(SourcesPath);
        return JsonSerializer.Deserialize<SourcesConfig>(json) ?? new SourcesConfig();
    }

    // ── 工具 ─────────────────────────────────────────

    public static string AppDir =>
        Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";

    public static string OriginDir =>
        Path.Combine(AppDir, "origin");

    public static string OutputDir =>
        Path.Combine(AppDir, "output");

    public static string MabiPackPath =>
        Path.Combine(AppDir, "mabi-pack2.exe");
}
