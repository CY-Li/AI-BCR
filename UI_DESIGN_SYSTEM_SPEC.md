# UI Design System Specification

## Design System Purpose

本設計系統的主要目的，是作為未來大規模 UI 風格置換的前置基礎。

現階段不是立即進行視覺改版，也不是重新設計主操作流程，而是先建立穩定、可替換、可維護的樣式層。未來若要切換到新的工業風、品牌色、字體、按鈕風格、卡片風格或狀態視覺，應優先透過 `Styles/` 內的 token 與共用樣式完成。

## Non-Disruptive Adoption Rules

導入設計系統時必須遵守以下規則：

- 不改變現有 UI layout，除非另有明確需求。
- 不新增 top bar 下方狀態列。
- 不重排既有 navigation、search、Auto Scan、Import、AI toggle。
- 不改變 Auto Scan overlay 的操作行為。
- 不因樣式整理修改掃描、OCR、ERP upload 流程。
- 不移除既有 style key，除非所有引用都已安全替換。
- 新增 token 時應保持向後相容。
- 頁面 XAML 應逐步減少本地 `Style` 與 `ControlTemplate`，但不應為了抽象而重寫 layout。

## Future Visual Replacement Rules

若未來正式進入大規模 UI 風格置換，應先確認：

- 新視覺方向。
- 是否允許調整主畫面資訊架構。
- 是否允許新增 workflow status area。
- 是否允許修改 ViewModel 顯示屬性。
- 是否需要保留原操作員工作流程。
- 是否需要與硬體 Auto Scan 流程同步驗證。

## 文件目的

本文件定義 AI-BCR Scanner Management System 的 UI 設計系統規格。

此規格用於後續重設計、樣式抽離與元件統一。實作時應優先遵守本文件，再參考 `UI_DESIGN_SYSTEM_PLAN.md` 的分階段執行方式。

本文件不改變掃描流程、OCR schema、ERP upload contract 或 ViewModel 行為。

## 設計目標

AI-BCR 是企業內部掃描、OCR 與名片資料管理工具。UI 應服務長時間、重複性、低容錯的操作情境。

核心目標：

- 讓操作員清楚知道目前系統狀態。
- 讓掃描、OCR、上傳流程可被快速辨識。
- 讓錯誤可被理解與處理。
- 讓主要操作不被工程資訊干擾。
- 讓畫面呈現穩定、可靠、工業級工具感。

## 設計原則

### Workflow First

主畫面應優先呈現工作流，而不是單純功能入口。

主要狀態必須可見：

- scanner connectivity
- paper detection
- scan state
- OCR state
- upload state
- last error summary

### Operator Confidence

操作員需要知道系統是否正在工作、是否卡住、是否需要人工介入。

任何長時間流程都應有：

- 明確狀態文字。
- 可辨識的進行中視覺。
- 完成或失敗結果。
- 下一步操作入口。

### Reliability Feeling

視覺應穩定、乾淨、可預期。

應避免：

- 過度動畫。
- 過多 modal dialog。
- 工程 debug 資訊佔據主要畫面。
- 視覺風格在不同頁面跳動。

### Backward Compatibility

設計系統導入時應保持既有 style key 可用，避免一次改動大量 XAML。

允許方式：

- 保留舊 key。
- 新增 alias key。
- 逐頁替換。
- 每階段 build 驗證。

## Resource Dictionary 結構

建議使用以下結構：

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

### Theme.xaml

用途：

- Light/Dark theme dictionaries。
- WinUI resource overrides。
- Application-level theme defaults。

不應放入：

- 單一頁面樣式。
- business card item template。
- scanner workflow layout。

### Converters.xaml

用途：

- visibility converters。
- status converters。
- image/date converters。

所有跨頁使用的 XAML converter resource 應集中於此檔案。

### Brushes.xaml

用途：

- brand colors。
- surface colors。
- text colors。
- border colors。
- semantic colors。

所有共用顏色應優先從此檔案引用。

### Typography.xaml

用途：

- font family。
- font size。
- font weight。
- text style。

### Spacing.xaml

用途：

- margin。
- padding。
- corner radius。
- fixed size token。

### Buttons.xaml

用途：

- primary button。
- secondary button。
- icon button。
- danger button。
- toolbar button。
- toggle button。

### Inputs.xaml

用途：

- search text box。
- text box。
- combo box。
- date picker input。

### Cards.xaml

用途：

- card container。
- raised panel。
- business card preview surface。
- image preview surface。

### Lists.xaml

用途：

- ListViewItem。
- GridViewItem。
- group header。
- sidebar list item。

### Status.xaml

用途：

- status badge。
- workflow status panel。
- progress indicator。
- error summary。

### Overlays.xaml

用途：

- modal overlay。
- auto scan overlay。
- search dropdown。
- confirmation panel。

## 命名規範

所有新設計系統資源使用 `Bcr` 前綴，避免與 WinUI 內建 resource key 混淆。

格式：

```text
Bcr{Category}.{Name}
```

範例：

```text
BcrBrush.Surface
BcrBrush.TextPrimary
BcrRadius.Control
BcrSpace.Md
BcrButton.Primary
BcrStatus.Badge
```

舊有 key 可保留：

```text
PrimaryActionStyle
SecondaryActionStyle
HeaderIconButtonStyle
AiToggleButtonStyle
```

保留舊 key 的目的：

- 降低初期導入風險。
- 避免一次修改所有引用。
- 保持舊頁面可正常載入。

## Color Tokens

### Base Brushes

```xml
<SolidColorBrush x:Key="BcrBrush.AppBackground" Color="#F4F6F8" />
<SolidColorBrush x:Key="BcrBrush.Surface" Color="#FFFFFF" />
<SolidColorBrush x:Key="BcrBrush.SurfaceMuted" Color="#F7F8FA" />
<SolidColorBrush x:Key="BcrBrush.SurfaceRaised" Color="#FFFFFF" />
<SolidColorBrush x:Key="BcrBrush.Border" Color="#D0D7DE" />
<SolidColorBrush x:Key="BcrBrush.BorderMuted" Color="#E3E6EA" />
<SolidColorBrush x:Key="BcrBrush.TextPrimary" Color="#1F2328" />
<SolidColorBrush x:Key="BcrBrush.TextSecondary" Color="#484F58" />
<SolidColorBrush x:Key="BcrBrush.TextMuted" Color="#6E7781" />
<SolidColorBrush x:Key="BcrBrush.Brand" Color="#2563EB" />
```

### Semantic Brushes

```xml
<SolidColorBrush x:Key="BcrBrush.Success" Color="#14804A" />
<SolidColorBrush x:Key="BcrBrush.Warning" Color="#B7791F" />
<SolidColorBrush x:Key="BcrBrush.Danger" Color="#D92D20" />
<SolidColorBrush x:Key="BcrBrush.Info" Color="#0E7490" />
<SolidColorBrush x:Key="BcrBrush.Processing" Color="#2563EB" />
```

### Status Brushes

```xml
<SolidColorBrush x:Key="BcrStatus.Ready" Color="#14804A" />
<SolidColorBrush x:Key="BcrStatus.DetectingPaper" Color="#B7791F" />
<SolidColorBrush x:Key="BcrStatus.Scanning" Color="#2563EB" />
<SolidColorBrush x:Key="BcrStatus.OcrProcessing" Color="#7C3AED" />
<SolidColorBrush x:Key="BcrStatus.Uploading" Color="#0E7490" />
<SolidColorBrush x:Key="BcrStatus.Success" Color="#14804A" />
<SolidColorBrush x:Key="BcrStatus.Error" Color="#D92D20" />
<SolidColorBrush x:Key="BcrStatus.Offline" Color="#6E7781" />
```

## Typography Tokens

建議 token：

```xml
<x:Double x:Key="BcrFontSize.Caption">11</x:Double>
<x:Double x:Key="BcrFontSize.Body">14</x:Double>
<x:Double x:Key="BcrFontSize.Label">12</x:Double>
<x:Double x:Key="BcrFontSize.SectionTitle">16</x:Double>
<x:Double x:Key="BcrFontSize.PageTitle">24</x:Double>
<x:Double x:Key="BcrFontSize.WorkflowStatus">36</x:Double>
```

使用規則：

- 操作狀態文字可使用較大字級。
- 卡片、列表、側欄內文字不應使用 hero-scale type。
- Label 與 metadata 應低於 body text 權重。
- 不使用負 letter spacing。

## Spacing and Radius Tokens

```xml
<Thickness x:Key="BcrSpace.Xs">4</Thickness>
<Thickness x:Key="BcrSpace.Sm">8</Thickness>
<Thickness x:Key="BcrSpace.Md">12</Thickness>
<Thickness x:Key="BcrSpace.Lg">16</Thickness>
<Thickness x:Key="BcrSpace.Xl">24</Thickness>

<CornerRadius x:Key="BcrRadius.Control">6</CornerRadius>
<CornerRadius x:Key="BcrRadius.Card">8</CornerRadius>
<CornerRadius x:Key="BcrRadius.Panel">8</CornerRadius>
<CornerRadius x:Key="BcrRadius.Overlay">8</CornerRadius>
```

使用規則：

- 一般卡片圓角不超過 `8`。
- 主要面板圓角保持克制。
- 大型操作區可使用較明確的 border 與背景層次，不依賴過度陰影。

## Button 規格

### Primary Button

用途：

- Auto Scan。
- Import。
- Search submit。
- Confirm action。

視覺要求：

- 高對比。
- 明確 hover/pressed 狀態。
- 可容納 icon + text。
- 高度應穩定，不因內容改變跳動。

### Secondary Button

用途：

- Cancel。
- Clear。
- Less prominent command。

### Icon Button

用途：

- toolbar action。
- delete。
- edit。
- AI reprocess。
- close。

要求：

- 必須有 tooltip。
- icon 應優先使用現有 icon library 或 WinUI glyph。
- destructive action 使用 danger color。

### Danger Button

用途：

- Delete。
- Stop Auto Scan。
- Confirm destructive action。

要求：

- 使用 danger brush。
- 不應與 primary button 視覺相同。
- 文字需明確，不使用模糊語意。

## Status UI 規格

狀態是 AI-BCR 的核心 UI 元件。

### Status Badge

用途：

- 名片 OCR 狀態。
- upload 狀態。
- scanner 連線狀態。

內容：

- 狀態文字。
- 狀態色。
- 可選 progress indicator。

規則：

- 進行中狀態可顯示 small progress ring。
- 成功狀態使用 success。
- 錯誤狀態使用 danger。
- offline 或 unknown 使用 neutral。

### Workflow Status Panel

用途：

- 主畫面顯示目前掃描流程。

必要欄位：

- primary status。
- secondary explanation。
- scanner connection。
- paper detection。
- OCR progress。
- upload progress。
- last error。

建議狀態文字：

```text
READY
DETECTING PAPER
SCANNING
OCR PROCESSING
UPLOADING
SUCCESS
ERROR
OFFLINE
```

## Card 規格

### Business Card Grid Item

用途：

- `AllCardsPage` Grid mode。

要求：

- 固定寬度或穩定 min/max width。
- 圖片區固定 aspect ratio。
- 名稱、公司使用 ellipsis。
- 狀態 badge 位於固定位置。
- hover 不應造成 layout shift。

### Business Card List Item

用途：

- `AllCardsPage` List mode。
- `CardDetailPage` 左側縮圖列表。

要求：

- 橫向資訊可快速掃視。
- thumbnail 尺寸固定。
- selected/hover 狀態清楚。

### Detail Inspector Panel

用途：

- 側欄或詳細頁資訊檢視。

要求：

- 分區清楚。
- 編輯欄位與唯讀欄位視覺可辨識。
- 主要 action 放在固定區域。

## Overlay 規格

### Auto Scan Overlay

用途：

- 自動掃描進行中或等待紙張時顯示。

要求：

- 顯示目前掃描狀態。
- 顯示已掃描數量。
- 顯示 scanner/OCR/upload 子狀態。
- `Stop Auto Scan` 必須清楚可見。
- 不應阻塞 UI thread。

### Search Overlay

用途：

- Recent search。
- Advanced search。

要求：

- 位置應跟 search input 對齊。
- 不應遮住主要狀態區過多。
- close behavior 必須直覺。

### Error Overlay

原則：

- 非必要不使用 modal error。
- 優先使用 inline error summary。
- 嚴重且需要操作員確認時才使用 modal。

## Debug Information 規格

debug 資訊不得主導主要操作畫面。

允許：

- collapsible debug panel。
- secondary diagnostics view。
- last error summary。
- reconnect status。

避免：

- raw sensor spam。
- stack trace。
- internal exception。
- excessively verbose logs。

## Accessibility and Responsiveness

要求：

- 重要狀態不可只靠顏色辨識。
- icon button 需有 tooltip。
- 文字需避免被裁切。
- 長文字需使用 wrapping 或 ellipsis。
- 主要 action hit target 不應太小。
- 視窗縮放時 layout 不應重疊。

## Animation 規格

動畫應用於狀態轉換與操作回饋，不應成為裝飾主體。

允許：

- Auto Scan pulse。
- panel entrance。
- button hover/pressed。
- progress indicator。

限制：

- 不使用長時間、強烈、反覆分散注意力的動畫。
- 錯誤狀態不使用誇張動畫。
- 動畫不可影響流程穩定或造成操作延遲。

## Implementation Rules

實作時遵守：

- 優先修改 XAML resource。
- 優先保留既有 binding、command、event handler。
- 不因 UI 整理改動 service。
- 不因樣式抽離改動 OCR schema。
- 不將 ViewModel 邏輯搬到 code-behind。
- 不新增與現有架構不一致的大型 UI framework。

每次改動後執行：

```powershell
dotnet build .\PlustekBCR.csproj
```

## Migration Compatibility

導入新設計系統時，舊 key 應逐步對應到新 key。

範例：

```xml
<Style x:Key="PrimaryActionStyle"
       TargetType="Button"
       BasedOn="{StaticResource BcrButton.Primary}" />
```

若 WinUI XAML 限制導致 `BasedOn` 不適用，則保留舊 style 內容直到該頁完成替換。

## Definition of Done

設計系統階段完成條件：

- `Styles/` resource dictionary 建立完成。
- `App.xaml` 主要負責 merge dictionaries。
- 常用 button style 已集中。
- 常用 list/grid item style 已集中。
- status badge 與 workflow status 規格已落地。
- 主要頁面不再大量重複 control template。
- `dotnet build` 通過。
- 主畫面、All Cards、Card Detail 基本流程人工檢查通過。
