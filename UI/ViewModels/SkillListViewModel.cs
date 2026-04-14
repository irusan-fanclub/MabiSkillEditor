using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.UI.ViewModels;

public class SkillListViewModel : INotifyPropertyChanged
{
    // ── 全部技能（載入後不變）─────────────────────────
    private List<SkillEntry> _allSkills = new();

    // ── 顯示用（篩選後）──────────────────────────────
    public ObservableCollection<SkillEntry> DisplayedSkills { get; } = new();

    // ── 所有天賦欄位 key（給 UI 動態生成欄位用）────────
    public List<string> TalentKeys { get; private set; } = new();

    // ── 搜尋字串 ─────────────────────────────────────
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    // ── 選取的技能 ────────────────────────────────────
    private SkillEntry? _selectedSkill;
    public SkillEntry? SelectedSkill
    {
        get => _selectedSkill;
        set { _selectedSkill = value; OnPropertyChanged(); }
    }

    // ── 狀態訊息 ──────────────────────────────────────
    private string _statusMessage = "請載入 SkillInfo.xml";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public void LoadSkills(List<SkillEntry> skills, List<string> talentKeys)
    {
        _allSkills  = skills;
        TalentKeys  = talentKeys;
        StatusMessage = $"已載入 {skills.Count} 個技能";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _searchText.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allSkills
            : _allSkills.Where(s =>
                s.SkillID.ToString().Contains(q) ||
                s.EngName.ToLowerInvariant().Contains(q) ||
                s.LocalName.Contains(q));

        DisplayedSkills.Clear();
        foreach (var s in filtered)
            DisplayedSkills.Add(s);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
