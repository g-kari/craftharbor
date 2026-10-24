# Contributing to CraftHarbor

不具合の再現報告、ドキュメント、翻訳、アクセシビリティ改善、対応サーバーの検証を歓迎します。日本語・英語どちらでも参加できます。

## はじめに

既存のIssuesを検索してから、不具合はBug report、機能提案はFeature requestを使ってください。大きな仕様変更は実装前にIssueで相談してください。個人情報やトークン、ワールド、参加者一覧、未編集のログは公開しないでください。

Windows x64と.NET Framework 4.8を使用します。

```powershell
powershell -File tools/build.ps1
Start-Process dist/CraftHarbor.exe -ArgumentList --core-check -Wait
Get-Content dist/core-check-report.txt
powershell -File tools/check-ui.ps1 -Scenario vanilla
powershell -File tools/check-ui.ps1 -Scenario modpack
powershell -File tools/check-ui.ps1 -Scenario argfile
powershell -File tools/check-integrations.ps1
```

チェックは合成データを使用します。Minecraftや外部サービスへの実接続を確認した場合は、環境・範囲・結果を分けて記載してください。稼働中のサーバーや本物のワールドで破壊的な試験をしないでください。

インストーラーはInno Setup 6.7以降で生成します。`tools/install-inno.ps1`は固定ハッシュ・Authenticode署名を検証した公式コンパイラーを`work/tools/inno`へこのユーザー用に導入します。`tools/build-installer.ps1 -CompilerPath .../ISCC.exe`で`CraftHarbor-Setup-<版>.exe`とインストール版用の`CraftHarbor-Uninstall-<版>.exe`（インストール先では`CraftHarbor-Uninstall.exe`）を生成します。`tools/check-installer.ps1`は既存インストールや同名ショートカットがある場合は停止し、ない場合に限って作業用フォルダーへ試験インストールし、ワールドと設定が残ることを確認してアンインストールします。

## Pull requests

変更の目的、利用者から見た動作、実施した確認、未確認の範囲を書いてください。既存の配色・キーボード操作・高コントラストを維持し、UI変更では秘密情報のない画面例を添えてください。関連する挙動を確かめ、実装を写しただけのテストや無関係な書式変更を避けてください。

ソースや独自アイコンへの貢献は、このプロジェクトと同じMIT Licenseで提供できるものに限ります。Minecraft、MOD、Java、トンネルツール、認証情報は同梱しないでください。

メンテナーは[@g-kari](https://github.com/g-kari)です。返信の時間は保証していません。判断の理由はIssue・PRに残します。[行動規範](CODE_OF_CONDUCT.md)と[セキュリティ報告手順](SECURITY.md)も確認してください。

## リリース

`release.json` の版、AssemblyInfo・UninstallLauncher、Inno Setup、ZIP名、CI artifactパス、リリースノートを揃えたPRを確認してmainへマージします。mainで全チェックが成功し、直前のコミットからrelease.jsonの版が変わった場合だけ、CIが同じ実行の成果物からリリースを公開します。

公開前に6ファイル（setup、uninstall、ポータブルZIPと各SHA-256）のサイズ・ハッシュ・ソースSHAを確認し、まずdraftへアップロードします。GitHub側のダイジェストがすべて一致してから公開します。既存の公開済みファイルや別コミットのタグは上書きしません。失敗時は該当のmain実行を再実行でき、同じソースのdraftから再開します。PR・タグ・手動実行では公開しません。公開権限は専用ジョブのcontents:writeだけです。
