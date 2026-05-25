# Plustek AI-BCR

Windows WinUI 3 名片管理原型專案（含掃描/匯入/辨識流程模擬）。

## 目前狀態（2026-05-25）

- 性質：**可互動 UI 原型 + Mock 流程**
- 已可用：
  - 掃描確認流程（含 AI OFF 警示）
  - CSV/XLSX 匯入與圖片匯入
  - 卡片列表（Grid/List）與側邊詳情
  - 卡片詳細頁編輯、圖片上傳/拖放、備註新增
  - 背景 mock OCR（Recognizing -> Done）
- 尚未落地：
  - 真實 AI/OCR 引擎
  - 掃描器硬體整合
  - SQLite 持久化
  - 完整匯出功能
  - 自動化測試

## 技術棧

- `.NET 8`
- `WinUI 3 (Windows App SDK)`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.Hosting`（DI）
- `MiniExcel`（CSV/XLSX 匯入）
- `WinUIEx`

## 專案結構（核心）

- `Views/`：`MainWindow`, `AllCardsPage`, `CardDetailPage`, `ImportDialog`, `EmptyPage`
- `ViewModels/`：`MainViewModel`, `AllCardsViewModel`, `CardDetailViewModel`
- `Models/`：`BusinessCard`, `Note`, `ProcessingStatus`, `CardsImportedMessage`
- `Helpers/`：CSV/Excel 讀取、UI Converter
- `Controls/`：可編輯欄位控制項

## 執行需求

- Windows 10/11
- Visual Studio 2022（建議）
- .NET SDK（建議 8.x）

## 啟動方式

### Visual Studio

1. 開啟 `PlustekBCR.csproj`。
2. 選擇啟動設定檔 `PlustekBCR (Unpackaged)`。
3. 按 `F5`。

### CLI

```powershell
dotnet run --project .\PlustekBCR.csproj --launch-profile "PlustekBCR (Unpackaged)"
```

## 建置

```powershell
dotnet build .\PlustekBCR.csproj
```

若建置出現 `MSB3021/MSB3027`（exe 被占用），請先關閉執行中的 `PlustekBCR.exe` 再重建。

## 自動更新（已實作）

啟動時會讀取 `appsettings.json` 的 `Update` 設定並檢查更新：

```json
{
  "Update": {
    "Enabled": true,
    "ManifestUrl": "https://your-domain.com/plustekbcr/update.json",
    "CheckTimeoutSeconds": 5
  }
}
```

`ManifestUrl` 回傳格式：

```json
{
  "version": "1.0.3.0",
  "downloadUrl": "https://your-domain.com/plustekbcr/PlustekBCR-1.0.3-setup.exe",
  "notes": "修正匯入流程與穩定性問題"
}
```

當遠端 `version` 大於目前版本時，程式會跳出提示並開啟 `downloadUrl`。

### GitHub 整合（已配置）

專案已內建：

- 應用程式更新來源：`https://cy-li.github.io/AI-BCR/update.json`
- Workflow：`.github/workflows/publish-update-manifest.yml`

流程如下：

1. 在 GitHub 建立並發佈一個 Release（`published`）。
2. Release 內上傳安裝檔（建議 `.exe` / `.msix` / `.zip`）。
3. Workflow 會自動產生最新 `update.json` 並部署到 GitHub Pages。
4. App 啟動時會檢查該 manifest，有新版本即提示更新。

注意：

- Tag 請使用可被 .NET `Version` 解析的格式，例如 `v1.0.3.0` 或 `1.0.3.0`。
- 請在 GitHub Repository 設定中啟用 **Pages**（Source: GitHub Actions）。

## 已知限制

1. 資料預設儲存在記憶體，重開程式會遺失。
2. OCR 結果為隨機 mock，不代表真實辨識品質。
3. 搜尋/進階搜尋 UI 已存在，但尚未完成實際篩選。
4. 部分字串存在編碼與在地化一致性問題，需後續清理。

## 文件

- 功能現況規格請見 [FSD.md](./FSD.md)
