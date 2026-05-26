# UI Design System and Redesign Plan

## Current Scope

本階段的設計系統重構是為了後續可能進行「大規模 UI 風格置換」所做的前置工程。

目前重點是建立穩定的 resource dictionary、design token、共用樣式與命名規則，讓未來若要替換整體視覺風格，可以優先修改 `Styles/` 內的 token 與共用樣式，而不是逐頁修改 `Views/`。

本階段原則：

- 維持現有 UI 結構。
- 維持現有操作流程。
- 維持現有 top bar、navigation、page layout。
- 不新增大型狀態列或新的主要工作區塊。
- 不主動改變 Auto Scan overlay 的行為。
- 不改掃描、OCR、ERP upload 流程。
- 不為了視覺整理新增 ViewModel 或 Service 架構。

允許的調整：

- 將散落在頁面內的 `Style`、`ControlTemplate`、converter resource 移到 `Styles/`。
- 新增 `Bcr*` design tokens。
- 讓既有共用樣式逐步引用 design tokens。
- 清理未使用的 XAML namespace。
- 保留既有 style key，以維持向後相容。

## Deferred Scope

以下工作延後到明確決定進行 UI 風格置換時再處理：

- 大幅改變主畫面資訊架構。
- 新增 top bar 下方的 workflow status bar。
- 重排 `NavigationView`、搜尋區、Auto Scan 區。
- 重新設計 Auto Scan overlay 的內容與流程。
- 導入全新的 visual theme。
- 改 ViewModel 狀態模型以支援新 UI。
- 改 code-behind 或服務層行為。

## Implementation Checklist

### Completed

- 建立 `Styles/` resource dictionary 架構。
- 將 `App.xaml` 縮減為 resource merge 入口。
- 新增 `Theme.xaml`、`Converters.xaml`、`Brushes.xaml`、`Typography.xaml`、`Spacing.xaml`。
- 新增 `Buttons.xaml`、`Inputs.xaml`、`Cards.xaml`、`Lists.xaml`、`Status.xaml`、`Overlays.xaml`。
- 將跨頁 converters 集中到 `Converters.xaml`。
- 將主要 button、card item、list item、status badge、overlay panel 樣式集中到 `Styles/`。
- 將 `Views/ImportDialog.xaml` 的主要 dialog panel、drop zone、loading shield 與 template button 基礎樣式納入設計系統。
- 將 `Controls/EditableField.xaml` 的 display/placeholder 基礎樣式納入 `Inputs.xaml`。
- 新增 `Bcr*` design tokens。
- 讓部分既有共用樣式開始引用 `Bcr*` tokens。
- 清理 `AllCardsPage.xaml` 與 `CardDetailPage.xaml` 的本地 `Style` / `ControlTemplate`。
- 保留既有 style key，維持現有 XAML 相容性。
- 移除先前試作的 top bar 下方 workflow status bar，回到現有 UI 結構。
- 修正 Import dialog 圖片拖曳離開後縮放未完全回復的既有問題。
- 將 `ImportDialog.xaml.cs` 的 drop-zone brush key 與 active/idle 狀態切換集中，並保留既有相容 key。
- 已以 `dotnet build .\PlustekBCR.csproj` 驗證通過。

### Pending

- 做一次人工畫面檢查：MainWindow、AllCardsPage、CardDetailPage、ImportDialog、Auto Scan overlay、Search dropdown。

### Current Guardrails

- 後續調整以維持現有 UI 為前提。
- 不新增主畫面狀態列。
- 不重排 top bar、navigation、search 或 Auto Scan 操作位置。
- 不修改掃描、OCR、ERP upload 流程。
- 不為了樣式整理新增 ViewModel 狀態模型。

## 目的

本文件規劃 AI-BCR UI 重設計與樣式系統化的執行方式。

目標不是一次性重寫 UI，而是先降低樣式分散程度，再逐步導入更清楚、穩定、具操作信心的工作流程介面。

重點原則：

- 保留既有 ViewModel、Service、掃描流程與 OCR 流程。
- 優先集中 UI token 與共用樣式。
- 避免大規模架構改寫。
- 每階段都保持可建置、可回退、可驗證。
- 操作員介面優先呈現掃描狀態、OCR 進度、上傳狀態、連線狀態與錯誤摘要。

## 現況判斷

目前 UI 屬於「半系統化、半散落」。

已經集中管理的部分：

- `App.xaml` 內已有全域 theme resource。
- 已有部分共用樣式，例如主要按鈕、header icon button、AI toggle、sidebar menu item。
- 已有狀態相關 converters，可支援名片處理狀態顯示。

仍然分散的部分：

- `Views/MainWindow.xaml` 內仍有局部 `Grid.Resources` 與區域樣式。
- `Views/AllCardsPage.xaml` 內定義了頁面專屬按鈕、GridViewItem、ListViewItem、GroupHeader 樣式。
- `Views/CardDetailPage.xaml` 內定義了 back button、header action button、image empty state、sidebar list item 樣式。
- 多個頁面對按鈕、卡片、列表、狀態 badge 的視覺規則不完全一致。

根因：

- UI 原型階段以頁面快速完成為主，樣式自然累積在各 XAML 檔案內。
- 全域資源已經存在，但尚未形成清楚的 design token、base control style、composite component style 分層。
- 目前沒有獨立的 UI resource dictionary 結構，因此後續重設計會牽動多個頁面。

## 設計方向

AI-BCR 是企業內部掃描與 OCR 工作流工具。UI 應偏向操作型、穩定型、工業級介面。

優先目標：

- 工作流程清楚。
- 狀態可見。
- 操作員有信心知道系統正在做什麼。
- 錯誤可理解，但不暴露工程細節。
- 主要流程不被除錯資訊干擾。

主畫面應清楚呈現：

- `READY`
- `DETECTING PAPER`
- `SCANNING`
- `OCR PROCESSING`
- `UPLOADING`
- `SUCCESS`
- `ERROR`

每個狀態至少應對應：

- 狀態文字。
- 狀態色。
- 簡短說明。
- 是否正在執行。
- 是否需要操作員介入。

## 分層規劃

建議將 UI 資源分成四層。

### 1. Design Tokens

定義最底層視覺語言。

內容包含：

- 色彩。
- 字體。
- 字級。
- 間距。
- 圓角。
- 陰影。
- border。
- 狀態色。

這一層不應包含具體頁面邏輯。

### 2. Base Control Styles

定義通用控制項樣式。

內容包含：

- Button。
- ToggleButton。
- TextBox。
- ComboBox。
- ListViewItem。
- GridViewItem。
- ProgressRing。

這一層應只描述控制項基本視覺與互動狀態。

### 3. Composite Component Styles

定義由多個控制項組合而成的 UI pattern。

內容包含：

- Status badge。
- Business card item。
- Header action group。
- Search dropdown。
- Auto scan overlay panel。
- Detail inspector panel。

這一層可以具有產品語意，但仍應盡量避免綁死單一頁面。

### 4. Page Layouts

保留在 `Views/`。

內容包含：

- 頁面 grid 結構。
- navigation。
- data binding。
- command binding。
- x:Bind。
- 頁面事件。

頁面應優先引用共用樣式，避免重新定義 control template。

## 建議目錄結構

建議新增 `Styles/` 資料夾：

```text
Styles/
  Theme.xaml
  Converters.xaml
  Brushes.xaml
  Typography.xaml
  Spacing.xaml
  Buttons.xaml
  Inputs.xaml
  Cards.xaml
  Lists.xaml
  Status.xaml
  Overlays.xaml
```

各檔案責任：

- `Theme.xaml`：theme dictionaries 與 Windows App SDK resource overrides。
- `Converters.xaml`：跨頁共用 XAML converters。
- `Brushes.xaml`：品牌色、surface、border、text、semantic colors。
- `Typography.xaml`：字體、字級、字重。
- `Spacing.xaml`：共用 margin、padding、corner radius。
- `Buttons.xaml`：primary、secondary、icon、danger、toolbar button。
- `Inputs.xaml`：search box、text box、combo box。
- `Cards.xaml`：名片卡片、panel、surface container。
- `Lists.xaml`：ListViewItem、GridViewItem、group header。
- `Status.xaml`：status badge、status text、status progress。
- `Overlays.xaml`：modal overlay、auto scan overlay、search dropdown。

`App.xaml` 最終只保留 resource merge 與必要 app-level resource：

```xml
<ResourceDictionary.MergedDictionaries>
    <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
    <ResourceDictionary Source="Styles/Theme.xaml" />
    <ResourceDictionary Source="Styles/Converters.xaml" />
    <ResourceDictionary Source="Styles/Brushes.xaml" />
    <ResourceDictionary Source="Styles/Typography.xaml" />
    <ResourceDictionary Source="Styles/Spacing.xaml" />
    <ResourceDictionary Source="Styles/Buttons.xaml" />
    <ResourceDictionary Source="Styles/Inputs.xaml" />
    <ResourceDictionary Source="Styles/Cards.xaml" />
    <ResourceDictionary Source="Styles/Lists.xaml" />
    <ResourceDictionary Source="Styles/Status.xaml" />
    <ResourceDictionary Source="Styles/Overlays.xaml" />
</ResourceDictionary.MergedDictionaries>
```

## 建議 Token 命名

為了避免與 WinUI 內建 resource 混淆，建議使用產品前綴。

範例：

```xml
<SolidColorBrush x:Key="BcrBrush.Surface" Color="#FFFFFF" />
<SolidColorBrush x:Key="BcrBrush.SurfaceRaised" Color="#F7F8FA" />
<SolidColorBrush x:Key="BcrBrush.Border" Color="#D0D7DE" />
<SolidColorBrush x:Key="BcrBrush.TextPrimary" Color="#1F2328" />
<SolidColorBrush x:Key="BcrBrush.TextSecondary" Color="#484F58" />
<SolidColorBrush x:Key="BcrBrush.Brand" Color="#4285F4" />
<SolidColorBrush x:Key="BcrBrush.Success" Color="#14804A" />
<SolidColorBrush x:Key="BcrBrush.Warning" Color="#B7791F" />
<SolidColorBrush x:Key="BcrBrush.Danger" Color="#D92D20" />
<SolidColorBrush x:Key="BcrBrush.Processing" Color="#2563EB" />
```

狀態 token：

```xml
<SolidColorBrush x:Key="BcrStatus.ReadyBrush" Color="#14804A" />
<SolidColorBrush x:Key="BcrStatus.DetectingBrush" Color="#B7791F" />
<SolidColorBrush x:Key="BcrStatus.ScanningBrush" Color="#2563EB" />
<SolidColorBrush x:Key="BcrStatus.OcrBrush" Color="#7C3AED" />
<SolidColorBrush x:Key="BcrStatus.UploadingBrush" Color="#0E7490" />
<SolidColorBrush x:Key="BcrStatus.SuccessBrush" Color="#14804A" />
<SolidColorBrush x:Key="BcrStatus.ErrorBrush" Color="#D92D20" />
```

## 分階段執行計畫

### Phase 1: 盤點與標記

目標：

- 不改視覺。
- 不改行為。
- 找出可抽出的樣式與重複元件。

工作：

- 檢查 `App.xaml` 的全域 resource。
- 檢查 `MainWindow.xaml` 的 local resources。
- 檢查 `AllCardsPage.xaml` 的 local resources。
- 檢查 `CardDetailPage.xaml` 的 local resources。
- 檢查 `Controls/EditableField.xaml` 是否需要納入共用表單樣式。

產出：

- 元件分類表。
- 樣式搬移優先順序。

驗證：

```powershell
dotnet build .\PlustekBCR.csproj
```

### Phase 2: 建立 Styles 資料夾

目標：

- 建立 resource dictionary 結構。
- 先搬移，不重設計。
- 保持既有 style key，避免一次改太多引用。

工作：

- 新增 `Styles/`。
- 從 `App.xaml` 拆出 brushes、typography、buttons。
- `App.xaml` 改成 merge dictionaries。
- 保留既有 key，例如 `PrimaryActionStyle`，確保舊頁面仍能使用。

驗證：

```powershell
dotnet build .\PlustekBCR.csproj
```

人工檢查：

- App 可啟動。
- Top bar 按鈕樣式正常。
- NavigationView 樣式正常。
- AI toggle 樣式正常。

### Phase 3: 抽出共用控制項樣式

目標：

- 降低各頁 local style 數量。
- 讓同類元件使用一致樣式。

優先抽出：

- `HeaderActionIconButtonStyle`
- `SidebarActionIconButtonStyle`
- `StopAutoScanButtonStyle`
- `BusinessCardItemStyle`
- group header item style
- `CardListViewItemStyle`
- search textbox style

注意：

- 若樣式只用一次且具強烈頁面語意，可以暫時保留在頁面。
- 抽出時保留舊 key 或加 alias，避免破壞現有引用。
- 不改 converter、不改 ViewModel、不改 command。

驗證：

```powershell
dotnet build .\PlustekBCR.csproj
```

人工檢查：

- All Cards grid/list 正常切換。
- Detail page 可開啟。
- Delete、AI reprocess、Back button 外觀與行為正常。
- Search overlay 正常開關。

### Phase 4: 建立狀態 UI 規範

目標：

- 將掃描、OCR、上傳、錯誤狀態轉為一致視覺語言。
- 提升操作員對流程穩定性的信心。

工作：

- 定義 status badge style。
- 定義 status panel style。
- 定義 progress row style。
- 保留既有 `StatusTo*Converter`。
- 如需新增狀態文字，優先在 ViewModel 層提供，不在 XAML 硬編複雜邏輯。

主狀態區建議欄位：

- scanner connectivity。
- paper detection。
- scan state。
- OCR state。
- upload state。
- last error summary。

錯誤顯示規則：

- 主畫面只顯示可理解摘要。
- 詳細 exception 或 raw log 放到可收合 debug panel。
- 不在主要操作畫面顯示 stack trace。

### Phase 5: MainWindow 工作流程重設計

目標：

- 主畫面由「功能入口」提升為「掃描工作流儀表板」。
- Auto Scan 成為最高優先級視覺焦點。

建議調整：

- 保留 top bar 的 `Auto Scan`、`Import`、search、AI toggle。
- 新增大型 workflow status area。
- Auto Scan overlay 改成更清楚的進行中狀態與停止操作。
- 將 debug 資訊放入次要區域。
- 避免過多 modal dialog。

不在此階段修改：

- 掃描服務流程。
- OCR schema。
- ERP upload contract。
- ViewModel 以外的硬體流程。

### Phase 6: AllCardsPage 重設計

目標：

- 提升資料瀏覽效率。
- 名片狀態與處理結果更容易掃視。

建議調整：

- Grid/List 使用同一套 card/list item token。
- Status badge 視覺一致。
- Sidebar 改為穩定 inspector panel。
- 批次操作與 context menu 保持既有行為。
- 圖片縮圖維持固定比例，避免卡片高度跳動。

### Phase 7: CardDetailPage 重設計

目標：

- 提升 OCR 結果檢視、修正與重新處理效率。

建議調整：

- 左側縮圖列表保留。
- 右側分成 image preview、recognized fields、notes/actions。
- `Controls/EditableField.xaml` 納入統一表單視覺。
- AI reprocess、delete、back 使用共用 icon/action button style。

### Phase 8: 驗證與回歸

每次修改後至少執行：

```powershell
dotnet build .\PlustekBCR.csproj
```

必要人工檢查：

- App 啟動。
- 主視窗載入。
- NavigationView 切頁。
- Auto Scan 按鈕可觸發。
- Auto Scan overlay 可開啟與關閉。
- Import dialog 可開啟。
- Search overlay 可開啟與關閉。
- All Cards grid/list 切換。
- Card detail 可進入與返回。
- 狀態 badge 顯示正常。
- OCR mock 狀態流程仍正常。

## 風險與控管

主要風險：

- Resource key 搬移後引用失效。
- ControlTemplate 搬移後 TargetType 或 theme resource 找不到。
- Page local resource 與 App resource key 衝突。
- x:Bind 或 element name 依賴被誤動。
- 視覺重設計時誤改事件或 command binding。

控管方式：

- 先搬移資源，再改視覺。
- 每次只抽一類元件。
- 保留既有 key，必要時新增 alias。
- 不同階段分開提交。
- 每階段執行 `dotnet build`。
- 優先改 XAML resource，不動 code-behind。

## 不在本計畫範圍

以下項目不應在 UI design system 初期一起處理：

- 改 OCR JSON schema。
- 改掃描硬體流程。
- 改 ERP upload contract。
- 大規模重寫 ViewModel。
- 導入全新前端框架。
- 移除既有頁面導覽架構。
- 將 debug log 直接放進主操作畫面。

## 建議執行順序摘要

1. 建立 `Styles/` resource dictionary 架構。
2. 搬移 `App.xaml` 內既有全域樣式。
3. 抽出 button、list、card、status、overlay 樣式。
4. 清理 `MainWindow.xaml` local styles。
5. 清理 `AllCardsPage.xaml` local styles。
6. 清理 `CardDetailPage.xaml` local styles。
7. 建立主工作流程狀態區。
8. 重設計 All Cards 與 Detail 的資料操作介面。
9. 每階段 build 與人工檢查。
