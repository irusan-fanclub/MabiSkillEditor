# MabiSkillEditor

瑪奇（Mabinogi）技能設定編輯器，用於修改 `SkillInfo.xml` 中的技能武器限制與目標設定。

## 環境需求

- Windows 10/11 64-bit
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## 安裝

將以下檔案放在同一個資料夾中：

```
MabiSkillEditor.exe
mabi-pack2.exe
sources.json
wpfgfx_cor3.dll
PresentationNative_cor3.dll
D3DCompiler_47_cor3.dll
PenImc_cor3.dll
vcruntime140_cor3.dll
```

## 使用方式

### 1. 設定遊戲資料夾

啟動後程式會自動從 Registry 偵測瑪奇安裝路徑。
若偵測失敗，點選「瀏覽」手動選擇遊戲資料夾（即 `Mabinogi` 根目錄）。

### 2. 載入技能

點選「**載入**」，程式會自動：
- 從 `package/` 解包 `SkillInfo.xml` 及本地化檔案
- 解析所有玩家可用技能（自動排除怪物技能）

### 3. 編輯技能

在左側清單選取一個技能：

- **右側上方**：顯示原始資料（唯讀）
- **右側下方**：編輯區

可修改的欄位：

| 欄位 | 說明 |
|---|---|
| 武器限制 | 選擇持握方式（單手 / 雙手）與武器種類，支援多選 |
| 自訂武器字串 | 手動輸入不在清單中的 token，格式如 `/token/` |
| 目標設定 | 技能的 `TargetPreference` 字串 |

編輯完成後點「**套用**」，技能會出現在下方「已修改」清單。
若要取消修改，選取技能後點「**還原**」。

### 4. 輸出

在頂部輸入輸出檔名（預設 `skill_mod`），點選「**輸出**」：

- 產生 `output/skill_mod_0.it`：可直接放入瑪奇 `package/` 資料夾
- 產生 `output/diff.txt`：記錄本次所有修改內容

### 5. 載入修改記錄

點選「**載入修改記錄**」可匯入先前產生的 `diff.txt`，恢復上次的編輯狀態。

## 技能清單欄位說明

| 欄位 | 說明 |
|---|---|
| ID | 技能編號 |
| 名稱 | 繁中名稱（來自本地化檔） |
| 英文名 | 內部英文識別名 |
| 行號 | 在 SkillInfo.xml 中的行號 |

## 注意事項

- 輸出的 `.it` 檔案請放入瑪奇 `package/` 資料夾，遊戲會優先讀取。
- 不要修改 `IsHidden` 以外未開放的欄位，可能導致遊戲異常。
- `sources.json` 記錄解包來源設定，一般不需修改。
