# CraftHarbor

Windows向けの汎用Minecraft Javaサーバーマネージャーです。
Vanilla、Paper/Purpur、Fabric、導入済みForge/NeoForgeやMODパックを、サーバーごとのプロファイルで管理します。
MIT License。Minecraft、個々のMODパック、playit.gg、Discordの公式製品ではありません。

**開発プレビューです。現在のリリースは未署名です。** Smart App Controlが有効なPCでは起動できない場合があります。
保護機能の無効化や回避を行う機能はありません。署名の審査・取得は未完了です。
SignPathへの申請は行っておらず、現時点では申請を予定していません。

## 機能

- サーバーの起動、保存停止、再起動。playitエージェントの起動・停止。
- 複数サーバーの登録・切替・別ウィンドウでの同時起動。データ、Java、起動方法、ポート、公開先はサーバーごとに分離。
- CPU、プロセスRAM、TPS、MSPT、人数、稼働時間、ディスク空き、履歴グラフ、CSV。
- 専用PC向けのメモリ予算計算。OSと使用中のメモリを残し、ヒープ最大量を起動時に決定。CPU・メモリ・MOD構成に応じた距離とJavaオプションの自動設定（[詳細](docs/PERFORMANCE.md)）。
- コンソール、ログ検索、Minecraft／公開接続／管理のログフィルター、参加許可、OP、キック、BAN、告知。
- 基本設定、完全バックアップ・復元、同梱FTB Backupsの設定、予約再起動。
- 左ナビ、文字ラベル付きアイコン、文字拡大、高コントラスト、キーボード操作。
- Discordへコピーできる参加案内。任意のWebhook通知（初期状態は無効、URLはDPAPI保存）。
- サーバーと公開接続を独立した常駐プロセスで実行。管理画面を閉じて再度開いても、同じプロセスへ再接続します。
- 直接ダウンロードURLからのサーバー用ZIP取り込み。SHA-256検証、進捗、中止、入れ子フォルダーの認識、Create: Astralの入力例。
- playit.gg・ngrok・直接公開・任意のポート開放ツールをサーバーごとに選択。

## ダウンロード

**[v0.4.0-preview の CraftHarbor-Setup-0.4.0-preview.exe](https://github.com/g-kari/craftharbor/releases/download/v0.4.0-preview/CraftHarbor-Setup-0.4.0-preview.exe)** · [リリースとポータブル版](https://github.com/g-kari/craftharbor/releases/tag/v0.4.0-preview)

灯台・港の新UIと24種類のアイコンを追加しました。[変更前後の比較](https://github.com/g-kari/craftharbor/blob/main/docs/UI_COMPARISON.md)で確認できます。

## インストールと継続運転

`CraftHarbor-Setup-0.4.0-preview.exe`はこのWindowsユーザーの`%LOCALAPPDATA%/Programs/CraftHarbor`へインストールし、スタートメニューへショートカットを追加します。デスクトップへの追加は任意です。管理者権限は不要です。

管理画面を閉じても、`CraftHarbor.Host.exe`がMinecraftサーバーと管理下の公開接続を維持します。サーバーを止めるときは「保存して停止」、トンネルを止めるときは公開接続の「停止」を選んでください。PCを停止するとプロセスは終了します。開発版では「運用・予約」からWindows起動後の自動復帰を登録できます。サインイン前に保存済みサーバーと公開接続を起動し、管理画面は後から再接続できます（[設定・制限](docs/BOOT_RECOVERY.md)）。スリープ解除は起動トリガーの対象外です。予約操作とDiscord通知は管理画面の起動中に実行します。

アプリ本体・常駐プロセスの更新やアンインストール前には、サーバーと公開接続を停止し、管理画面を閉じてください。旧版が直接起動したサーバーを、新方式へ稼働途中に移す機能はありません。

## URLからサーバーを追加

「サーバー一覧」→「URLから追加」で、直接ダウンロードできるHTTPSのサーバー用ZIPと新しい保存先を指定します。Minecraft EULAを確認してチェックするとダウンロード・検証・展開へ進みます。既存の保存先は上書きせず、一時フォルダーで完了してから新規追加します。

CurseForgeの紹介ページURL、クライアント用manifest ZIPや`.mrpack`を直接サーバーへ変換する機能はありません。配布サイトの**Server Pack**を選んでください。Forge/NeoForgeなどは、配布元の初期セットアップが完了したJARまたは`libraries/.../win_args.txt`が必要です。ZIP内のスクリプトは自動実行しません。

Create: Astral 2.1.5bの入力例は公式GitHubのサーバーZIPを固定SHA-256で検証し、不足するVinery 1.1.4を正規配布元から追加検証します。Java 17が必要です。これは入力例であり、ほかのパックも取り込めます。

## 公開方法と音声MOD

ダッシュボードの公開接続の「設定」から選択します。

| 方法 | 主催者側の準備 | 音声UDP |
|---|---|---|
| playit.gg | 公式エージェント・連携・トンネル作成。作成済み接続先はAPIで自動取得 | 音声ポートに対応する別トンネルが必要 |
| ngrok | 公式ngrok.exeと認証設定。TCP利用条件はngrok側を確認 | ngrok TCPでは音声UDPを中継しません |
| 直接公開 | グローバルに接続できる回線、ルーターのポート転送、必要な待受設定 | UDPも転送 |
| 外部ポート開放ツール | 利用者が入手した開放くん等のexeを指定。ツールの画面で設定 | ツール・ルーター側で設定 |

ツール起動はポート開放の成功を保証しません。本アプリはWindowsのセキュリティ設定やルーターを自動変更しません。ngrok無料TCPにはカード確認が必要です。[ngrok公式FAQ](https://ngrok.com/faq)

Simple Voice Chat・Plasmo VoiceのMODと設定からUDPポートを検出します。playitで作成済みの対応UDPトンネルをポート別に取得し、自動反映を有効にしている場合は停止中・次回起動前に各MODへ反映します。設定は変更前のコピーを残します。読み取り専用の連携キーではトンネル新規作成はできないため、公式の作成画面を開きます。ゲームのTCPが接続できても音声UDPが接続できるとは限りません。[Simple Voice Chat公式](https://modrepo.de/minecraft/voicechat/wiki/server_setup)、[Plasmo Voice公式](https://plasmovoice.com/docs/server/advanced/)

## 状態と制限

現在はMinecraft Java Edition向けです。必要なJava版・サーバー版・ランチャーは各プロファイルで設定します。Bedrockは対象外です。
JAR起動とJava引数ファイル起動に対応します。全サーバー実装の完全互換を保証するものではありません。
0.3.1-previewのインストール版で、Java 17のCreate: Astralサーバー起動・実測TPS/MSPT・保存停止・再起動を確認しました。管理画面の終了・再表示を挟んでも同じゲームとplayitのプロセスが継続し、公開アドレスのMinecraftステータス応答を維持しました。移行後のプレイヤーログインは再検証していません。
実機での確認状況はリリースノートを参照してください。Discord Webhookの実送信は未検証です。
CIのチェックは計算・検証ロジックと合成データを使うUI生成が対象で、実サーバーの接続やスクリーンリーダー対応を保証しません。
CIの画面プレビューは空の合成ワークスペースを使ったもので、プレイヤーの本物のワールドや稼働中サーバーの画面ではありません。

## 必要なもの

- Windows 10/11 x64 と .NET Framework 4.8。
- 対象サーバーが必要とするJava。プロファイルから実行ファイルを指定できます。
- ユーザー自身が正規の配布元から入手・導入したMinecraft Javaサーバーまたはサーバーパック。
- 外部公開する場合はplayitの公式Windows x64 CLI（1.0系と従来の0.17.1に対応）。別途アカウントとMinecraft Javaトンネルが必要。
- Minecraft EULAへの同意はユーザー自身で行ってください。

Minecraft、Java、MOD、ワールド、playitのバイナリは、このリポジトリにも管理アプリの配布ZIPにも含まれません。

## ワークスペース

既存サーバーを、以下の構成で用意してください。

```text
my-server/
  server/
    server.jar
    server.properties
    mods/             # MODを使う場合のみ
    plugins/          # プラグインを使う場合のみ
    ...サーバー実装が必要とするファイル...
  playit/
    playit-0.17.1.exe
  java-path.txt       # 任意: java.exeの絶対パスを1行で指定
```

管理アプリを起動して、`server`の親フォルダーを選びます。アプリと同じフォルダーに上記構成があれば自動検出します。
`CraftHarbor.exe --workspace "D:\Minecraft\my-server"` でも指定できます。
サーバー未導入時は選択をキャンセルし、READMEに従って準備してください。

初期値のポートは25565です。実際のポートはserver.propertiesから読み取り、プロファイル画面から変更できます。
playitを使う場合、Minecraft Javaトンネルをそのサーバーのローカル接続先に設定し、発行されたアドレスを保存します。
友達のクライアントをそのプロファイルのバージョン・MOD構成に揃えてください。Vanillaの参加案内にはMOD導入を要求しません。
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
Start-Process dist/CraftHarbor.exe -ArgumentList --core-check -Wait
Get-Content dist/core-check-report.txt
```

Smart App ControlがブロックするPCではチェック用引数でも実行できません。代替ホストでブロックされたEXEを実行する方法は案内しません。

## 操作

Alt+1〜9で画面切替、Ctrl+Lでコンソール入力、F1で参加者向け案内へ移動します。
自動メモリは「全RAMを直ちに予約する」方式ではありません。Vanillaは初期2GiB、MOD・プラグイン構成は初期4GiBから、起動時に算出した上限までJVMが利用します。
登録サーバーが複数なら予算を分け、稼働中の他サーバーがまだ使っていないヒープ予約分も残します。
登録追加後の再配分には各サーバーの再起動が必要です。最大割当済みのサーバーを動かしたまま、別サーバーへ同じRAMを二重に割り当てることはしません。
完全復元は停止中だけ実行し、変更前のサーバーは`recovery/`へ保管します。
予約再起動などの管理アプリ側の機能は、アプリ起動中のみ実行されます。

## Privacy / Code signing policy

- [Privacy policy](PRIVACY.md)
- [Code signing policy](CODE_SIGNING_POLICY.md)
- [SignPath preparation](docs/SIGNPATH.md)
- [Server profiles and multiple servers](docs/PROFILES.md)
- [Security reporting](SECURITY.md)

## アンインストール

「保存して停止」でサーバーを停止し、公開接続も停止して管理画面を閉じます。インストール版は`CraftHarbor-Uninstall.exe`またはWindowsのインストール済みアプリから削除します。ポータブル版はアプリ本体の展開フォルダーを削除します。
ワールドを残す場合はワークスペースを削除しないでください。
保存したワークスペース選択情報は`%LOCALAPPDATA%/CraftHarbor/workspace.txt`です。
旧Astral Server Managerの`workspace.txt`と`servers.json`は、新しい保存先に同名ファイルがない場合だけコピーします。旧版を終了してから更新してください。ワールドやワークスペース内の設定はそのまま利用します。
Webhook設定は管理アプリの「送信先を解除」で消去できます。Discord側のWebhookはDiscord上で管理してください。

## 参考・外部サービス

- [Create: Astral](https://www.curseforge.com/minecraft/modpacks/create-astral)
- [Minecraft EULA](https://www.minecraft.net/eula)
- [playit agent](https://github.com/playit-cloud/playit-agent)
- [spark](https://spark.lucko.me/)
- [Discord webhooks](https://docs.discord.com/developers/resources/webhook)

## 参加・サポート

- [不具合・機能提案](https://github.com/g-kari/craftharbor/issues)
- [貢献方法](CONTRIBUTING.md)
- [行動規範](CODE_OF_CONDUCT.md)
- [セキュリティ報告](SECURITY.md)

メンテナーは[g-kari](https://github.com/g-kari)です。現在は開発プレビューで、全パック・全公開方法の実接続を保証していません。
