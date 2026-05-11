# MabiSkillEditor — DEVLOG

## v0.2.0 — 2026-05-12

### 新增

- **快速範本分頁**：內建「技能 × 修改」配方，一鍵套用，不必先找技能再改值。範本來源 `presets.json`（AppDir，可自行編輯）。
- **從武器找 tag**：新 modal dialog。可依「類型 + 中文/英文搜尋」過濾武器，點武器看它路徑上所有 tag、勾選加入「自訂條件」。第三欄列出含此 tag 的其他武器（前 15 / 共 N）。武器資料整合 `itemDB_Weapon.xml` + `ItemDB.xml`（共約 1,700 把）。
  - 已 block 不可選的 tag：`equip` / `weapon` / `righthand` / `twohand`（限制無意義或主畫面已有對應）。可自行擴充。
  - 預留 tag 中文說明欄位（`TagDescriptions`），未來可補。
- **mod .it 內附修改記錄**：輸出時 `meta/skill-edits.json` 一起打包進 .it。下次載入會掃描 `package/` 內所有非 `data_*.it`，自動套用內附修改、還原 OutputItName，免「載入修改記錄」一步。
- **titlebar 顯示遊戲版本**：載入完成後從 `version.dat` 讀出顯示，例：`v0.2.0 · Mabinogi v151`。

### 改進

- **diff.txt → diff.json**：結構化格式（含 metadata：app 版本、遊戲版本、輸出時間）。舊 `diff.txt` 仍可匯入。
- **mod .it 衝突偵測**：載入時若多個 .it 都含 `data/db/Skill/SkillInfo.xml` 或 `meta/skill-edits.json` → 跳警告、不自動套用。
- **武器名稱顯示更新**：魔杖→長杖、單手杖→短杖、槍→長槍、斧→斧頭、彈夾→鋼瓶、投槍器→擲矛器、芬貝爾→芬恩鈴鐺。
- **picker titlebar 風格**：與主視窗統一（自訂 caption、Catppuccin 配色）。
- **視窗預設高度** 760 → 800（編輯欄不必捲動）。

### 修復

- **載入卡死**：mabi-pack2 子程序 stdout 緩衝填滿造成 deadlock + 預設會打遠端 30 秒 timeout 抓 `salts.txt`。改為本地寫 `salts.txt` 到 cwd，跳過遠端；async 讀 stdout/stderr。原本 30 秒 × N salt 的卡死變秒退。

### 內部

- 加 `log.txt`（AppDir）— 每次載入/輸出/解包都記錄，定位問題用。
- 程式碼命名 `token` → `tag` 統一（避免歧義）。

---

## v0.1.2 — 2026-04-30

- Salt 自動偵測：內建 30 組 salt，解包成功後把命中的排到首位寫回 `sources.json`。不必再手動填 salt。
- 自訂 titlebar（WindowChrome）+ 應用程式圖示（lucide wand）。
- 隱藏 `AvailableRace ≤ 0` 的怪物/寵物技能（可勾選顯示）。

## v0.1.1 — 2026-04 中

- Parser 修正：相同 SkillID 重複時取「檔案中最後一筆」（之前是取最大 Season，會選錯版本）。
- 「輸出完成」改成可複製路徑、一鍵複製到 `package/` 的 overlay。
- 加 `scripts/build.ps1` / `scripts/release.ps1` 打包腳本。

## v0.1.0 — 初版

- 解包 `data_00640.it` 取 `SkillInfo.xml`、解 `data_00756.it` 取本地化。
- 三欄主畫面：技能清單 / 原始資料 / 編輯表單。
- 編輯 WeaponStringID（持握 + 武器類型 + 自訂條件）、TargetPreference。
- 修改後重新打包成 mod `.it`。
- 輸出 `diff.txt`，可重新匯入套回。
