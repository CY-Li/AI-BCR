# Change Log

## 2026-08-11 — 獨立圖片 Viewer

### 操作與介面

- All Cards 右側預覽與 Card Detail 詳細頁統一為直接點擊正面／背面圖片開啟 Viewer，不另外顯示放大按鈕。
- 新增可移動、可調整大小並可放置到第二螢幕的獨立工具視窗；同時間只保留一個 Viewer。
- 支援正反面切換、25%～400% 縮放、25% 步進、100%、適合視窗、置頂切換及 `Esc` 關閉。
- 滑鼠滾輪可直接以游標位置為中心縮放；圖片大於可視範圍時可拖曳平移。
- 工具列移除額外 Close 按鈕，保留 Windows 標題列關閉按鈕；圖示按鈕使用零 padding，避免小尺寸下裁切。

### 顯示與生命週期

- Viewer 使用 Canvas 保存完整縮放圖片，再由最外層 Viewport 裁切目前不可見區域，避免 Grid／RenderTransform 在平移前永久裁掉圖片內容。
- 適合視窗時顯示完整圖片；放大後可拖曳到所有邊緣，不會只移動已裁切的中央區域。
- Viewer 固定顯示開啟時的名片；再次從其他名片開啟時更新同一視窗。
- 圖片重新掃描、上傳或刪除時同步更新；目前面不存在時切到另一面，兩面皆空、刪除名片或關閉主程式時關閉 Viewer。
- 置頂選擇在本次程式執行期間保留，視窗關閉時解除名片與語言事件訂閱。

### 架構與驗證

- 新增 `IImageViewerService`／`ImageViewerService`、`CardImageSide`、`ImageViewerState` 與 `ImageViewerWindow`，由 DI singleton 管理視窗生命週期。
- UI 專屬的視窗、指標與 Canvas 定位保留在 WinUI code-behind；縮放範圍、面別 fallback 與置頂狀態保持 UI 無關，方便後續替換成 WPF Window。
- 未修改 OCR JSON schema、名片持久化格式、掃描流程或外部 API。
- 最終驗證：`dotnet test` 19/19 通過；Release `dotnet build` 0 警告、0 錯誤。

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
- 新增集合級 `RebuildReviewStates`，依 `AllCards` 順序只與較早且已完成 OCR 的名片比對；刪除、Replace、OCR 完成、欄位編輯與設定變更後會自動重建。
- 重建會清除失效候選參照並保留 Accepted；`Pending`／`Recognizing` 名片在完成前不參與比對。

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
- 編輯目前選定名片的已啟用比對欄位時會重建整個集合；已執行 Keep 的名片若再修改相關欄位，會解除 Accepted 並重新判定。
- OCR 失敗仍維持原本手動流程，重複判定不會中止掃描或建立 Modal。
- `DuplicateReviewState` 與既有 OCR `ProcessingStatus` 分離，避免審核狀態污染辨識流程。

### 待確認行為

- 候選名片顯示 `Possible duplicate` 徽章，工作區顯示待確認數量。
- 精簡橫幅顯示命中欄位；單筆顯示既有名片名稱，多筆顯示既有名片數量，不再提供目標選擇器。
- 橫幅與 Replace 目標改為即時查詢全部已完成 OCR 的名片；新增第三張重複名片後，第二張的摘要也會同步反映完整相符數量，同時不改變內部單向審核關係。
- `Replace`：按鈕固定顯示 `Replace`；確認視窗會顯示實際刪除筆數並以 Cancel 為預設焦點。確認前重新比對整個集合，刪除所有與目前候選相符的其他名片，只保留目前候選及其原始 ID、圖片與內容，之後重建其餘候選關係。
- `Keep`：保留目前候選與全部既有名片，候選設為 `Accepted` 並一次清除它的全部提示。
- 已移除 `Keep existing`／`Keep both` 與選定覆蓋目標；Replace 執行前會顯示不可復原提示，且不要求逐筆選擇目標。

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
- 設定變更會在目前記憶體集合執行 O(n²) 順序重建；接入大量持久化資料後，應由後端查詢、索引或背景工作取代全量記憶體掃描。
- Replace 目前會刪除全部命中的既有記憶體物件並保留新候選 ID。接入資料庫後必須以 transaction／concurrency token 保護，並處理外部 ERP 關聯與刪除審計。
- `BusinessCard.DuplicateMatches` 是依集合順序建立的單向審核關係，不能直接當成 Replace 的完整刪除集合；側欄與 Replace 必須以目前候選重新查詢全部已完成 OCR 的資料。後端實作也應分開「穩定審核狀態」與「即時完整相符集合」。
- Accepted 目前是一次 UI session 的抑制狀態；若後端需要跨裝置或跨 session 保留，應新增持久化審核紀錄，而不是重用 OCR status。
- 支援欄位 key 定義於 `DuplicateComparisonSettings.SupportedFieldKeys`，並由 `BusinessCardDuplicateService` 映射到 `BusinessCard` 屬性。若 API schema 改名，必須維持 key 相容或提供版本轉換。
- OCR 完成透過 CommunityToolkit Messenger 傳遞；未來若改為後端 job/event，需維持「資料完整後才比對」與「失敗不阻塞掃描」兩項不變條件。

### 驗證

- `BusinessCardDuplicateServiceTests` 共 15 項：除原有正規化與條件測試外，新增刪除第一／中間名片後重建、Accepted 基準、設定擴大與未完成 OCR 排除。
- 最終驗證：`dotnet test` 15/15 通過；`dotnet build` 0 警告、0 錯誤。
