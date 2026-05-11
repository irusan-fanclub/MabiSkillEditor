# 武器 Token Picker — Design

**Date**: 2026-05-11
**Status**: Approved, pending implementation

## 目標

編輯 WeaponStringID 時，使用者按按鈕跳 modal dialog，輸入中文/英文名搜尋具體武器、檢視該武器路徑上所有 token、勾選後附加到「自訂條件」textbox。同時可預覽「此 token 在哪些武器上」（前 10 把）。

省去使用者手打 `/Brionac/`、`/healing_wand/` 這類非標準 19 類型 token 的麻煩。

## 非目標（YAGNI）

- 反向搜尋（給 token 查武器）獨立功能，目前僅在 picker 內附帶
- 多選武器同時看交集 token
- 收藏 / 我的最愛 武器
- Token 中文翻譯映射（除既有 19 個類型）—— 顯示原文
- Picker 內 dedupe 已選 token

## 資料載入

### 主 Load 多解 2 個檔

| 來源 IT | 內部路徑 | 用途 |
|---|---|---|
| `data_00633.it` | `data/db/itemDB_Weapon.xml` | 1,182 把武器（Category 路徑、Text_Name0/1） |
| `data_00756.it` | `data/local/xml/itemdb_weapon.taiwan.txt` | 中文本地化 |

`data_00756.it` 在現有流程中已會解開（為了 SkillInfo localization），多解一個 inner path；`data_00633.it` 為新增。

選擇「主 Load 時一起解」（不 lazy），原因：
- 簡化邏輯（無需 cache 失效偵測）
- 多 ~3–5 秒可接受
- 武器資料一旦解開就駐留記憶體，後續 picker 開啟即時

### 資料模型

```csharp
public class Weapon
{
    public int    ID      { get; set; }
    public string Name    { get; set; }  // 中文（_LT 解出）；無則用 EngName
    public string EngName { get; set; }  // Text_Name0
    public List<string> Tokens { get; set; }  // 從 Category 拆出，無前後 slash
}
```

`Category` 例：`/equip/righthand/weapon/edged/steel/blade/sword06/Flute_Short_Sword/...`
→ `Tokens = ["equip", "righthand", "weapon", "edged", "steel", "blade", "sword06", "Flute_Short_Sword", ...]`

### Inverted Index

Parse 完武器資料後額外建一個 `Dictionary<string, List<Weapon>>`：
- Key: token（不含 slash）
- Value: 包含此 token 的所有武器（按 ID 升序）

成本：1,182 × 13 平均 = ~15k 迭代，<10ms；記憶體 ~150KB（references + key strings）。

## UI

### 入口
編輯欄「自訂條件」label 旁加一個 SecondBtn 樣式按鈕「**+ 從武器找 token**」。

點開 → modal dialog（`WeaponPickerWindow`）。

### Modal Layout (~900 × 500)

```
┌─ 從武器找 token ────────────────────────────────────────────┐
│ 類型: [全部 ▾]    搜尋: [_____________________]              │
├──────────────────┬───────────────────┬──────────────────────┤
│ 武器 (filtered)   │ 該武器的 token     │ 含此 token 的武器     │
│                  │                   │ (前 10)              │
│ 布里歐納克 #45001 │ ☑ /righthand/    │ 點 token 看           │
│ 治癒杖    #30006 │ ☑ /Brionac/      │                      │
│ ...              │ ☐ /blade/        │                      │
│                  │ ☐ /weapon/       │                      │
│                  │ ☐ /not_enchant.../│                     │
└──────────────────┴───────────────────┴──────────────────────┘
                                          [取消]  [加入]
```

**左欄 — 武器列表**：
- 由「類型 dropdown + 搜尋 textbox」即時 filter
- 顯示：中文名（首要）+ ID（次要）+ 英文（次要）
- 單選

**中欄 — Tokens**：
- 顯示左欄選中武器的所有 token
- 每行：`[checkbox] /token/`
- Row click 觸發右欄更新；checkbox click 切換勾選狀態（兩者分離）

**右欄 — 含此 token 的武器**：
- 顯示中欄選中 token（最後點的那個）對應的武器，最多 10 筆
- 按武器 ID 升序
- 底部：`(共 N 把)`
- 初始狀態顯示提示「點左側 token 看武器」

**底部**：
- 「取消」：關閉，不修改 自訂條件
- 「加入」：勾選的 token 用 ` | ` join，append 到 自訂條件 textbox 後，關閉

### 搜尋邏輯

- substring match `Name`（中文）OR `EngName`，case-insensitive
- 類型 filter：dropdown 值非「全部」時，要求 `Tokens` 含該類型 token
- 兩條件 AND
- 空搜尋 + 「全部」= 顯示所有 1,182 把（要捲）

### 與 WeaponStringID 整合

確認 picker 「加入」後：
- 取所有勾選 token，轉成 `/token/` 格式
- 用 ` | ` join 後 append 到 `EWeaponCustom.Text`
- 若 `EWeaponCustom.Text` 非空，先加 ` | ` 連接
- 觸發既有 `TextChanged` → 自動更新 `EWeaponPreview`
- **不 dedupe**：使用者自己處理重複

載入既有 WeaponStringID 時：`ParseWeaponStringToUi` 本來就把未知 token 放進 自訂條件 textbox，與 picker 路徑一致，無需新增 round-trip 邏輯。

## 邊緣情況

| 情況 | 行為 |
|---|---|
| 武器解包失敗（兩個 .it 任一） | 主畫面「+ 從武器找 token」按鈕 disabled，hover tip「武器資料未載入」；其餘功能正常 |
| 武器無中文名 | 用 EngName 顯示，並在搜尋 fallback |
| Token 無中文映射 | 直接顯示原文 token |
| 含某 token 的武器 > 10 把 | 取前 10 + `(共 N 把)` 標示 |
| Picker 關閉時 textbox 已被使用者編輯 | append 仍從末尾加，使用者的編輯保留 |

## 變更檔案

### 新增
- `Core/Models/Weapon.cs` — DTO
- `Core/Services/WeaponDataService.cs` — 解析 itemDB_Weapon.xml + 對應本地化、建 inverted index
- `UI/Windows/WeaponPickerWindow.xaml` + `.xaml.cs` — modal dialog

### 修改
- `Core/Models/SourcesConfig.cs` — 加 `WeaponIt`（= `"data_00633.it"`）、`WeaponInnerPath`（= `"data/db/itemDB_Weapon.xml"`）、`WeaponLocalizationInnerPath`（= `"data/local/xml/itemdb_weapon.taiwan.txt"`）
- `sources.json` — 加上述欄位
- `UI/ViewModels/MainViewModel.cs` — `LoadAsync` 多解兩個檔；公開 `Weapons : IReadOnlyList<Weapon>` 與 `WeaponsByToken : IReadOnlyDictionary<string, IReadOnlyList<Weapon>>`；`IsWeaponDataLoaded : bool` 給 UI 用 disabled
- `MainWindow.xaml` — 加 picker 按鈕、binding `IsEnabled` 到 `IsWeaponDataLoaded`
- `MainWindow.xaml.cs` — 開 picker handler、確認後 append 結果至 `EWeaponCustom.Text`

## 效能評估

| 項 | 預估 |
|---|---|
| Parse itemDB_Weapon.xml | UTF-16 ~100KB / 1,182 entries，~50–100ms |
| 解 2 個額外 inner path | 各 ~1–2 秒（mabi-pack2 子程序成本） |
| 建 inverted index | <10ms |
| 搜尋 filter（每次 keystroke） | O(N) linear scan，N=1,182，~1ms |
| 點 token 查武器列表 | O(1) hash + 取 10，<1ms |
| 記憶體 | 武器 1,182 物件 + index 共 ~250KB |

主 Load 從現行 ~1–2 秒 → 預期 ~3–5 秒，多 ~2–3 秒。可接受。

## 開放問題
無。
