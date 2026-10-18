# Hashelf

よく使うファイル・フォルダー・URL を登録して、`Ctrl + Alt + Space` で呼び出して開く Windows 用のランチャーです。
グループによる階層、タグ（グループのタグは中の項目に継承）、名前・`#タグ` での検索に対応しています。

名前の表記は、画面のタイトルやロゴでは「#Hashelf」、製品名・exe・フォルダー・自動起動の登録名では「Hashelf」です。
コードの名前空間とプロジェクトのフォルダーは、改名前の `DesktopLauncher` のままです。

利用者向けの説明は [docs/distribution/README.txt](docs/distribution/README.txt)、利用条件は [docs/distribution/LICENSE.txt](docs/distribution/LICENSE.txt) にあります。

## 構成

| パス | 内容 |
|---|---|
| `src/DesktopLauncher/` | アプリ本体（WPF、.NET 10）。トレイアイコンだけ Windows Forms |
| `tests/DesktopLauncher.Tests/` | xUnit のテスト |
| `docs/distribution/` | 配布物に入れる README・利用条件・外部部品のライセンス表記の元 |
| `scripts/publish.ps1` | 配布用フォルダを作る |
| `scripts/make-icon.ps1` | `Assets/app.ico`（ロゴ）を作り直す |

データは exe の横の `data` フォルダ（`items.json`・`settings.json`・`links/`）に保存します。
フォルダの外に書き込むのは、「Windows 起動時に開始」をオンにしたときのレジストリ（`HKCU\...\Run` の `Hashelf`）だけです。
改名前の版が同じフォルダから `DesktopLauncher` の名前で登録していた場合は、起動時に `Hashelf` へ移します。

## ビルドとテスト

```powershell
dotnet build -warnaserror
dotnet test
```

CI（`.github/workflows/ci.yml`）は、main へのプッシュとプルリクエストのたびに windows-latest でビルド・テスト・発行の確認を行います。

## 配布物の作り方

```powershell
.\scripts\publish.ps1
```

`artifacts\Hashelf\` を作り直し、次のものを入れます。
このフォルダを zip にして配布します。

- 自己完結型で発行したアプリ（.NET ランタイム同梱。単一ファイルにすると `%TEMP%` にネイティブ DLL を取り出すため、複数ファイルのまま）
- `README.txt`・`LICENSE.txt`：`docs/distribution/` からコピー
- `THIRD-PARTY-NOTICES.txt`：`docs/distribution/third-party-notices.txt` に、MIT の本文と付録（.NET ランタイムのパッケージ内の表記、`docs/distribution/licenses/` の WPF・Windows Forms の表記）を連結したもの

テキストはメモ帳で文字化けしないよう、UTF-8（BOM 付き）・CRLF で書き出します。
出力先は毎回作り直すので、手元で動かしたときの `data` フォルダは配布物に入りません。

外部の NuGet パッケージを追加したときは、`third-party-notices.txt` の一覧とライセンス本文を追記してください。
WPF・Windows Forms の表記は、[dotnet/wpf](https://github.com/dotnet/wpf/blob/release/10.0/THIRD-PARTY-NOTICES.TXT) と [dotnet/winforms](https://github.com/dotnet/winforms/blob/release/10.0/THIRD-PARTY-NOTICES.TXT) の原文です。
