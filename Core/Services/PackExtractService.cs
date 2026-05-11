using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MabiSkillEditor.Core.Services;

public class PackExtractService
{
    private const string ModSalt          = "})wWb4?-sVGHNoPKpc";
    private const int    ProcessTimeoutMs = 60_000;

    private readonly string _mabiPackPath;
    private readonly string _packageDir;
    private readonly string _originDir;
    private readonly string _workDir;
    private readonly string _saltsFilePath;

    public PackExtractService(string gameFolder, string originDir)
    {
        _mabiPackPath  = ConfigService.MabiPackPath;
        _packageDir    = Path.Combine(gameFolder, "package");
        _originDir     = originDir;
        _workDir       = ConfigService.AppDir;
        _saltsFilePath = Path.Combine(_workDir, "salts.txt");
    }

    /// <summary>
    /// 從指定 .it 解出特定內部路徑的檔案到 origin/ 目錄。
    /// 機制：把 candidateSalts 寫到 AppDir/salts.txt（cwd），讓 mabi-pack2 自己跑。
    /// 不傳 -k：mabi-pack2 預設會自動嘗試 salts.txt 內所有 salt 直到成功。
    /// 回傳解出後的本地路徑與真正成功的 salt（從 stdout 解析）。
    /// </summary>
    public (string Path, string? SaltUsed) Extract(
        string itFileName, string innerPath, IEnumerable<string> candidateSalts)
    {
        Log.Section($"Extract {itFileName} :: {innerPath}");

        if (!File.Exists(_mabiPackPath))
        {
            Log.Error($"找不到 mabi-pack2.exe：{_mabiPackPath}");
            throw new FileNotFoundException($"找不到 mabi-pack2.exe：{_mabiPackPath}");
        }

        var itPath = Path.Combine(_packageDir, itFileName);
        if (!File.Exists(itPath))
        {
            Log.Error($"找不到 .it 檔案：{itPath}");
            throw new FileNotFoundException($"找不到 .it 檔案：{itPath}");
        }

        Directory.CreateDirectory(_originDir);
        WriteSaltsFile(candidateSalts);
        Log.Info($"itPath={itPath} originDir={_originDir} salts={_saltsFilePath}");

        var sw = Stopwatch.StartNew();
        var r  = RunExtract(itPath, innerPath);
        sw.Stop();

        if (!r.Success)
        {
            var firstErr = r.Combined.Trim().Split('\n').FirstOrDefault(l =>
                l.Contains("[ERROR]", StringComparison.Ordinal)) ?? r.Combined.Trim();
            Log.Error($"extract 失敗 ({sw.ElapsedMilliseconds}ms): {firstErr}");
            throw new System.Exception(
                $"解包失敗 ({itFileName})：\n{r.Combined}\n" +
                "請確認 sources.json 的 KnownSalts 涵蓋此 IT 版本。");
        }

        var workingSalt = ExtractSaltFromOutput(r.Combined);
        Log.Info($"extract OK ({sw.ElapsedMilliseconds}ms) salt={(workingSalt != null ? Mask(workingSalt) : "(unparsed)")}");

        var outputFile = Path.Combine(_originDir,
            innerPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(outputFile))
        {
            Log.Error($"解包後找不到輸出檔案：{outputFile}");
            throw new FileNotFoundException($"解包後找不到輸出檔案：{outputFile}");
        }

        Log.Info($"output={outputFile} ({new FileInfo(outputFile).Length} bytes)");
        return (outputFile, workingSalt);
    }

    private void WriteSaltsFile(IEnumerable<string> salts)
    {
        var lines = salts.Where(s => !string.IsNullOrEmpty(s)).ToList();
        File.WriteAllLines(_saltsFilePath, lines, new UTF8Encoding(false));
        Log.Info($"wrote salts.txt ({lines.Count} salts)");
    }

    /// <summary>
    /// 列出 .it 內部所有檔案路徑（forward-slash 正規化）。
    /// 失敗時回空 list、不 throw（caller 通常是 best-effort 掃描）。
    /// </summary>
    public IReadOnlyList<string> List(string itFileName, IEnumerable<string> candidateSalts)
    {
        var itPath = Path.Combine(_packageDir, itFileName);
        if (!File.Exists(_mabiPackPath) || !File.Exists(itPath))
            return System.Array.Empty<string>();

        WriteSaltsFile(candidateSalts);

        var args = $"list -i \"{itPath}\"";
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = _mabiPackPath,
                Arguments              = args,
                WorkingDirectory       = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            if (!proc.WaitForExit(ProcessTimeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                Log.Warn($"list {itFileName} timeout");
                return System.Array.Empty<string>();
            }
            proc.WaitForExit();
        }
        catch (Exception ex)
        {
            Log.Warn($"list {itFileName} 啟動失敗: {ex.Message}");
            return System.Array.Empty<string>();
        }

        if (proc.ExitCode != 0)
        {
            Log.Info($"list {itFileName} exit={proc.ExitCode}: {stderr.ToString().Trim().Split('\n').LastOrDefault()}");
            return System.Array.Empty<string>();
        }

        return stdout.ToString()
                     .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim().Replace('\\', '/'))
                     .Where(s => s.Length > 0)
                     .ToList();
    }

    private static readonly Regex HeaderKeyRegex =
        new(@"Header key '([^']+)'", RegexOptions.Compiled);

    private static string? ExtractSaltFromOutput(string output)
    {
        var m = HeaderKeyRegex.Match(output);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string Mask(string salt)
        => salt.Length <= 4 ? salt : salt[..2] + "***" + salt[^2..];

    private (bool Success, string Combined) RunExtract(string itPath, string innerPath)
    {
        var args = $"extract -i \"{itPath}\" -o \"{_originDir}\" -f \"{Path.GetFileName(innerPath)}\"";

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = _mabiPackPath,
                Arguments              = args,
                WorkingDirectory       = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!proc.WaitForExit(ProcessTimeoutMs))
            {
                Log.Warn($"mabi-pack2 timeout ({ProcessTimeoutMs}ms)，強制 kill");
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (false, $"timeout after {ProcessTimeoutMs}ms\nstdout:\n{stdout}\nstderr:\n{stderr}");
            }
            proc.WaitForExit();
        }
        catch (Exception ex)
        {
            Log.Error("mabi-pack2 啟動失敗", ex);
            return (false, ex.Message);
        }

        // mabi-pack2 用 Rust env_logger，預設 INFO/WARN/ERROR 都寫 stderr；stdout 通常空
        var combined = stdout.Length + stderr.Length > 0
            ? $"{stdout}{stderr}"
            : "";
        return (proc.ExitCode == 0, combined);
    }

    /// <summary>打包 outputDir 資料夾內容成 .it 檔案</summary>
    public void Pack(string inputDir, string outputItPath)
    {
        Log.Section($"Pack {inputDir} -> {outputItPath}");

        if (!File.Exists(_mabiPackPath))
            throw new FileNotFoundException($"找不到 mabi-pack2.exe：{_mabiPackPath}");

        var args = $"pack -i \"{inputDir}\" -o \"{outputItPath}\" -k \"{ModSalt}\"";

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = _mabiPackPath,
                Arguments              = args,
                WorkingDirectory       = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        var sw = Stopwatch.StartNew();
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(ProcessTimeoutMs))
        {
            Log.Error($"Pack timeout ({ProcessTimeoutMs}ms)");
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new System.Exception($"打包逾時 ({ProcessTimeoutMs}ms)");
        }
        proc.WaitForExit();
        sw.Stop();

        if (proc.ExitCode != 0)
        {
            Log.Error($"Pack 失敗 exit={proc.ExitCode}: {stderr}");
            throw new System.Exception($"打包失敗:\n{stderr}");
        }
        Log.Info($"Pack OK ({sw.ElapsedMilliseconds}ms)");
    }
}
