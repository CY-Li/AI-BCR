# Plustek AI-BCR 功能規格書（現況版）

- 文件版本：2.0 (As-Is)
- 更新日期：2026-05-25
- 專案型態：WinUI 3 桌面應用（Unpackaged）
- 目的：描述「目前程式碼已實作到哪裡」，不是理想規劃稿

## 1. 專案定位

本專案目前是 **高擬真 UI + 可互動流程原型**，核心目標是展示 AI 名片管理流程（掃描、匯入、辨識中、編輯、刪除、備註），尚未接上正式後端與持久化資料層。

## 2. 實際技術現況

- Runtime：`.NET 8`（TFM: `net8.0-windows10.0.19041.0`）
- UI：WinUI 3（`Microsoft.WindowsAppSDK 1.*`）
- 架構：MVVM + `CommunityToolkit.Mvvm`
- DI：`Microsoft.Extensions.Hosting` + `ServiceCollection`
- 檔案匯入：`MiniExcel`
- 視窗管理：`WinUIEx`
- 套件已引用但目前未形成主流程：
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Microsoft.Web.WebView2`

## 3. 模組完成度（As-Is）

### 3.1 已完成（可操作）

1. 主畫面框架與導覽
- `MainWindow` 完成上方工具列、左側 NavigationView、主內容區 Frame 導頁。
- 支援 Dashboard(EmptyPage) 與 AllCardsPage 兩主頁切換。

2. AI Toggle 與動畫
- AI ON/OFF 切換完成。
- 包含 icon/text 過場與縮放/旋轉動畫。

3. 掃描流程（Mock）
- Scan 會先顯示掃描確認對話框。
- AI OFF 會先顯示警示對話框，可選擇啟用 AI。
- 確認後建立一筆 `Recognizing` 假資料並送入列表。

4. 匯入流程（CSV/XLSX + 圖片）
- 支援拖放與檔案選擇。
- 支援 `.csv/.xlsx` 讀取欄位、映射到 `BusinessCard`。
- 支援多張圖片匯入為待辨識卡片。

5. 卡片列表與側欄詳情（AllCardsPage）
- Grid/List 兩種呈現。
- 卡片依日期分組。
- 右側抽屜可編輯主要欄位、顯示 front/back 圖。
- 抽屜可拖曳調寬（280~1200）。

6. 卡片詳細頁（CardDetailPage）
- 支援編輯基本資料。
- 支援 front/back 圖上傳、拖放、刪除。
- 支援 notes timeline 顯示與新增。
- 支援刪卡、返回動畫（Connected Animation）。

7. Mock OCR 佇列
- 透過 `CardsImportedMessage` 收到新卡後背景處理。
- 以 semaphore 控制並行度（3）。
- 3 秒後填入隨機姓名/公司/職稱等資料，狀態改 `Done`。

### 3.2 部分完成（有 UI/流程但未封口）

1. 搜尋
- 有 recent search 與 advanced search UI。
- 尚未真正對資料做篩選查詢。

2. 設定/Help
- Header 上有按鈕與視覺，但未串接完整功能頁。

3. 匯入欄位映射
- 有自動比對邏輯，但規則混雜且含亂碼字串，可靠度不足。

### 3.3 尚未完成

1. 正式 OCR/AI 引擎串接（目前全 mock）
2. 掃描器硬體整合（ADF/TWAIN/WIA 等）
3. SQLite/EF Core 持久化（目前為記憶體資料）
4. 匯出（CSV/vCard/Excel）完整流程
5. 自動化測試（單元測試/UI 測試）

## 4. 資料生命週期

- 資料來源：Sample Data + 使用者匯入 + Scan mock
- 記憶體持有：`AllCardsViewModel.AllCards`
- 模組溝通：`WeakReferenceMessenger` 傳遞 `CardsImportedMessage`
- 關閉程式後：目前不保留（無 DB/檔案持久化）

## 5. 建置與執行現況

- `dotnet build` 在本機檢查時失敗，原因為輸出檔被執行中的程式鎖定：
  - `bin\Debug\...\PlustekBCR.exe` 被 `PlustekBCR (PID 20236)` 佔用
- 此問題屬「執行中重建置衝突」，非編譯錯誤。

## 6. 主要技術風險與負債

1. 文件與實作落差
- 舊版 FSD 內容偏規劃，與現況不一致，已改為 as-is。

2. 字串/編碼一致性問題
- 多處可見亂碼字串，風險包含 UI 文案錯誤與欄位映射判斷失準。

3. ViewModel 職責偏重
- `AllCardsViewModel` 同時負責資料、流程、mock OCR，後續可拆 service。

4. 無持久化/無測試
- 現階段適合 Demo，不適合正式上線。

## 7. 建議下一階段（優先序）

1. 先落地資料層
- 建立 EF Core DbContext、Migration、Repository。
- 將 `AllCards` 改成可讀寫 DB。

2. 抽離服務層
- `IOcrService`, `IImportService`, `IScanService`。
- ViewModel 只保留狀態管理。

3. 補齊可驗證流程
- 搜尋/進階搜尋真正套用到清單。
- 補齊設定頁與匯出流程。

4. 建立最小測試防線
- 至少針對匯入映射、OCR 狀態機、刪卡流程加單元測試。
