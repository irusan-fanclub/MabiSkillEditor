using System;
using System.IO;
using System.Threading;

namespace MabiSkillEditor.Core.Services;

/// <summary>
/// 簡易 thread-safe append-only file logger。輸出到 AppDir/log.txt。
/// </summary>
public static class Log
{
    private static readonly object _lock = new();
    private static string? _path;

    public static string Path => _path ??= System.IO.Path.Combine(ConfigService.AppDir, "log.txt");

    public static void Section(string title) => Write($"\n=== {title} ===");
    public static void Info(string msg)      => Write($"[INF] {msg}");
    public static void Warn(string msg)      => Write($"[WRN] {msg}");
    public static void Error(string msg)     => Write($"[ERR] {msg}");

    public static void Error(string msg, Exception ex)
    {
        Write($"[ERR] {msg}: {ex.GetType().Name}: {ex.Message}");
        Write(ex.StackTrace ?? "(no stack)");
    }

    private static void Write(string line)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var tid   = Thread.CurrentThread.ManagedThreadId;
        var text  = $"{stamp} t{tid,2} {line}{Environment.NewLine}";
        lock (_lock)
        {
            try { File.AppendAllText(Path, text); }
            catch { /* logger 自己不能死 */ }
        }
    }
}
