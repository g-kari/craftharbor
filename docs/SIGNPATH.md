# SignPath申請準備

## 費用と適用範囲

[SignPath Foundation](https://signpath.org/)は対象OSS向けの署名支援を無料で提供しています。
MITライセンス、保守、リリース、機能説明、ソースからの検証可能なビルド、MFA、役割分担などが条件です。
新規アプリの実績も審査対象であり、公開すれば直ちに受理される制度ではありません。

## このリポジトリで準備したもの

- 管理アプリだけのMITライセンスソース。
- ユーザーのデータ・認証情報・Minecraft/Java/MOD/playitのバイナリを含めない配布物。
- 固定リビジョンのGitHub Actionsを使ったWindowsビルド・コアチェック・ZIP生成。
- 製品名・バージョンのアセンブリ属性。
- Privacy policy / Code signing policy / アンインストール方法。
- 明示的な未署名プレビュー表記。

## まだ必要なこと

1. GitHub側のビルド結果を確認し、ユーザー自身が変更内容をレビューする。
2. 公開プレビューリリースを用意する。公開後の利用・保守実績を積む。
3. GitHubのMFAと、参加時のSignPathのMFAを有効にする。
4. [Apply](https://signpath.org/apply.html)から本人が申請する。審査・契約・アカウント同意は本人が行う。
5. 受理後、SignPath GitHub連携・組織ID・プロジェクト・ポリシー・承認者を設定する。
6. 実際の設定値が確定してから署名ワークフローを追加する。APIトークンはGitHub Actions Secretsに保存する。
7. 手動承認された署名済み成果物をダウンロードし、署名チェーンとタイムスタンプを検証してから配布する。

ローカルでSmart App Controlにブロックされる未署名EXEを、別のホストやスクリプトへ読み込ませて回避する手順は用意していません。
現在は無料署名の受理・実署名・この開発PCでの新ビルド起動のいずれも未完了です。

## 申請時の説明案（未送信）

Project: Astral Server Manager

This is an MIT-licensed Windows desktop manager for a user-provided Create: Astral Minecraft server workspace.
It provides local lifecycle controls, resource/tick metrics, console access, backup and recovery, and optional operator-configured Discord webhook notifications.
The signed artifact would contain only our own application and icon, not Minecraft, Java, mods, playit binaries, user data, or credentials.
The project is an early preview and has not yet established a broad user base. We would like to understand whether it is eligible for your program and what further evidence or project maturity would be needed.

## 参考

- https://signpath.org/terms.html
- https://signpath.org/apply.html
- https://learn.microsoft.com/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control
