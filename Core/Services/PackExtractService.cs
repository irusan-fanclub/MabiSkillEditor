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
    /// knownSalt：sources.json 中指定的 salt，優先使用
    /// 回傳解出後的本地路徑，失敗則拋出例外
    /// </summary>
    public string Extract(string itFileName, string innerPath, string? knownSalt = null)
    {
        if (!File.Exists(_mabiPackPath))
            throw new FileNotFoundException($"找不到 mabi-pack2.exe：{_mabiPackPath}");

        var itPath = Path.Combine(_packageDir, itFileName);
        if (!File.Exists(itPath))
            throw new FileNotFoundException($"找不到 .it 檔案：{itPath}");

        Directory.CreateDirectory(_originDir);

        // 優先用 sources.json 指定的 salt
        (bool Success, string ErrorMessage) result = (false, "");
        if (!string.IsNullOrEmpty(knownSalt))
            result = RunExtract(itPath, innerPath, key: knownSalt);

        // fallback：不帶 -k 讓工具自動試（需伺服器可用）
        if (!result.Success)
            result = RunExtract(itPath, innerPath, key: null);

        // 最後 fallback：mod salt
        if (!result.Success)
            result = RunExtract(itPath, innerPath, key: ModSalt);

        if (!result.Success)
            throw new Exception($"解包失敗 ({itFileName}):\n{result.ErrorMessage}");

        // 轉換 inner path 分隔符，找到輸出檔案
        var outputFile = Path.Combine(_originDir,
            innerPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(outputFile))
            throw new FileNotFoundException($"解包後找不到輸出檔案：{outputFile}");

        return outputFile;
    }

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
            throw new Exception($"打包失敗:\n{stderr}");
    }
}
