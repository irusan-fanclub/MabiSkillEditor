using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MabiSkillEditor.Core.Services;

public class PackExtractService
{
    private const string ModSalt = "})wWb4?-sVGHNoPKpc";

    private readonly string _mabiPackPath;
    private readonly string _packageDir;
    private readonly string _originDir;

    public PackExtractService(string gameFolder, string originDir)
    {
        _mabiPackPath = ConfigService.MabiPackPath;
        _packageDir   = Path.Combine(gameFolder, "package");
        _originDir    = originDir;
    }

    /// <summary>
    /// 從指定 .it 解出特定內部路徑的檔案到 origin/ 目錄
    /// candidateSalts：依序嘗試的 salt 清單（不會打遠端）
    /// 回傳解出後的本地路徑與真正成功的 salt
    /// </summary>
    public (string Path, string SaltUsed) Extract(
        string itFileName, string innerPath, IEnumerable<string> candidateSalts)
    {
        if (!File.Exists(_mabiPackPath))
            throw new FileNotFoundException($"找不到 mabi-pack2.exe：{_mabiPackPath}");

        var itPath = Path.Combine(_packageDir, itFileName);
        if (!File.Exists(itPath))
            throw new FileNotFoundException($"找不到 .it 檔案：{itPath}");

        Directory.CreateDirectory(_originDir);

        var errors = new List<string>();
        string? working = null;

        foreach (var salt in candidateSalts)
        {
            if (string.IsNullOrEmpty(salt)) continue;
            var r = RunExtract(itPath, innerPath, key: salt);
            if (r.Success) { working = salt; break; }
            errors.Add($"[{Mask(salt)}] {r.ErrorMessage.Trim()}");
        }

        if (working == null)
            throw new System.Exception(
                $"解包失敗 ({itFileName})：所有已知 salt 都無法解開。\n" +
                "請在 sources.json 的 KnownSalts 加入新 salt（會在下次成功時自動排到首位）。\n\n" +
                string.Join("\n", errors));

        var outputFile = Path.Combine(_originDir,
            innerPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(outputFile))
            throw new FileNotFoundException($"解包後找不到輸出檔案：{outputFile}");

        return (outputFile, working);
    }

    private static string Mask(string salt)
        => salt.Length <= 4 ? salt : salt[..2] + "***" + salt[^2..];

    private (bool Success, string ErrorMessage) RunExtract(
        string itPath, string innerPath, string? key)
    {
        var args = $"extract -i \"{itPath}\" -o \"{_originDir}\" -f \"{Path.GetFileName(innerPath)}\"";
        if (key != null) args += $" -k \"{key}\"";

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = _mabiPackPath,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };

        proc.Start();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        return proc.ExitCode == 0
            ? (true, "")
            : (false, stderr);
    }

    /// <summary>打包 outputDir 資料夾內容成 .it 檔案</summary>
    public void Pack(string inputDir, string outputItPath)
    {
        if (!File.Exists(_mabiPackPath))
            throw new FileNotFoundException($"找不到 mabi-pack2.exe：{_mabiPackPath}");

        var args = $"pack -i \"{inputDir}\" -o \"{outputItPath}\" -k \"{ModSalt}\"";

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = _mabiPackPath,
                Arguments              = args,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };

        proc.Start();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
            throw new System.Exception($"打包失敗:\n{stderr}");
    }
}
