# Change Log

## 2026-08-11 — 重複名片比對、待確認流程與 Settings UI

本次變更以 WinUI 3 原型驗證工作流程；新增核心邏輯維持 UI 無關，後續仍應依專案政策遷移至 WPF／MVVM。未修改 OCR JSON schema、OCR endpoint、掃描器協定或外部 API。

### 新增：重複比對核心

- 新增 `DuplicateComparisonSettings`、`DuplicateMatchOperator`、`DuplicateReviewState`、`DuplicateMatchResult` 與辨識完成訊息模型。
- 新增 `IBusinessCardDuplicateService.FindMatches(candidate, existingCards, settings)` 與 `IsSupportedField(fieldKey)`。
- 預設規則為 Email／OR；無效或空欄位設定安全回退至 Email。
- OR 為任一有效欄位命中；AND 要求所有選定欄位在雙方都有值且完全命中。
- 正規化規則：
  - 所有文字：Unicode FormKC、去除首尾空白、合併連續空白、不分大小寫。
  - `tel`、`extension`、`fax`、`mobile`：再移除空白、`-`、`(`、`)`。
  - 空值永不命中；排除相同名片 ID。
- 一次比對可回傳多筆既有候選，且每筆保留實際命中的欄位 key。

### 設定契約

`IApplicationSettingsService` 新增：

```csharp
DuplicateComparisonSettings DuplicateComparison { get; }
event Action<DuplicateComparisonSettings>? DuplicateComparisonChanged;
Task SetDuplicateComparisonAsync(DuplicateComparisonSettings settings);
```

設定沿用既有 `appsettings.json`，未新增資料庫 migration：

```json
{
  "DuplicateDetection": {
    "MatchOperator": "Or",
    "Fields": ["email"]
  }
}
```

- 寫入前會過濾不支援欄位、轉為小寫並去除重複值。
- 至少保留一個欄位；無有效欄位、節點缺少或 JSON 讀取失敗時回退 Email／OR。
- 公開 getter 與事件回傳 clone，避免呼叫端直接修改服務內部狀態。
- 設定保存成功後才更新記憶體狀態並送出 `DuplicateComparisonChanged`。

### 流程整合

- CSV／XLSX 等已完成欄位映射的資料：加入 `AllCards` 後立即比對。
- 圖片／掃描資料：先加入辨識佇列，OCR 成功並收到 `BusinessCardRecognitionCompletedMessage` 後比對。
- 批次匯入逐筆加入集合後比對，因此後面的資料可以命中同批較早加入的資料。
- 編輯目前選定名片的已啟用比對欄位時會重新檢查；已執行 Keep both 的名片若再修改相關欄位，會解除 Accepted 並重新判定。
- OCR 失敗仍維持原本手動流程，重複判定不會中止掃描或建立 Modal。
- `DuplicateReviewState` 與既有 OCR `ProcessingStatus` 分離，避免審核狀態污染辨識流程。

### 待確認行為

- 候選名片顯示 `Possible duplicate` 徽章，工作區顯示待確認數量。
- 精簡橫幅顯示命中的既有名片與欄位；只有多筆候選時顯示目標選擇器。
- `Replace`：將候選所有名片內容（包含空值、圖片與狀態）複製至選定既有名片，只保留既有 ID，再從集合移除候選。
- `Keep both`：保留兩筆，候選設為 `Accepted` 並清除本次候選清單，避免立即再次提示。
- 已移除 `Keep existing`；Replace 不再顯示二次確認視窗。

### Settings 與介面調整

- General Settings 改為滿版工作區，Language and region 與 Duplicate card comparison 使用相同卡片標頭層級。
- 重複規則提供 `Email only`、`Name + Company`、`Contact methods`、`Custom`；預設方案分別對應完整欄位集合與 OR／AND。
- 欄位依身分／公司、聯絡方式、地址、進階欄位分組；進階欄位預設收合。
- 手動修改欄位或條件後顯示 Custom；取消最後一個欄位會立即恢復並提示。
- 保存狀態包含 Saving／Saved／Save failed；保存失敗時重新載入上一份有效設定。
- Quick rules 使用 4／2／1 欄等寬響應式排列；選取狀態改為淺藍底、藍框、深藍文字與勾選圖示。
- 新增 Light、Dark、High Contrast 對應資源及英／日文文案。
- Header 的 AI ON／OFF 圖示改為掃描框＋AI 星芒向量，保留原有切換動畫並校正至右側 20px 圖示的視覺尺寸。

### 新增或擴充的主要檔案

- `Models/DuplicateDetectionModels.cs`
- `Services/IBusinessCardDuplicateService.cs`
- `Services/BusinessCardDuplicateService.cs`
- `ViewModels/DuplicateSettingsViewModel.cs`
- `Controls/WrapPanel.cs`：新增可選的等寬 4／2／1 欄排列能力，預設行為保持相容。
- `PlustekBCR.Tests/BusinessCardDuplicateServiceTests.cs`

### 後端接手注意事項

- 現有 `AllCards`、候選清單、`DuplicateReviewState` 都是記憶體資料；關閉應用程式後不保存。
- 「現有資料」只代表目前載入的 `AllCards`，尚未查詢 ERP、資料庫或遠端 API。
- 設定變更目前只重新計算仍為 Pending 的候選；若新規則變寬，原本非候選的既有名片不會被全量重掃。接入持久層時應決定由後端查詢、索引或背景工作負責全量重算。
- Replace 目前是 ViewModel 內的記憶體物件完整複製。接入資料庫後必須以 transaction／concurrency token 保護，並明確定義圖片、審計欄位與外部 ERP ID 的覆蓋規則。
- Accepted 目前是一次 UI session 的抑制狀態；若後端需要跨裝置或跨 session 保留，應新增持久化審核紀錄，而不是重用 OCR status。
- 支援欄位 key 定義於 `DuplicateComparisonSettings.SupportedFieldKeys`，並由 `BusinessCardDuplicateService` 映射到 `BusinessCard` 屬性。若 API schema 改名，必須維持 key 相容或提供版本轉換。
- OCR 完成透過 CommunityToolkit Messenger 傳遞；未來若改為後端 job/event，需維持「資料完整後才比對」與「失敗不阻塞掃描」兩項不變條件。

### 驗證

- 新增 9 項 `BusinessCardDuplicateServiceTests`：Email 大小寫／空白／Unicode、電話格式、空值、OR、AND、自身排除、多候選、一般文字正規化及無效設定回退。
- 最終驗證：`dotnet test` 9/9 通過；`dotnet build` 0 警告、0 錯誤。
