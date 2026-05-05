using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MabiSkillEditor.Core.Models;
using MabiSkillEditor.Core.Services;

namespace MabiSkillEditor.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // ── Services ─────────────────────────────────────
    private readonly LocalizationService _loc  = new();
    private readonly SkillXmlParser      _parser;
    public  readonly EditSession          Session = new();

    private string _originXmlPath = "";

    public MainViewModel()
    {
        _parser = new SkillXmlParser(_loc);
        var cfg = ConfigService.LoadConfig();
        GameFolder   = cfg.GameFolder;
        OutputItName = cfg.OutputItName;
    }

    // ── 設定欄位 ─────────────────────────────────────

    private string _gameFolder = "";
    public string GameFolder
    {
        get => _gameFolder;
        set { _gameFolder = value; OnPropertyChanged(); }
    }

    private string _outputItName = "skill_mod";
    public string OutputItName
    {
        get => _outputItName;
        set { _outputItName = value; OnPropertyChanged(); }
    }

    // ── 技能資料 ──────────────────────────────────────

    private List<SkillEntry> _allSkills = new();
    public List<string> TalentKeys { get; private set; } = new();

    public ObservableCollection<SkillEntry> DisplayedSkills { get; } = new();
    public ObservableCollection<SkillEdit>  ModifiedSkills  { get; } = new();

    // ── 搜尋 ─────────────────────────────────────────

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    private bool _showMonsterSkills;
    public bool ShowMonsterSkills
    {
        get => _showMonsterSkills;
        set { _showMonsterSkills = value; OnPropertyChanged(); ApplyFilter(); }
    }

    // ── 選取的技能 ────────────────────────────────────

    private SkillEntry? _selectedSkill;
    public SkillEntry? SelectedSkill
    {
        get => _selectedSkill;
        set { _selectedSkill = value; OnPropertyChanged(); LoadEditForSelected(); }
    }

    // 右側編輯表單目前綁定的 SkillEdit
    private SkillEdit? _currentEdit;
    public SkillEdit? CurrentEdit
    {
        get => _currentEdit;
        set { _currentEdit = value; OnPropertyChanged(); }
    }

    // ── 狀態 ─────────────────────────────────────────

    private string _status = "請設定遊戲資料夾並點選「載入」";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool IsLoaded => _allSkills.Count > 0;

    // ── 載入 ─────────────────────────────────────────

    public async Task LoadAsync(IProgress<string> progress)
    {
        if (string.IsNullOrWhiteSpace(GameFolder))
            throw new InvalidOperationException("請先設定遊戲資料夾");

        var sources    = ConfigService.LoadSources();
        var extractor  = new PackExtractService(GameFolder, ConfigService.OriginDir);
        var gameFolder = GameFolder;

        string originXmlPath = "";
        List<SkillEntry>  skills    = new();
        List<string>      talentKeys = new();

        // ── 重量 I/O 在背景執行緒 ──────────────────────
        bool sourcesChanged = false;
        await Task.Run(() =>
        {
            progress.Report("正在解包 SkillInfo.xml...");
            var (xmlPath, xmlSalt) = extractor.Extract(
                sources.SkillInfoIt, sources.SkillInfoInnerPath, sources.KnownSalts);
            originXmlPath = xmlPath;
            if (xmlSalt != null && HoistSalt(sources.KnownSalts, xmlSalt))
                sourcesChanged = true;

            progress.Report("正在解包本地化檔案...");
            var (locPath, locSalt) = extractor.Extract(
                sources.LocalizationIt, sources.LocalizationInnerPath, sources.KnownSalts);
            _loc.Load(locPath);
            if (locSalt != null && HoistSalt(sources.KnownSalts, locSalt))
                sourcesChanged = true;

            progress.Report("正在解析 XML...");
            (skills, talentKeys) = _parser.Parse(originXmlPath);
        });

        // 把實際成功的 salt 排到首位並持久化（清掉舊欄位、寫成新 list 格式）
        if (sourcesChanged)
            ConfigService.SaveSources(sources);

        // ── 回到 UI 執行緒更新集合 ──────────────────────
        _originXmlPath = originXmlPath;
        _allSkills     = skills;
        TalentKeys     = talentKeys;

        Session.Clear();
        ModifiedSkills.Clear();
        ApplyFilter();

        ConfigService.SaveConfig(new AppConfig
        {
            GameFolder   = GameFolder,
            OutputItName = OutputItName,
        });

        Status = $"已載入 {skills.Count} 個技能";
        OnPropertyChanged(nameof(IsLoaded));
    }

    // ── 輸出 ─────────────────────────────────────────

    public async Task<string> ExportAsync(IProgress<string> progress)
    {
        if (!IsLoaded) throw new InvalidOperationException("尚未載入技能資料");
        if (Session.AllEdits.Count == 0) throw new InvalidOperationException("沒有任何修改");

        var itName = OutputItName.TrimEnd('.').Trim();
        if (string.IsNullOrEmpty(itName)) itName = "skill_mod";
        var outDir   = ConfigService.OutputDir;
        // stageDir 只放要打包的內容（data/...），不放 IT 或 diff
        var stageDir = Path.Combine(outDir, "stage");
        var sources  = ConfigService.LoadSources();
        // 根據 sources.json 的 innerPath 決定輸出位置，確保 IT 內部路徑正確
        var xmlOut   = Path.Combine(stageDir,
            sources.SkillInfoInnerPath.Replace('/', Path.DirectorySeparatorChar));
        var diffOut  = Path.Combine(outDir, "diff.txt");
        var itOut    = Path.Combine(outDir, $"{itName}_0.it");

        // 在呼叫前快照，避免在背景執行緒存取 UI 集合
        var edits      = Session.AllEdits.ToList();
        var originPath = _originXmlPath;
        var gameFolder = GameFolder;
        List<SkillXmlParser.DiffEntry> diffs = new();

        await Task.Run(() =>
        {
            progress.Report("正在寫出 SkillInfo.xml...");
            diffs = _parser.Save(originPath, xmlOut, edits);

            progress.Report("正在寫出 diff.txt...");
            Session.WriteDiff(diffOut, diffs);

            progress.Report("正在打包 .it...");
            var extractor = new PackExtractService(gameFolder, ConfigService.OriginDir);
            // 打包 stageDir（不含 diff、IT 本身），IT 輸出到 stageDir 外
            extractor.Pack(stageDir, itOut);
        });

        Status = $"輸出完成！共 {diffs.Count} 個技能修改 → {itOut}";
        return itOut;
    }

    // ── 編輯表單操作 ──────────────────────────────────

    private void LoadEditForSelected()
    {
        if (_selectedSkill == null) { CurrentEdit = null; return; }
        // 若已有修改記錄，載入；否則新建（帶入原始值）
        CurrentEdit = Session.GetOrCreate(_selectedSkill);
    }

    public void CommitCurrentEdit()
    {
        if (CurrentEdit == null) return;
        Session.Commit(CurrentEdit);
        RefreshModifiedList();
    }

    public void RevertCurrentEdit()
    {
        if (_selectedSkill == null) return;
        Session.Remove(_selectedSkill.SkillID);
        CurrentEdit = Session.GetOrCreate(_selectedSkill);
        RefreshModifiedList();
    }

    private void RefreshModifiedList()
    {
        ModifiedSkills.Clear();
        foreach (var edit in Session.AllEdits.OrderBy(e => e.Original.SkillID))
            ModifiedSkills.Add(edit);
    }

    // ── 篩選 ─────────────────────────────────────────

    // ── Diff 載入 ────────────────────────────────────

    public void LoadDiff(string diffPath)
    {
        if (!IsLoaded) throw new InvalidOperationException("請先載入技能資料");

        var changes    = DiffImportService.Parse(diffPath);
        var lookup     = _allSkills.ToDictionary(s => s.SkillID);
        int applied    = 0;

        foreach (var group in changes.GroupBy(c => c.SkillId))
        {
            if (!lookup.TryGetValue(group.Key, out var skill)) continue;

            var edit = Session.GetOrCreate(skill);
            foreach (var c in group)
            {
                switch (c.Field)
                {
                    case "WeaponStringID":   edit.WeaponStringID   = c.NewValue; break;
                    case "TargetPreference": edit.TargetPreference = c.NewValue; break;
                    case "IsHidden":         edit.IsHidden = c.NewValue == "True"; break;
                    // 進階分類欄位（暫時停用）
                    // case "SkillType":        edit.SkillType        = c.NewValue; break;
                    // case "TriggerType":      edit.TriggerType      = c.NewValue; break;
                    // case "SkillCategory":    edit.SkillCategory    = c.NewValue; break;
                }
            }
            Session.Commit(edit);
            applied++;
        }

        RefreshModifiedList();
        Status = $"已載入修改記錄，套用 {applied} 個技能變更";
        OnPropertyChanged(nameof(IsLoaded));
    }

    // ── 篩選 ─────────────────────────────────────────

    private void ApplyFilter()
    {
        var q = _searchText.Trim().ToLowerInvariant();

        // 預設排除怪物/寵物技能（AvailableRace <= 0：== 0 或未設定），勾選後顯示
        var source = _showMonsterSkills
            ? (IEnumerable<SkillEntry>)_allSkills
            : _allSkills.Where(s => s.AvailableRace > 0);

        var filtered = string.IsNullOrEmpty(q)
            ? source
            : source.Where(s =>
                s.SkillID.ToString().Contains(q) ||
                s.EngName.ToLowerInvariant().Contains(q) ||
                s.LocalName.Contains(q));

        DisplayedSkills.Clear();
        foreach (var s in filtered)
            DisplayedSkills.Add(s);
    }

    // 把成功的 salt 移到 list 首位；若 list 已是該順序則回傳 false
    private static bool HoistSalt(List<string> salts, string used)
    {
        if (salts.Count > 0 && salts[0] == used) return false;
        salts.Remove(used);
        salts.Insert(0, used);
        return true;
    }

    // ── INotifyPropertyChanged ────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
