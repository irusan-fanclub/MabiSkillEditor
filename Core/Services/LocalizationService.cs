using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MabiSkillEditor.Core.Services;

/// <summary>
/// 讀取 SkillInfo.taiwan.txt，解析 _LT[xml.skillinfo.N] 對照
/// </summary>
public class LocalizationService
{
    private readonly Dictionary<int, string> _table = new();

    public void Load(string txtPath)
    {
        _table.Clear();
        var lines = File.ReadAllLines(txtPath, Encoding.UTF8);
        foreach (var line in lines)
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var idx))
                _table[idx] = parts[1].Trim();
        }
    }

    /// <summary>
    /// 輸入 "_LT[xml.skillinfo.271]" 回傳中文字串；
    /// 若不是此格式則原樣回傳
    /// </summary>
    public string Resolve(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        var m = Regex.Match(key, @"_LT\[xml\.skillinfo\.(\d+)\]");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
            return _table.TryGetValue(idx, out var val) ? val : key;
        return key;
    }
}
