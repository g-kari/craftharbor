# 起動時の自動調整

設定はサーバーごとに保存します。稼働中の距離やJVMは変更せず、次回起動の直前にメモリの空き容量を再確認して反映します。

## メモリと距離

メモリ上限は物理メモリ、現在の空き容量、登録サーバー数、他の稼働サーバーの予約分を考慮します。OS用の予算を残します。

距離の自動設定は、論理CPU数を登録数で割った目安、実際に割り当てるヒープ上限、MOD／プラグインの有無、想定する同時接続人数から決めます。人数の目安は最大参加人数を変更しません。

| 構成 | 描画距離の範囲 | シミュレーション距離の範囲 |
| --- | --- | --- |
| Vanilla | 6～12 chunks | 4～8 chunks |
| MOD・プラグインあり | 6～10 chunks | 4～6 chunks |

大容量RAMだけを理由に距離を増やしません。これは保守的な初期値のヒューリスティックで、CPUの世代差、単一スレッド性能、MODの処理、拠点の規模をベンチマークした最適値ではありません。TPS／MSPTを確認し、必要なら自動設定をオフにして手動指定してください。自動と手動を切り替えても、保存済みの手動距離は保持します。

シミュレーション距離はJava 1.18以降で使用します。バージョンが不明な場合は、元のserver.propertiesにその設定があるときだけ変更します。変更前のファイルをserver.properties.before-distance.bakに保存します。無関係な設定、認証、参加許可、ワールドは変更しません。

## Java

起動前に、選択したjava.exeを別プロセスの`-XshowSettings:properties -version`で確認します。64bit版Java 8以上が必要です。Minecraftが要求するJavaバージョンの選択はプロファイル側で行ってください。

- `-Xmx`：共有メモリ予算で決定。
- `-Xms`：Vanillaは最大2 GiB、MOD・プラグイン構成は最大4 GiBから開始し、上限まで必要に応じて拡張。
- HotSpot系VM：G1、停止時間の目標200ms、参照処理の並列化を指定。細部はJVMの自動調整に任せる。
- 複数登録時：Java 10以降はActiveProcessorCount、Java 8／9はGCスレッド数を登録数に応じて抑える。CPU使用率の強制制限やCPUコアの専有ではない。
- 他のVM：HotSpot固有のGCフラグを付けず、VM標準のGCを使用。

自動をオフにすると、`-XX:...`と`-Xss...`の手動オプションを指定できます。ヒープサイズはメモリ設定から変更してください。起動先や引数ファイルを手動オプションで置き換えることはできません。

GCオプションは小さなヒープの別の`-version`プロセスで受理されるか検証してから使用します。この確認はMODの互換性やゲームの性能を保証しません。200msはGC停止時間の目標値で、達成を保証する値ではありません。

## グラフ

CPU／プロセスRAMは1秒ごとに取得し、直近5分・最大300点を表示します。データの追加・削除のたびに時間軸とRAM軸を再計算します。最初の1点はマーカーを表示し、停止中・未取得の状態も明示します。アプリを閉じている間の履歴は取得しません。

参考：[Minecraft Java 1.18の変更](https://feedback.minecraft.net/hc/en-us/articles/4415128577293-Minecraft-Java-Edition-1-18)、[Java 17の起動オプション](https://docs.oracle.com/en/java/javase/17/docs/specs/man/java.html)、[Microsoft ChartArea](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.datavisualization.charting.chartarea?view=netframework-4.8.1)。距離の表はCraftHarbor独自の初期設定方針です。
