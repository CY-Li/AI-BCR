# Plustek AI-BCR

Windows 桌面名片管理與 AI OCR 整合專案。

目前倉庫狀態是 **WinUI 3 / MVVM / 服務分層的 transitional prototype**：

- 已有可運作的名片管理、搜尋、設定、本地化、更新檢查與 AI 辨識骨架
- Auto Scan 與部分掃描流程仍保留 prototype / mock 行為
- 重點是 workflow 穩定、相容性、可回復性，不是大幅重構

## 目前可用功能

- 名片列表與詳細頁
- 搜尋與進階搜尋
- 標籤管理
- 匯入 CSV / XLSX
- 匯入圖片
- 圖片上傳與拖放
- AI 辨識 queue 與失敗 fallback
- 日本郵遞區號查詢
- UI 語言切換
- 更新檢查

## 技術棧

- `.NET 8`
- `WinUI 3`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.Hosting`
- `Microsoft.WindowsAppSDK`
- `MiniExcel`
- `WinUIEx`
- `.resx` 本地化

## 專案結構

- `App.xaml` / `App.xaml.cs`
  - 應用程式入口、DI 組裝、啟動流程

- `Views/`
  - `MainWindow`
  - `AllCardsPage`
  - `CardDetailPage`
  - `ImportDialog`
  - `SettingsPage`
  - `EmptyPage`

- `ViewModels/`
  - `MainViewModel`
  - `AllCardsViewModel`
  - `CardDetailViewModel`
  - `EmptyViewModel`

- `Services/`
  - 設定、更新、本地化、標籤 catalog、郵遞區號查詢、辨識 queue、Plustek Console 整合

- `Models/`
  - 名片資料、辨識模型、查詢選項、狀態列舉

- `Helpers/`
  - 字串、格式化、UI helper、converter、本地化包裝

- `Controls/`
  - 自訂控制項，例如 `EditableField`

- `Resources/`
  - `Strings.resx`
  - `Strings.ja-JP.resx`

- `Styles/`
  - 主題、brush、字體、間距、按鈕、輸入框、清單、狀態、overlay

## 執行需求

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 或等效開發環境

## 啟動方式

### Visual Studio

1. 開啟 `PlustekBCR.csproj`
2. 選擇 `PlustekBCR (Unpackaged)`
3. 按 `F5`

### CLI

```powershell
dotnet run --project .\PlustekBCR.csproj --launch-profile "PlustekBCR (Unpackaged)"
```

## 建置

```powershell
dotnet build .\PlustekBCR.csproj
```

如果建置時遇到 `MSB3021/MSB3027`，通常是執行中的 `PlustekBCR.exe` 仍占用輸出檔，先關閉程式再重建。

## 設定檔

主要設定檔是 `appsettings.json`，目前包含：

- `Update`
- `TagOptions`
- `Localization.UiLanguage`
- `Recognition.IsAiEnabled`
- `PlustekConsole.JP`
- `PlustekConsole.US`
- `BusinessCard.CurrentMarket`

這些設定會被下列服務讀寫：

- `ApplicationSettingsService`
- `LocalizationService`
- `TagCatalogService`
- `UpdateService`

## 更新機制

應用程式啟動時會檢查 `appsettings.json` 的 `Update` 設定，並依 `ManifestUrl` 讀取更新資訊。

預設來源：

```json
{
  "Update": {
    "Enabled": true,
    "ManifestUrl": "https://raw.githubusercontent.com/CY-Li/AI-BCR/main/update.json",
    "CheckTimeoutSeconds": 3
  }
}
```

若遠端版本較新，程式會提示更新並開啟下載連結。
