using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MabiSkillEditor.Core.Models;
using MabiSkillEditor.UI.ViewModels;
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace MabiSkillEditor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private bool _suppressEditEvents;

    // ── 武器 token 清單（不含持握方式 righthand/twohand，那兩個移到 Radio）──
    private static readonly List<WeaponCheckItem> WeaponCatalog = new()
    {
        new("/staff/",              "魔杖"),
        new("/wand/",               "單手杖"),
        new("/bow/",                "弓"),
        new("/crossbow/",           "弩"),
        new("/blade/",              "單手劍"),
        new("/blunt/",              "鈍器"),
        new("/axe/",                "斧"),
        new("/lance/",              "槍"),
        new("/knuckle/",            "拳套"),
        new("/chainblade/",         "鏈刃"),
        new("/scythe/",             "大鐮刀"),
        new("/magical_scythe/",     "魔法大鐮刀"),
        new("/dualgun/",            "雙槍"),
        new("/cylinder_repairable/","彈夾"),
        new("/atlatl/",             "投槍器"),
        new("/fynnbell/",           "芬貝爾"),
        new("/shuriken/",           "手裏劍"),
        new("/handle/",             "把手"),
        new("/weapontype_combat/",  "近戰武器"),
    };

    private static readonly HashSet<string> HandTokens =
        new() { "/righthand/", "/twohand/" };

    private static readonly HashSet<string> CatalogTokens =
        new(WeaponCatalog.Select(w => w.Token));

    public MainWindow()
    {
        InitializeComponent();

        EWeaponList.ItemsSource = WeaponCatalog;

        // 即時更新預覽
        foreach (var item in WeaponCatalog)
            item.PropertyChanged += (_, _) => UpdateWeaponPreview();

        TxtGameFolder.Text = _vm.GameFolder;
        TxtItName.Text     = _vm.OutputItName;
        TxtStatus.Text     = _vm.Status;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Status))
                TxtStatus.Text = _vm.Status;
            if (e.PropertyName == nameof(MainViewModel.CurrentEdit))
                RefreshEditForm();
        };

        SkillGrid.ItemsSource    = _vm.DisplayedSkills;
        ModifiedGrid.ItemsSource = _vm.ModifiedSkills;
    }

    // ── 工具列 ───────────────────────────────────────

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description            = "選擇 Mabinogi 遊戲資料夾",
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK)
        {
            TxtGameFolder.Text = dlg.SelectedPath;
            _vm.GameFolder     = dlg.SelectedPath;
        }
    }

    private async void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        _vm.GameFolder   = TxtGameFolder.Text;
        _vm.OutputItName = TxtItName.Text;
        var progress = new Progress<string>(msg =>
        {
            LoadingText.Text = msg;
            _vm.Status       = msg;
        });
        ShowOverlay("正在初始化...");
        try   { await _vm.LoadAsync(progress); }
        catch (Exception ex)
        { WinMsgBox.Show(ex.Message, "載入失敗", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { HideOverlay(); }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        _vm.OutputItName = TxtItName.Text;
        var progress = new Progress<string>(msg =>
        {
            LoadingText.Text = msg;
            _vm.Status       = msg;
        });
        ShowOverlay("正在輸出...");
        try
        {
            await _vm.ExportAsync(progress);
            WinMsgBox.Show(_vm.Status, "輸出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        { WinMsgBox.Show(ex.Message, "輸出失敗", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { HideOverlay(); }
    }

    private void BtnLoadDiff_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.OpenFileDialog
        {
            Title  = "選擇修改記錄（diff.txt）",
            Filter = "文字檔 (*.txt)|*.txt|所有檔案 (*.*)|*.*",
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
        try
        {
            _vm.LoadDiff(dlg.FileName);
            WinMsgBox.Show(_vm.Status, "載入成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        { WinMsgBox.Show(ex.Message, "載入失敗", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // ── Overlay 控制 ─────────────────────────────────

    private void ShowOverlay(string message)
    {
        LoadingText.Text          = message;
        LoadingOverlay.Visibility = Visibility.Visible;
        SetToolbarEnabled(false);
    }

    private void HideOverlay()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        SetToolbarEnabled(true);
    }

    private void SetToolbarEnabled(bool enabled)
    {
        BtnLoad.IsEnabled      = enabled;
        BtnExport.IsEnabled    = enabled;
        BtnLoadDiff.IsEnabled  = enabled;
    }

    // ── 搜尋 ─────────────────────────────────────────

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => _vm.SearchText = TxtSearch.Text;

    // ── 技能清單選取 ──────────────────────────────────

    private void SkillGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkillGrid.SelectedItem is not SkillEntry skill) return;
        _vm.SelectedSkill = skill;
        ShowOriginal(skill);
    }

    // ── 已修改清單選取 ────────────────────────────────

    private void ModifiedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModifiedGrid.SelectedItem is not SkillEdit edit) return;
        var match = _vm.DisplayedSkills.FirstOrDefault(s => s.SkillID == edit.Original.SkillID);
        if (match != null) SkillGrid.SelectedItem = match;
        _vm.SelectedSkill = edit.Original;
        ShowOriginal(edit.Original);
    }

    // ── 編輯按鈕 ─────────────────────────────────────

    private void BtnCommit_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentEdit == null) return;
        _vm.CurrentEdit.WeaponStringID   = BuildWeaponString();
        _vm.CurrentEdit.TargetPreference = ETarget.Text;
        // IsHidden 不從 UI 讀取，保留原始值
        _vm.CommitCurrentEdit();
    }

    private void BtnRevert_Click(object sender, RoutedEventArgs e)
        => _vm.RevertCurrentEdit();

    // ── 武器 UI 邏輯 ─────────────────────────────────

    private void HandType_Checked(object sender, RoutedEventArgs e)
        => UpdateWeaponPreview();

    private void EWeaponCustom_TextChanged(object sender, TextChangedEventArgs e)
        => UpdateWeaponPreview();

    /// <summary>
    /// 從持握 Radio + 武器 CheckBox + 自訂欄 組裝 WeaponStringID 字串。
    /// 規則：hand & weapon1 | hand & weapon2 | ... | custom
    /// </summary>
    private string BuildWeaponString()
    {
        var hand    = GetSelectedHand();
        var weapons = WeaponCatalog.Where(w => w.IsChecked).Select(w => w.Token).ToList();
        var custom  = EWeaponCustom?.Text?.Trim() ?? "";

        string weaponPart;
        if (hand != "" && weapons.Count > 0)
            weaponPart = string.Join(" | ", weapons.Select(w => $"{hand} & {w}"));
        else if (hand != "")
            weaponPart = hand;
        else if (weapons.Count > 0)
            weaponPart = string.Join(" | ", weapons);
        else
            weaponPart = "";

        if (custom == "") return weaponPart;
        return weaponPart == "" ? custom : $"{weaponPart} | {custom}";
    }

    private string GetSelectedHand()
    {
        if (RHandSingle?.IsChecked == true) return "/righthand/";
        if (RHandDouble?.IsChecked == true) return "/twohand/";
        return "";
    }

    private void UpdateWeaponPreview()
    {
        if (_suppressEditEvents || EWeaponPreview == null) return;
        EWeaponPreview.Text = BuildWeaponString();
    }

    // ── 解析 WeaponStringID → 回填 UI ────────────────

    private void ParseWeaponStringToUi(string raw)
    {
        // 清空
        RHandNone.IsChecked = true;
        foreach (var w in WeaponCatalog) w.IsChecked = false;
        EWeaponCustom.Text = "";

        if (string.IsNullOrEmpty(raw)) return;

        // 把 XML entity decode（&amp; → &）
        raw = raw.Replace("&amp;", "&");

        var handFound   = "";
        var customParts = new List<string>();

        // 以 " | " 分割各 token 片段
        var segments = raw.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            var s = seg.Trim();
            if (s.Contains(" & "))
            {
                // hand & weapon 對
                var idx  = s.IndexOf(" & ", StringComparison.Ordinal);
                var left = s[..idx].Trim();
                var right = s[(idx + 3)..].Trim();

                if (HandTokens.Contains(left))
                {
                    handFound = left;
                    if (CatalogTokens.Contains(right))
                        WeaponCatalog.First(w => w.Token == right).IsChecked = true;
                    else
                        customParts.Add(s);
                }
                else
                {
                    customParts.Add(s);
                }
            }
            else if (HandTokens.Contains(s))
            {
                handFound = s;
            }
            else if (CatalogTokens.Contains(s))
            {
                WeaponCatalog.First(w => w.Token == s).IsChecked = true;
            }
            else
            {
                customParts.Add(s);
            }
        }

        if (handFound == "/righthand/")   RHandSingle.IsChecked = true;
        else if (handFound == "/twohand/") RHandDouble.IsChecked = true;

        EWeaponCustom.Text = string.Join(" | ", customParts);
    }

    // ── UI 更新 ───────────────────────────────────────

    private void ShowOriginal(SkillEntry skill)
    {
        OTitle.Text      = $"[{skill.SkillID}] {skill.DisplayName}";
        OID.Text         = skill.SkillID.ToString();
        OEng.Text        = skill.EngName;
        OHidden.Text     = skill.IsHidden ? "True（隱藏）" : "False";
        OAvailRace.Text  = skill.AvailableRaceDisplay;
        OWeapon.Text     = string.IsNullOrEmpty(skill.WeaponStringID) ? "（無限制）" : skill.WeaponStringID;
        OTarget.Text     = string.IsNullOrEmpty(skill.TargetPreference) ? "（未設定）" : skill.TargetPreference;
        OTalents.ItemsSource = skill.TalentValues
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key)
            .ToList();
    }

    private void RefreshEditForm()
    {
        var edit = _vm.CurrentEdit;
        if (edit == null)
        {
            ETitle.Text = "選擇技能後在此編輯";
            ETarget.Text = "";
            _suppressEditEvents = true;
            RHandNone.IsChecked = true;
            foreach (var w in WeaponCatalog) w.IsChecked = false;
            EWeaponCustom.Text  = "";
            EWeaponPreview.Text = "";
            _suppressEditEvents = false;
            return;
        }

        _suppressEditEvents = true;
        ETitle.Text  = $"編輯：[{edit.Original.SkillID}] {edit.Original.DisplayName}";
        ETarget.Text = edit.TargetPreference;
        ParseWeaponStringToUi(edit.WeaponStringID ?? "");
        _suppressEditEvents = false;

        UpdateWeaponPreview();
    }
}
