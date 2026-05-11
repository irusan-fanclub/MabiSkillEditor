using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MabiSkillEditor.Core.Models;

public class WeaponCheckItem : INotifyPropertyChanged
{
    public string Tag         { get; }
    public string DisplayName { get; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public WeaponCheckItem(string tag, string displayName)
    {
        Tag         = tag;
        DisplayName = displayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
