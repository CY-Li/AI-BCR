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
- 獨立名片圖片 Viewer（縮放、平移、正反面切換與置頂）
- AI 辨識 queue 與失敗 fallback
- 日本郵遞區號查詢
- UI 語言切換
- 更新檢查
- 掃描／匯入名片的重複比對與待確認流程
- 可自訂比對欄位、OR／AND 條件與快捷規則

## 圖片 Viewer

在 All Cards 右側預覽或名片詳細頁直接點擊正面／背面圖片，即可開啟獨立圖片 Viewer。Viewer 維持單一視窗，可移動到第二螢幕並在主畫面繼續核對或編輯欄位。

- 滑鼠滾輪：以游標位置為中心放大／縮小
- 滑鼠拖曳：圖片大於可視範圍時平移查看完整內容
- `-`／`+`：以 25% 步進縮放，範圍為 25%～400%
- `100%`：回到原始尺寸並重設平移位置
- 適合視窗：完整顯示圖片並隨 Viewer 尺寸調整
- Front Side／Back Side：切換存在的正反面圖片
- 置頂：預設啟用，可從工具列切換；本次程式執行期間保留選擇
- `Esc` 或 Windows 標題列關閉按鈕：關閉 Viewer

Viewer 會固定顯示開啟時的名片，不會因主畫面選取其他名片而自動切換；重新掃描、上傳或刪除該名片圖片時會同步更新。刪除目前顯示的名片或關閉主程式時，Viewer 會一併關閉。

## 重複名片比對

系統會在 CSV／XLSX 匯入完成後，以及圖片或掃描名片 OCR 成功後，將新名片與目前記憶體中的名片集合比對。預設規則為 `Email + OR`。

- `OR`：任一選定且非空白的欄位相同即列為候選
- `AND`：所有選定欄位在雙方皆有值且相同才列為候選
- 一般文字會執行 Unicode FormKC、Trim、連續空白合併及不分大小寫比較
- 電話、分機、傳真與手機會額外忽略空白、括號及連字號
- 空白值不會互相命中，且同一個名片 ID 不會與自身比對
- 同一批匯入依序加入與比對，因此可找出批次內的重複資料

疑似重複資料會保留在集合中並標記為待確認，不會中止自動掃描。側欄提供：

- `Replace`：按鈕固定顯示 `Replace`；點擊後以確認視窗提示實際刪除筆數，確認後重新比對目前集合，刪除所有與目前候選相符的其他名片，只保留目前候選及其原始 ID
- `Keep`：保留目前候選與所有既有名片，並一次結束目前候選的全部提示

Replace 確認視窗預設焦點為 `Cancel`。使用者取消、目前候選已被移除，或確認前候選關係已失效時，不會執行刪除。

刪除、Replace、OCR 完成、比對欄位編輯或規則變更後，系統會依名片進入集合的順序自動重建候選關係。較新的名片只會比對較早且已完成 OCR 的名片，因此刪除候選目標後不會留下失效徽章，也不會形成互相指向的循環候選。

內部審核狀態仍採上述單向順序；側欄摘要與 Replace 則會即時查詢整個已完成 OCR 的集合。因此連續掃描三張相同名片時，第一張作為基準，第二、第三張顯示待確認，且第二張與第三張的側欄都會顯示目前完整的兩筆相符資料。

Settings 的 General 頁可選擇欄位與 OR／AND，並提供 `Email only`、`Name + Company`、`Contact methods`、`Custom`。設定儲存於 `appsettings.json` 的 `DuplicateDetection` 節點；缺少、損壞或沒有有效欄位時會回退至 Email／OR。

> 目前名片、候選結果與審核狀態只存在應用程式記憶體，尚未接資料庫或外部 API。

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
  - `ImageViewerWindow`
  - `ImportDialog`
  - `SettingsPage`
  - `EmptyPage`

- `ViewModels/`
  - `MainViewModel`
  - `AllCardsViewModel`
  - `CardDetailViewModel`
  - `EmptyViewModel`
  - `DuplicateSettingsViewModel`
  - `ImageViewerState`

- `Services/`
  - 設定、更新、本地化、標籤 catalog、郵遞區號查詢、圖片 Viewer、辨識 queue、Plustek Console 整合

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
- `DuplicateDetection.MatchOperator`
- `DuplicateDetection.Fields`

這些設定會被下列服務讀寫：

- `ApplicationSettingsService`
- `LocalizationService`
- `TagCatalogService`
- `UpdateService`

## 測試

```powershell
dotnet test .\PlustekBCR.Tests\PlustekBCR.Tests.csproj
dotnet build .\PlustekBCR.csproj
```

目前重複比對服務包含 15 項單元測試，涵蓋 Email／Unicode／空白、電話格式、OR／AND、空值、多候選、自身排除、設定回退及集合重建。

## 變更與交接文件

- [CHANGELOG.md](CHANGELOG.md)：本輪重複比對、Settings UI 與 AI 圖示變更
- [docs/backend-handover.md](docs/backend-handover.md)：既有後端交接文件

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
