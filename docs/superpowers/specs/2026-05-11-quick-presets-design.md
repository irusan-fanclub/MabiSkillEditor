# 快速範本（Quick Presets）— Design

**Date**: 2026-05-11
**Status**: Approved, pending implementation
**Author**: brainstormed with user

## 目標

讓使用者一鍵把預先定義好的「特定技能 × 特定修改」套用上去，省去手動瀏覽技能清單、找到對的技能、改欄位值的步驟。套用後的修改與手動編輯走相同路徑（`Session` + `ModifiedSkills`），可在主編輯分頁檢視、還原、輸出。

## 非目標（YAGNI）

- 使用者自建範本（只有內建）
- 範本分類 / 搜尋 / 排序
- 一個範本套用到多個技能
- 一次套用多個範本（批次模式）
- 進階分類欄位（`SkillType` / `TriggerType` / `SkillCategory`）— 那組目前在 codebase 是注解狀態

## 資料模型

每個範本固定針對單一技能、只能改現有編輯表單的兩個欄位：

| 欄位 | 型別 | 語義 |
|---|---|---|
| `Name` | string | UI 顯示的範本標題 |
| `SkillID` | int | 目標技能（必填） |
| `WeaponStringID` | string? | null=不改；空字串=改成空（無限制）；其他=改成該值 |
| `TargetPreference` | string? | 同上 |

合法性：至少要設 `WeaponStringID` 或 `TargetPreference` 其中一個（兩個都 null 是無意義的範本）。

存放：`AppDir/presets.json`，與 `sources.json` 並列。schema：

```json
{
  "Presets": [
    {
      "Name": "冰凍術允許任何武器",
      "SkillID": 271,
      "WeaponStringID": ""
    }
  ]
}
```

啟動時讀一次。若檔案不存在，產生包含 1 個範例範本的預設檔（讓使用者照樣編輯）。

## UI

### Tab 結構

主畫面外層包一個 `TabControl`：
- **「技能編輯」** — 目前的三欄主畫面整個塞進去，不動內部結構
- **「快速範本」** — 新分頁

Tab 視覺風格延續現有 Catppuccin 配色。

### 範本分頁布局

垂直 ScrollViewer，每個範本一張卡片：

```
┌────────────────────────────┐
│ 冰凍術允許任何武器           │  [ 套用 ]
│ [271] 冰凍術                 │
│ → WeaponStringID = (清空)    │
└────────────────────────────┘
```

卡片內容：
- **標題**：`preset.Name`
- **副標**：`[SkillID] 技能本地化名稱` — 從已載入的 `_allSkills` lookup 拿；未載入時顯示「未載入」
- **變更摘要**：每個非 null 欄位一行 `→ 欄位名 = 值`（空字串顯示為 `(清空)`）
- **按鈕**：右側，三種狀態：
  - `套用`（預設藍）— 可點
  - `已套用 ✓`（灰）— disabled
  - `⚠ 找不到此技能`（紅）— disabled，當 SkillID 在 SkillInfo.xml 不存在

### 載入前狀態

如果技能資料尚未載入，範本分頁仍可瀏覽（看到範本清單）；所有「套用」按鈕 disabled，分頁頂端顯示提示：「請先在『技能編輯』分頁按『載入』。」

## 行為

### 套用流程

點「套用」：
1. `_allSkills` lookup `preset.SkillID` → 拿到 `SkillEntry`
2. `Session.GetOrCreate(skill)` 取得 / 新建 `SkillEdit`
3. 對 preset 的非 null 欄位寫進 SkillEdit 對應欄位
4. `Session.Commit(edit)`
5. `RefreshModifiedList()` — 「已修改」清單立刻更新
6. 卡片狀態重新計算 → 變「已套用 ✓」

### 已套用狀態判斷

對每個範本卡片：
- 在 `Session.AllEdits` 找出 `SkillID == preset.SkillID` 的 `SkillEdit`
- 如果不存在 → `未套用`
- 如果存在，比對 preset 的每個非 null 欄位是否等於 edit 對應值：
  - 全相等 → `已套用 ✓`
  - 否則 → `未套用`（使用者可能已自己改成別的值）

### Idempotency

重複按套用 OK：第二次寫入相同值，`Session` 不會在清單重複。

### 已修改後的反向同步

如果使用者在「技能編輯」分頁把該技能的修改 revert 掉，範本卡片應自動回到「未套用」狀態。

實作方式：訂閱 `ModifiedSkills.CollectionChanged`（以及 `SkillEdit` 屬性變動），觸發 `RefreshPresetStates()` 重新計算每張卡片的狀態。Tab 切換時不額外刷新（沒必要，因為已經訂閱了變動）。

### 邊緣情況

| 情況 | 行為 |
|---|---|
| presets.json 解析失敗 | log error，跳警示對話框，範本清單為空 |
| presets.json 不存在 | 產生預設檔（1 個範例），照常載入 |
| 範本欄位都是 null | 載入時略過該範本並 log warn |
| SkillID 在 XML 找不到 | 卡片顯示「⚠ 找不到此技能 (ID=X)」，按鈕 disabled |
| 技能未載入 | 卡片副標顯示「未載入」，按鈕 disabled + 分頁頂端提示 |

## 變更檔案

### 新增
- `Core/Models/SkillPreset.cs` — DTO（與 JSON schema 對應）
- `Core/Models/PresetsConfig.cs` — `List<SkillPreset> Presets` 容器
- `Core/Services/PresetService.cs` — `LoadPresets()` / `SavePresets()` / 預設檔產生
- `UI/ViewModels/PresetViewModel.cs` — 每張卡片 VM：暴露 Name、SkillIDLabel、Summary、Status、ApplyCommand
- `presets.json` — 出貨包含 1 個範例（透過 csproj `Content Include` 複製到 output）

### 修改
- `MainWindow.xaml` — 三欄主體外面包 TabControl，加第二個 Tab
- `MainWindow.xaml.cs` — Tab 切換時觸發範本狀態 refresh
- `UI/ViewModels/MainViewModel.cs` — 加 `Presets : ObservableCollection<PresetViewModel>`、`LoadPresets()`、`ApplyPreset(p)`、`RefreshPresetStates()`；`LoadAsync` 完成時呼叫一次 refresh；`ModifiedSkills` 變動時呼叫 refresh
- `Core/Services/ConfigService.cs` — `PresetsPath` 屬性
- `MabiSkillEditor.csproj` — `<Content Include="presets.json" CopyToOutputDirectory="PreserveNewest"/>`

## 測試
- 手動：建 presets.json、載入遊戲、切到範本分頁、點套用、驗證「已修改」清單、輸出 .it 檔案、確認 diff.txt 內容
- 自動化測試目前不在範圍（codebase 沒有測試框架）

## 開放問題

無。
