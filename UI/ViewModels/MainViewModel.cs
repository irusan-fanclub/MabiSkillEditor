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
        LoadPresets();
    }

    // ── 設定欄位 ─────────────────────────────────────

    private string _gameFolder = "";
    public string GameFolder
    {
        get => _gameFolder;
        set { _gameFolder = value; OnPropertyChanged(); }
    }

    private int? _gameVersion;
    public int? GameVersion
    {
        get => _gameVersion;
        private set { _gameVersion = value; OnPropertyChanged(); }
    }

    private void RefreshGameVersion()
        => GameVersion = ConfigService.ReadGameVersion(_gameFolder);

    private string _outputItName = "skill_mod";
    public string OutputItName
    {
        get => _outputItName;
        set { _outputItName = value; OnPropertyChanged(); }
    }

    // ── 技能資料 ──────────────────────────────────────

    private List<SkillEntry> _allSkills = new();
    public List<string> TalentKeys { get; private set; } = new();

    public ObservableCollection<SkillEntry>      DisplayedSkills { get; } = new();
    public ObservableCollection<SkillEdit>       ModifiedSkills  { get; } = new();
    public ObservableCollection<PresetViewModel> Presets         { get; } = new();

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

    /// <summary>LoadAsync 結束後若偵測到 .it 衝突，這裡會放一段警告文字（UI 應彈 MessageBox）。否則為 null。</summary>
    public string? LoadWarning { get; private set; }

    public bool IsLoaded => _allSkills.Count > 0;

    // ── 載入 ─────────────────────────────────────────

    public async Task LoadAsync(IProgress<string> progress)
    {
        Log.Section($"LoadAsync 開始 GameFolder={GameFolder}");
        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(GameFolder))
            throw new InvalidOperationException("請先設定遊戲資料夾");

        var sources    = ConfigService.LoadSources();
        var extractor  = new PackExtractService(GameFolder, ConfigService.OriginDir);
        var gameFolder = GameFolder;
        Log.Info($"sources.KnownSalts.Count={sources.KnownSalts.Count}");

        string originXmlPath = "";
        List<SkillEntry>  skills    = new();
        List<string>      talentKeys = new();
        ScanResult? scan = null;

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

            Log.Section("Localization Load");
            var locSw = System.Diagnostics.Stopwatch.StartNew();
            _loc.Load(locPath);
            Log.Info($"Localization load OK ({locSw.ElapsedMilliseconds}ms)");

            if (locSalt != null && HoistSalt(sources.KnownSalts, locSalt))
                sourcesChanged = true;

            progress.Report("正在解析 XML...");
            Log.Section($"Parse XML {originXmlPath}");
            var parseSw = System.Diagnostics.Stopwatch.StartNew();
            (skills, talentKeys) = _parser.Parse(originXmlPath);
            Log.Info($"Parse OK ({parseSw.ElapsedMilliseconds}ms) skills={skills.Count} talentKeys={talentKeys.Count}");

            progress.Report("正在掃描內附修改記錄...");
            scan = ScanForEmbeddedDiff(extractor, gameFolder, sources.KnownSalts);
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

        RefreshGameVersion();
        Log.Info($"GameVersion={(GameVersion?.ToString() ?? "(unknown)")}");

        var skillCount = skills.Count;
        var statusBase = $"已載入 {skillCount} 個技能";

        LoadWarning = null;
        int? appliedFromEmbedded = null;

        if (scan != null && scan.HasConflict)
        {
            LoadWarning = BuildConflictWarning(scan);
            Log.Warn(LoadWarning);
        }
        else if (scan != null && scan.Embedded.HasValue)
        {
            try
            {
                var changes = DiffImportService.Parse(scan.Embedded.Value.DiffPath);
                appliedFromEmbedded = ApplyChanges(changes);
                var newOutputName = StripPackSuffix(scan.Embedded.Value.ItFileName);
                if (!string.IsNullOrEmpty(newOutputName))
                    OutputItName = newOutputName;
                Log.Info($"已套用內附修改 {appliedFromEmbedded} 個，來源 {scan.Embedded.Value.ItFileName}，OutputItName={OutputItName}");
            }
            catch (Exception ex)
            {
                Log.Warn($"套用內附修改失敗: {ex.Message}");
            }
        }

        Status = (LoadWarning, appliedFromEmbedded) switch
        {
            ({ } w, _) when w != null
                => $"{statusBase}（偵測到 .it 衝突，未自動套用）",
            (null, { } n)
                => $"{statusBase}；自動套用內附修改 {n} 個（來源：{scan!.Embedded!.Value.ItFileName}）",
            _   => statusBase,
        };
        OnPropertyChanged(nameof(IsLoaded));
        RefreshPresetStates();
        totalSw.Stop();
        Log.Info($"LoadAsync 完成 ({totalSw.ElapsedMilliseconds}ms)");
    }

    private record ScanResult(
        (string ItFileName, string DiffPath)? Embedded,
        List<string> SkillXmlMods,
        List<string> EditsJsonMods)
    {
        public bool HasConflict => SkillXmlMods.Count > 1 || EditsJsonMods.Count > 1;
    }

    /// <summary>
    /// 掃描 package 內所有非 data_*.it：
    /// - 記錄含 data/db/Skill/SkillInfo.xml 的檔案 (skillXmlMods)
    /// - 記錄含 meta/skill-edits.json 的檔案 (editsJsonMods)
    /// 任一 list 大於 1 視為衝突，回傳 Embedded=null 讓上層警告且不自動套用。
    /// 無衝突且 editsJsonMods 恰好 1 個時，extract 該檔的 skill-edits.json 並回傳。
    /// </summary>
    private static ScanResult ScanForEmbeddedDiff(
        PackExtractService extractor, string gameFolder, IReadOnlyList<string> salts)
    {
        var skillXml  = new List<string>();
        var editsJson = new List<string>();
        var emptyResult = new ScanResult(null, skillXml, editsJson);

        var packageDir = System.IO.Path.Combine(gameFolder, "package");
        if (!System.IO.Directory.Exists(packageDir)) return emptyResult;

        var dataPattern = new System.Text.RegularExpressions.Regex(
            @"^data_\d+\.it$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var editsCandidates = new List<(string Name, DateTime Mtime)>();
        foreach (var path in System.IO.Directory.EnumerateFiles(packageDir, "*.it"))
        {
            var name = System.IO.Path.GetFileName(path);
            if (dataPattern.IsMatch(name)) continue;
            try
            {
                var entries = extractor.List(name, salts);
                if (entries.Any(e => string.Equals(e, "data/db/Skill/SkillInfo.xml", StringComparison.OrdinalIgnoreCase)))
                    skillXml.Add(name);
                if (entries.Any(e => string.Equals(e, "meta/skill-edits.json", StringComparison.OrdinalIgnoreCase)))
                {
                    editsJson.Add(name);
                    editsCandidates.Add((name, System.IO.File.GetLastWriteTime(path)));
                }
            }
            catch (Exception ex) { Log.Warn($"list {name} 失敗: {ex.Message}"); }
        }

        // 衝突：不選 embedded，讓上層警告
        if (skillXml.Count > 1 || editsJson.Count > 1)
        {
            Log.Warn($"偵測到 .it 衝突: SkillInfo.xml in {skillXml.Count} 檔, skill-edits.json in {editsJson.Count} 檔");
            return new ScanResult(null, skillXml, editsJson);
        }

        if (editsCandidates.Count == 0) return emptyResult;

        var winner = editsCandidates.First();
        Log.Info($"找到內附修改記錄: {winner.Name}");
        try
        {
            var (diffPath, _) = extractor.Extract(winner.Name, "meta/skill-edits.json", salts);
            return new ScanResult((winner.Name, diffPath), skillXml, editsJson);
        }
        catch (Exception ex)
        {
            Log.Warn($"extract diff from {winner.Name} 失敗: {ex.Message}");
            return emptyResult;
        }
    }

    private static string BuildConflictWarning(ScanResult scan)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("偵測到技能修改 .it 衝突，未自動套用內附修改。");
        sb.AppendLine();
        if (scan.SkillXmlMods.Count > 1)
        {
            sb.AppendLine("以下 .it 都包含 data/db/Skill/SkillInfo.xml：");
            foreach (var f in scan.SkillXmlMods) sb.AppendLine($"  - {f}");
            sb.AppendLine();
        }
        if (scan.EditsJsonMods.Count > 1)
        {
            sb.AppendLine("以下 .it 都包含 meta/skill-edits.json：");
            foreach (var f in scan.EditsJsonMods) sb.AppendLine($"  - {f}");
            sb.AppendLine();
        }
        sb.Append("請手動清理 package 資料夾中多餘的 mod 檔。");
        return sb.ToString();
    }

    /// <summary>"skill_mod_0.it" → "skill_mod"；"foo_00001.it" → "foo"；其他 → 去副檔名</summary>
    private static string StripPackSuffix(string itFileName)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(itFileName);
        var m = System.Text.RegularExpressions.Regex.Match(name, @"^(.+)_\d+$");
        return m.Success ? m.Groups[1].Value : name;
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
        var diffOut    = Path.Combine(outDir,   "skill-edits.json");
        var diffInPack = Path.Combine(stageDir, "meta", "skill-edits.json");
        var itOut      = Path.Combine(outDir,   $"{itName}_0.it");

        // 在呼叫前快照，避免在背景執行緒存取 UI 集合
        var edits       = Session.AllEdits.ToList();
        var originPath  = _originXmlPath;
        var gameFolder  = GameFolder;
        var gameVersion = GameVersion;
        var ver         = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var appVersion  = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "";
        List<SkillXmlParser.DiffEntry> diffs = new();

        await Task.Run(() =>
        {
            progress.Report("正在寫出 SkillInfo.xml...");
            diffs = _parser.Save(originPath, xmlOut, edits);

            progress.Report("正在寫出 skill-edits.json...");
            Session.WriteDiff(diffOut, diffs, appVersion, gameVersion);
            // 同份內容塞進 stage/meta/，會被打包進 .it
            Directory.CreateDirectory(Path.GetDirectoryName(diffInPack)!);
            File.Copy(diffOut, diffInPack, overwrite: true);

            progress.Report("正在打包 .it...");
            var extractor = new PackExtractService(gameFolder, ConfigService.OriginDir);
            // 打包 stageDir（含 data/ 與 meta/，不含 IT 本身），IT 輸出到 stageDir 外
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
        RefreshPresetStates();
    }

    // ── 快速範本 ─────────────────────────────────────

    private void LoadPresets()
    {
        Presets.Clear();
        var cfg = PresetService.Load();
        foreach (var p in cfg.Presets)
            Presets.Add(new PresetViewModel(p));
    }

    public void ApplyPreset(PresetViewModel pvm)
    {
        if (!IsLoaded) return;
        var skill = _allSkills.FirstOrDefault(s => s.SkillID == pvm.SkillID);
        if (skill == null) return;

        var edit = Session.GetOrCreate(skill);
        if (pvm.Preset.WeaponStringID != null)
            edit.WeaponStringID = pvm.Preset.WeaponStringID;
        if (pvm.Preset.TargetPreference != null)
            edit.TargetPreference = pvm.Preset.TargetPreference;
        Session.Commit(edit);

        RefreshModifiedList();
        Status = $"已套用範本：{pvm.Name}";
        Log.Info($"ApplyPreset {pvm.Name} -> SkillID={pvm.SkillID}");
    }

    private void RefreshPresetStates()
    {
        var loaded = IsLoaded;
        foreach (var pvm in Presets)
        {
            if (!loaded)
            {
                pvm.SkillName = "(未載入)";
                pvm.Status    = PresetStatus.NotLoaded;
                continue;
            }
            var skill = _allSkills.FirstOrDefault(s => s.SkillID == pvm.SkillID);
            if (skill == null)
            {
                pvm.SkillName = "(找不到)";
                pvm.Status    = PresetStatus.SkillMissing;
                continue;
            }
            pvm.SkillName = skill.DisplayName;

            var edit = Session.TryGet(pvm.SkillID);
            if (edit == null) { pvm.Status = PresetStatus.NotApplied; continue; }

            var match =
                (pvm.Preset.WeaponStringID   == null || edit.WeaponStringID   == pvm.Preset.WeaponStringID) &&
                (pvm.Preset.TargetPreference == null || edit.TargetPreference == pvm.Preset.TargetPreference);
            pvm.Status = match ? PresetStatus.Applied : PresetStatus.NotApplied;
        }
    }

    // ── 篩選 ─────────────────────────────────────────

    // ── Diff 載入 ────────────────────────────────────

    public void LoadDiff(string diffPath)
    {
        if (!IsLoaded) throw new InvalidOperationException("請先載入技能資料");

        var changes = DiffImportService.Parse(diffPath);
        var applied = ApplyChanges(changes);
        Status = $"已載入修改記錄，套用 {applied} 個技能變更";
        OnPropertyChanged(nameof(IsLoaded));
    }

    /// <summary>套用 SkillChange list 到 Session，回傳實際成功套用的技能數</summary>
    private int ApplyChanges(IEnumerable<DiffImportService.SkillChange> changes)
    {
        var lookup  = _allSkills.ToDictionary(s => s.SkillID);
        int applied = 0;

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
        return applied;
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
