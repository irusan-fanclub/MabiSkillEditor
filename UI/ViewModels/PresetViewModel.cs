using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.UI.ViewModels;

public enum PresetStatus { NotLoaded, NotApplied, Applied, SkillMissing }

/// <summary>單張範本卡片的 view model</summary>
public class PresetViewModel : INotifyPropertyChanged
{
    public SkillPreset Preset { get; }

    public PresetViewModel(SkillPreset preset)
    {
        Preset = preset;
        _changes = BuildChanges(preset);
    }

    public string Name    => Preset.Name;
    public int    SkillID => Preset.SkillID;

    private string _skillName = "(未載入)";
    public string SkillName
    {
        get => _skillName;
        set
        {
            if (_skillName == value) return;
            _skillName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SkillIDLabel));
        }
    }

    public string SkillIDLabel => $"[{SkillID}] {_skillName}";

    private readonly IReadOnlyList<string> _changes;
    public IReadOnlyList<string> Changes => _changes;

    private PresetStatus _status = PresetStatus.NotLoaded;
    public PresetStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(CanApply));
        }
    }

    public bool CanApply => _status == PresetStatus.NotApplied;

    public string StatusLabel => _status switch
    {
        PresetStatus.NotApplied   => "套用",
        PresetStatus.Applied      => "已套用 ✓",
        PresetStatus.SkillMissing => "⚠ 找不到此技能",
        PresetStatus.NotLoaded    => "請先載入",
        _ => "?",
    };

    private static IReadOnlyList<string> BuildChanges(SkillPreset p)
    {
        var list = new List<string>();
        if (p.WeaponStringID != null)
            list.Add(string.IsNullOrEmpty(p.WeaponStringID)
                ? "→ WeaponStringID = (清空)"
                : $"→ WeaponStringID = {p.WeaponStringID}");
        if (p.TargetPreference != null)
            list.Add(string.IsNullOrEmpty(p.TargetPreference)
                ? "→ TargetPreference = (清空)"
                : $"→ TargetPreference = {p.TargetPreference}");
        return list;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
