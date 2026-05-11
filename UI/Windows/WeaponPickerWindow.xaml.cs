using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MabiSkillEditor.Core.Models;

namespace MabiSkillEditor.UI.Windows;

public partial class WeaponPickerWindow : Window
{
    /// <summary>
    /// 反灰禁選的 tag：作為技能武器限制毫無意義或已在主畫面有對應控制項。
    /// 想增刪改這裡。value 是 block 原因（暫不顯示給使用者，僅供日後 debug／重啟 hover 用）。
    /// </summary>
    private static readonly Dictionary<string, string> BlockedTags = new()
    {
        ["equip"]     = "所有裝備都有，限制無意義",
        ["weapon"]    = "所有武器都有，限制無意義",
        ["righthand"] = "已有持握 radio 可選",
        ["twohand"]   = "已有持握 radio 可選",
    };

    /// <summary>
    /// Tag 的中文說明。未來在 row 上顯示在 /tag/ 之後（小字、灰色）。
    /// 目前空，逐步補。例：["Brionac"] = "布里歐納克（金棒）"
    /// </summary>
    private static readonly Dictionary<string, string> TagDescriptions = new()
    {
        // ["Brionac"]      = "布里歐納克（金棒）",
        // ["healing_wand"] = "治癒杖",
    };

    private readonly IReadOnlyList<Weapon> _allWeapons;
    private readonly IReadOnlyDictionary<string, List<Weapon>> _byTag;

    /// <summary>使用者按「加入」後，這個 list 含選定的 tag（不含 slash）</summary>
    public List<string> SelectedTags { get; private set; } = new();

    public WeaponPickerWindow(IReadOnlyList<Weapon> weapons,
                              IReadOnlyDictionary<string, List<Weapon>> byTag,
                              IEnumerable<(string Tag, string Display)> typeFilters)
    {
        _allWeapons = weapons;
        _byTag      = byTag;
        InitializeComponent();

        // 類型 dropdown：「全部」+ 傳入的類型列表
        var items = new List<TypeFilterItem> { new("", "全部") };
        foreach (var (tag, display) in typeFilters)
            items.Add(new TypeFilterItem(tag, display));
        CmbType.ItemsSource       = items;
        CmbType.DisplayMemberPath = nameof(TypeFilterItem.Display);
        CmbType.SelectedIndex     = 0;

        StateChanged += (_, _) =>
            BtnMax.Content = WindowState == WindowState.Maximized ? "" : "";

        ApplyFilter();
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMax_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private record TypeFilterItem(string Tag, string Display);

    private void Filter_Changed(object sender, EventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (CmbType == null || TxtSearch == null) return;

        var typeTag = (CmbType.SelectedItem as TypeFilterItem)?.Tag ?? "";
        var query   = TxtSearch.Text?.Trim() ?? "";

        IEnumerable<Weapon> source = _allWeapons;
        if (!string.IsNullOrEmpty(typeTag))
            source = source.Where(w => w.Tags.Contains(typeTag));
        if (!string.IsNullOrEmpty(query))
        {
            var q = query.ToLowerInvariant();
            source = source.Where(w =>
                w.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.EngName.ToLowerInvariant().Contains(q) ||
                w.ID.ToString().Contains(q));
        }
        var list = source.OrderBy(w => w.ID).ToList();
        WeaponList.ItemsSource = list;
        TxtMatchCount.Text     = $"{list.Count} / {_allWeapons.Count}";

        // Filter 變更後 tag 顯示也清空
        TagList.ItemsSource           = null;
        TagWeaponList.ItemsSource     = null;
        TxtTagWeaponsHint.Visibility  = Visibility.Visible;
    }

    private void WeaponList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WeaponList.SelectedItem is not Weapon w)
        {
            TagList.ItemsSource = null;
            return;
        }
        var prev = (TagList.ItemsSource as List<TagItem>)?
                   .Where(t => t.IsChecked).Select(t => t.Tag).ToHashSet()
                   ?? new HashSet<string>();
        var items = w.Tags.Select(t =>
        {
            BlockedTags.TryGetValue(t, out var reason);
            TagDescriptions.TryGetValue(t, out var desc);
            return new TagItem(t, reason, desc) { IsChecked = prev.Contains(t) };
        }).ToList();
        TagList.ItemsSource = items;
        TxtTagsHeader.Text  = $"{w.Name} 的 tag";

        TagWeaponList.ItemsSource    = null;
        TxtTagWeaponsHint.Visibility = Visibility.Visible;
        TxtTagWeaponsHeader.Text     = "含此 tag 的武器（前 15）";
    }

    private void TagList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TagList.SelectedItem is not TagItem t)
        {
            TagWeaponList.ItemsSource    = null;
            TxtTagWeaponsHint.Visibility = Visibility.Visible;
            return;
        }
        if (!_byTag.TryGetValue(t.Tag, out var list))
            list = new List<Weapon>();

        TagWeaponList.ItemsSource    = list.Take(15).ToList();
        TxtTagWeaponsHeader.Text     = $"含 /{t.Tag}/ 的武器（前 15 / 共 {list.Count}）";
        TxtTagWeaponsHint.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (list.Count == 0) TxtTagWeaponsHint.Text = "（沒有武器有此 tag）";
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        // 只取「未被 block」且勾選的 tag
        SelectedTags = (TagList.ItemsSource as List<TagItem>)?
                       .Where(t => t.IsChecked && t.IsSelectable)
                       .Select(t => t.Tag)
                       .ToList() ?? new List<string>();
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 單一 tag 列。
    /// - <see cref="BlockedReason"/> 非 null = block，checkbox disabled、label 反灰。
    /// - <see cref="Description"/> 非 null = row 後方加顯示一段中文說明（目前未渲染，預留 slot）。
    /// </summary>
    private class TagItem : INotifyPropertyChanged
    {
        public string  Tag           { get; }
        public string  Display       => $"/{Tag}/";
        public bool    IsSelectable  { get; }
        public string? BlockedReason { get; }
        public string? Description   { get; }
        public Visibility DescriptionVisibility
            => string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        public System.Windows.Media.Brush LabelForeground
            => IsSelectable
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xcd, 0xd6, 0xf4))   // normal
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6c, 0x70, 0x86));  // muted

        private bool _checked;
        public bool IsChecked
        {
            get => _checked;
            set { _checked = value; OnPropertyChanged(); }
        }

        public TagItem(string tag, string? blockedReason, string? description)
        {
            Tag           = tag;
            BlockedReason = blockedReason;
            Description   = description;
            IsSelectable  = blockedReason == null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
