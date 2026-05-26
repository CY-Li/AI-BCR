# Auto Scan 模式規格文件

- 文件版本：1.0 (To-Be)
- 更新日期：2026-05-25
- 適用專案：Plustek AI-BCR（WinUI 3）
- 文件目的：將現行「按 Scan 即單次掃描」改為「Auto Scan 連續掃描模式」的完整功能規格

## 1. 背景與目標

目前流程為使用者點擊 `Scan` 後立即建立一筆掃描資料。  
目標改為：點擊 `Scan` 進入 `Auto Scan` 模式，維持 AP 與 Scanner 連線，當 Paper Sensor 偵測到紙張時自動觸發掃描；每掃完一張即新增一筆背景辨識資料，直到使用者離開 Auto Scan 模式。

## 2. 範圍定義

### 2.1 In Scope

1. `Scan` 按鈕行為改為模式切換（進入/離開 Auto Scan）。
2. Auto Scan 專用 Overlay Panel（半全屏）與即時狀態呈現。
3. AP/Scanner 長連線生命週期管理（進入模式連線、離開模式斷線）。
4. Paper Sensor 事件觸發掃描流程。
5. 每次掃描完成後建立新卡並送入既有背景辨識佇列。
6. 每次感測觸發時的短促視覺回饋（flash/掃描線動畫）。
7. 例外狀態與重試流程（連線失敗、掃描失敗、感測中斷）。

### 2.2 Out of Scope

1. 真實 OCR 引擎品質調校。
2. SQLite 永久化設計變更。
3. 匯出、搜尋等其他非掃描流程改造。

## 3. 名詞定義

1. `Auto Scan 模式`：持續待命並由感測器觸發掃描的運作模式。
2. `Session`：一次進入 Auto Scan 到離開的完整期間。
3. `Paper Detected`：感測器判定有紙張進入掃描路徑的事件。
4. `Scan Event`：單張掃描觸發到掃描完成的事件單位。

## 4. 使用者流程

1. 使用者點擊 Header 的 `Scan`。
2. 系統切入 Auto Scan 模式，顯示專用 Overlay Panel，開始建立 AP/Scanner 連線。
3. 連線成功後狀態顯示 `Ready / Waiting for paper`。
4. 每次感測到紙張：
   - 顯示 flash/掃描線動畫。
   - 執行單張掃描。
   - 掃描完成後新增一筆卡片（Recognizing/Pending）。
   - 送入既有背景辨識佇列並更新卡片資料。
5. 使用者點擊 `Stop Auto Scan` 或離開頁面。
6. 系統停止監聽、優雅收尾進行中的工作並斷線，回到一般模式。

## 5. UI/UX 規格

### 5.1 Header 行為

1. `Scan` 按鈕切換為模式控制：
   - Idle：`Start Auto Scan`
   - Active：`Stop Auto Scan`
2. Active 時顯示強烈狀態條（例：`AUTO SCAN ACTIVE` + 連線狀態）。
3. Active 時按鈕視覺需高對比，與一般操作區分。

### 5.2 Auto Scan Overlay Panel（半全屏）

1. 顯示時機：`Auto Scan` 進入後立即顯示。
2. 內容區塊：
   - 連線狀態：`Connecting / Ready / Scanning / Error / Stopping`
   - 即時提示：`Waiting for paper`、`Paper detected`、`Processing...`
   - Session 指標：`Scanned`、`Recognizing`、`Failed`
   - 主要操作：`Stop Auto Scan`
3. 遮罩與層級：
   - 不阻斷必要全域操作（如停止）
   - 保持使用者可觀察背景卡片新增

### 5.3 感測觸發動畫（強化回饋）

1. 觸發條件：收到 `Paper Detected` 事件時。
2. 動畫元素：
   - 短促 flash（約 120~220ms）
   - 掃描線由上到下（約 350~600ms）
3. 動畫目的：
   - 明確告知「已觸發掃描」
   - 與背景新增卡片形成同步回饋

## 6. 功能狀態機

### 6.1 狀態列表

1. `Idle`：未啟用 Auto Scan。
2. `Connecting`：建立 AP/Scanner 連線中。
3. `Ready`：已連線，等待紙張。
4. `Scanning`：正在執行單張掃描。
5. `Stopping`：停止流程中（不再接新觸發）。
6. `Error`：錯誤狀態（連線/感測/掃描）。

### 6.2 狀態轉移

1. `Idle -> Connecting -> Ready`
2. `Ready --PaperDetected--> Scanning --ScanDone--> Ready`
3. `Ready/Scanning/Error -> Stopping -> Idle`
4. `Connecting/Ready/Scanning -> Error`
5. `Error --Retry--> Connecting`

## 7. 事件與資料流

1. `StartAutoScanCommand`
   - 初始化 Session
   - 建立連線與感測器監聽
2. `PaperDetected`
   - 執行 Debounce 判斷
   - 合法事件進入單次掃描
3. `ScanCompleted`
   - 產生 `BusinessCard`（`Status=Recognizing` 或 `Pending`）
   - 透過 `CardsImportedMessage` 發送至既有卡片流程
4. `AllCardsViewModel.OnCardsImported`
   - 新卡加入清單
   - 交由既有 OCR mock queue 背景辨識
5. `StopAutoScanCommand`
   - 停止監聽
   - 收尾進行中掃描
   - 斷線並關閉 Session

## 8. 非功能需求

1. 響應性：
   - `Start/Stop` UI 響應需在 200ms 內更新狀態文字。
2. 穩定性：
   - 單張掃描失敗不得中斷整個 Session。
3. 可觀測性：
   - 記錄 Session 層級日誌（開始、停止、錯誤、張數統計）。
4. 擴充性：
   - Service 抽象可替換 mock 與真實硬體實作。

## 9. 錯誤處理規格

1. 連線失敗（Connecting Error）
   - UI 轉 `Error`
   - 顯示 `Retry` 與 `Stop`
2. 感測器中斷（Sensor Lost）
   - 轉 `Error`
   - 可設定自動重試 N 次，失敗後停留 Error
3. 單張掃描失敗（Scan Failed）
   - Session `Failed +1`
   - 保持模式運行，等待下一張
4. 停止時機（Stopping）
   - 停止接受新感測
   - 已開始掃描可收尾後退出

## 10. 與現有程式結構對應

1. [ViewModels/MainViewModel.cs](C:\Users\Jimmy\AI-BCR\ViewModels\MainViewModel.cs)
   - `ScanCommand` 改為 `Start/Stop Auto Scan` 控制
   - 新增 Auto Scan 狀態屬性與 Session 指標
2. [Views/MainWindow.xaml](C:\Users\Jimmy\AI-BCR\Views\MainWindow.xaml)
   - 新增 Auto Scan Overlay Panel、狀態條、Stop 按鈕、觸發動畫資源
3. [Views/MainWindow.xaml.cs](C:\Users\Jimmy\AI-BCR\Views\MainWindow.xaml.cs)
   - 綁定模式切換事件與 UI 狀態更新
4. [ViewModels/AllCardsViewModel.cs](C:\Users\Jimmy\AI-BCR\ViewModels\AllCardsViewModel.cs)
   - 沿用現有 `CardsImportedMessage -> OCR queue`，不改主要流程

## 11. 實作分階段

### Phase 1：互動骨架

1. Header Scan 切換模式（Start/Stop）。
2. 狀態機與 Session 基本欄位。
3. Overlay Panel 視覺框架。

### Phase 2：事件模擬

1. Mock AP/Scanner 連線服務。
2. Mock Paper Sensor 事件觸發。
3. 每次觸發新增卡片並進入背景辨識。

### Phase 3：硬體接軌

1. 以 Adapter 替換 mock 為真實連線/感測/掃描 SDK。
2. 補齊錯誤碼與重試策略。

### Phase 4：強化與驗證

1. Debounce/backpressure 調整。
2. 日誌與度量。
3. UX 微調與性能驗證。

## 12. 驗收標準（DoD）

1. 點擊 `Scan` 後不再立即掃一張，而是進入 Auto Scan 模式。
2. 進入模式後可維持連線並顯示 `Ready / Waiting for paper`。
3. 每次 `Paper Detected` 均會觸發掃描，並新增 1 筆待辨識卡片。
4. 新增卡片會走既有背景辨識流程，狀態可從 Recognizing 變 Done。
5. 使用者離開模式後，不再接受新的感測觸發。
6. 發生單張掃描失敗時 Session 不中斷，錯誤有可視回饋。
7. Overlay Panel 與觸發動畫在整個 Session 可穩定顯示。

## 13. 開放議題

1. Paper Sensor Debounce 預設值（建議 500ms 或 800ms）最終定案。
2. Stop 行為是否允許「立即中斷當前掃描」或僅「收尾後停止」。
3. AI OFF 狀態下 Auto Scan 預設策略：
   - 強制先開 AI
   - 或允許進入並標記為 Manual
