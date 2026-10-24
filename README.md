# Astral Server Manager

Windows向けの、Create: Astralサーバーを操作するローカル管理アプリです。
MIT License。Minecraft、Create: Astral、playit.gg、Discordの公式製品ではありません。

**開発プレビューです。現在のリリースは未署名です。** Smart App Controlが有効なPCでは起動できない場合があります。
保護機能の無効化や回避を行う機能はありません。署名の審査・取得は未完了です。
本プロジェクトにSignPathの支援や証明書が付与されたという意味ではありません。

## 機能

- サーバーの起動、保存停止、再起動。playitエージェントの起動・停止。
- CPU、プロセスRAM、TPS、MSPT、人数、稼働時間、ディスク空き、履歴グラフ、CSV。
- 専用PC向けのメモリ予算計算。OSと使用中のメモリを残し、ヒープ最大量を起動時に決定。
- コンソール、ログ検索、参加許可、OP、キック、BAN、告知。
- 基本設定、完全バックアップ・復元、同梱FTB Backupsの設定、予約再起動。
- 左ナビ、文字ラベル付きアイコン、文字拡大、高コントラスト、キーボード操作。
- Discordへコピーできる参加案内。任意のWebhook通知（初期状態は無効、URLはDPAPI保存）。

## 状態と制限

現在はCreate: Astral 2.1.5b / Minecraft Java 1.18.2 / Java 17 / Fabric向けです。
過去のローカル版ではサーバー起動・保存停止と実測TPSの表示を確認しています。
現行プレビューの新UI・自動メモリ・Webhookは、この開発PCの署名要件により実機検証が未完了です。
CIのチェックは計算・検証ロジックと合成データを使うUI生成が対象で、実サーバーの接続やスクリーンリーダー対応を保証しません。
CIの画面プレビューは空の合成ワークスペースを使ったもので、プレイヤーの本物のワールドや稼働中サーバーの画面ではありません。

## 必要なもの

- Windows 10/11 x64 と .NET Framework 4.8。
- Java 17。
- ユーザー自身が正規の配布元から入手したCreate: Astral公式サーバーパック。
- 外部公開する場合はplayitの公式Windows x64 CLI 0.17.1。別途アカウントとMinecraft Javaトンネルが必要。
- Minecraft EULAへの同意はユーザー自身で行ってください。

Minecraft、Java、MOD、ワールド、playitのバイナリは、このリポジトリにも管理アプリの配布ZIPにも含まれません。

## ワークスペース

既存サーバーを、以下の構成で用意してください。

```text
my-server/
  server/
    server.jar
    server.properties
    mods/
    config/
    ...公式サーバーパックのファイル...
  playit/
    playit-0.17.1.exe
  java-path.txt       # 任意: java.exeの絶対パスを1行で指定
```

管理アプリを起動して、`server`の親フォルダーを選びます。アプリと同じフォルダーに上記構成があれば自動検出します。
`AstralManager.exe --workspace "D:\Minecraft\my-server"` でも指定できます。
サーバー未導入時は選択をキャンセルし、READMEに従って準備してください。

起動後、Minecraft Javaトンネルのローカル接続先を`127.0.0.1:25565`に設定し、発行されたアドレスを保存します。
友達のクライアントもCreate: Astral 2.1.5bに揃え、Java版ユーザー名を参加許可リストへ追加してください。
EULA同意、オンライン認証、待受アドレス、許可リストは、既存サーバーの設定を確認してください。

## ビルド

```powershell
powershell -File tools/build.ps1
powershell -File tools/package.ps1
```

出力は`dist/`です。ビルドだけではコード署名は付与されません。
アイコンもこのリポジトリに含まれる独自制作のものです。

ネットワーク送信やMinecraft起動をしないコアチェック:

```powershell
Start-Process dist/AstralManager.exe -ArgumentList --core-check -Wait
Get-Content dist/core-check-report.txt
```

Smart App ControlがブロックするPCではチェック用引数でも実行できません。代替ホストでブロックされたEXEを実行する方法は案内しません。

## 操作

Alt+1〜9で画面切替、Ctrl+Lでコンソール入力、F1で参加者向け案内へ移動します。
自動メモリは「全RAMを直ちに予約する」方式ではありません。初期4GiBから、起動時に算出した上限までJVMが利用します。
完全復元は停止中だけ実行し、変更前のサーバーは`recovery/`へ保管します。
予約再起動などの管理アプリ側の機能は、アプリ起動中のみ実行されます。

## Privacy / Code signing policy

- [Privacy policy](PRIVACY.md)
- [Code signing policy](CODE_SIGNING_POLICY.md)
- [SignPath preparation](docs/SIGNPATH.md)
- [Security reporting](SECURITY.md)

## アンインストール

保存してサーバーを停止し、管理アプリを終了してから、アプリの展開フォルダーを削除します。
ワールドを残す場合はワークスペースを削除しないでください。
保存したワークスペース選択情報は`%LOCALAPPDATA%/AstralServerManager/workspace.txt`です。
Webhook設定は管理アプリの「送信先を解除」で消去できます。Discord側のWebhookはDiscord上で管理してください。

## 参考・外部サービス

- [Create: Astral](https://www.curseforge.com/minecraft/modpacks/create-astral)
- [Minecraft EULA](https://www.minecraft.net/eula)
- [playit agent](https://github.com/playit-cloud/playit-agent)
- [spark](https://spark.lucko.me/)
- [Discord webhooks](https://docs.discord.com/developers/resources/webhook)
