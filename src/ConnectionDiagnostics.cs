using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class ConnectionCheck { public string Stage="",State="",Detail="",Next=""; }
class MinecraftStatus { public bool Responded;public string Version="",Failure="";public int Players=-1; }

partial class CraftHarbor
{
    // A response observed on the host is deliberately not called an Internet reachability test.
    static bool DiagnosticEndpoint(string text,out string host,out int port)
    {
        host="";port=25565;Uri uri;
        if(!ValidPublicEndpoint(text)||!Uri.TryCreate("tcp://"+text,UriKind.Absolute,out uri)||uri.Host.Length==0||uri.UserInfo.Length>0)return false;
        if(uri.Port!=-1)port=uri.Port;if(port<1||port>65535)return false;host=uri.Host;return true;
    }
    static byte[] DiagnosticVarInt(int value)
    {
        var bytes=new List<byte>();uint n=(uint)value;do{byte b=(byte)(n&127);n>>=7;if(n!=0)b|=128;bytes.Add(b);}while(n!=0);return bytes.ToArray();
    }
    static void PrepareDiagnosticRead(Stream stream,DateTime deadline,CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();if(deadline==default(DateTime))return;
        double left=(deadline-DateTime.UtcNow).TotalMilliseconds;if(left<=0)throw new TimeoutException();
        if(stream.CanTimeout)stream.ReadTimeout=Math.Max(1,(int)Math.Min(2500,left));
    }
    static int ReadDiagnosticVarInt(Stream stream,DateTime deadline=default(DateTime),CancellationToken cancel=default(CancellationToken))
    {
        int value=0;for(int n=0;n<5;n++){PrepareDiagnosticRead(stream,deadline,cancel);int b=stream.ReadByte();if(b<0)throw new EndOfStreamException("ステータス応答が途中で終了しました。");if(n==4&&(b&0xf0)!=0)throw new InvalidDataException("ステータス応答の長さが不正です。");value|=(b&127)<<(n*7);if((b&128)==0)return value;}throw new InvalidDataException("ステータス応答の長さが不正です。");
    }
    static MinecraftStatus ReadDiagnosticStatus(Stream stream,DateTime deadline=default(DateTime),CancellationToken cancel=default(CancellationToken))
    {
        const int limit=1048576;int length=ReadDiagnosticVarInt(stream,deadline,cancel);if(length<2||length>limit)throw new InvalidDataException("ステータス応答が大きすぎるか不正です。");
        byte[] bytes=new byte[length];int offset=0;while(offset<length){PrepareDiagnosticRead(stream,deadline,cancel);int count=stream.Read(bytes,offset,length-offset);if(count==0)throw new EndOfStreamException("ステータス応答が途中で終了しました。");offset+=count;}
        using(var packet=new MemoryStream(bytes)){
            if(ReadDiagnosticVarInt(packet)!=0)throw new InvalidDataException("Minecraftステータス応答ではありません。");int size=ReadDiagnosticVarInt(packet);if(size<2||size>limit||size!=packet.Length-packet.Position)throw new InvalidDataException("ステータスJSONの長さが不正です。");
            var payload=new byte[size];packet.Read(payload,0,size);var json=new JavaScriptSerializer{MaxJsonLength=limit,RecursionLimit=32}.Deserialize<Dictionary<string,object>>(new UTF8Encoding(false,true).GetString(payload));
            object versionObject,playersObject;var result=new MinecraftStatus();
            var version=json!=null&&json.TryGetValue("version",out versionObject)?versionObject as Dictionary<string,object>:null;
            var players=json!=null&&json.TryGetValue("players",out playersObject)?playersObject as Dictionary<string,object>:null;
            if(version==null||players==null)throw new InvalidDataException("Minecraftの版・人数情報を確認できません。");
            result.Version=System.Text.RegularExpressions.Regex.Replace(StringValue(version,"name"),@"[\x00-\x1f\x7f]","");if(result.Version.Length>128)result.Version="不明";int online;if(!int.TryParse(StringValue(players,"online"),out online)||online<0)throw new InvalidDataException("Minecraftの人数情報が不正です。");
            result.Responded=true;result.Players=online;return result;
        }
    }
    static MinecraftStatus ProbeMinecraftStatus(string host,int port,CancellationToken cancel=default(CancellationToken))
    {
        try{
            using(var client=new TcpClient())using(cancel.Register(delegate{client.Close();})){
                cancel.ThrowIfCancellationRequested();
                var connecting=client.BeginConnect(host,port,null,null);using(connecting.AsyncWaitHandle){int completed=WaitHandle.WaitAny(new[]{connecting.AsyncWaitHandle,cancel.WaitHandle},2500);cancel.ThrowIfCancellationRequested();if(completed==WaitHandle.WaitTimeout)throw new TimeoutException();client.EndConnect(connecting);}
                client.ReceiveTimeout=2500;client.SendTimeout=2500;using(var stream=client.GetStream())using(var handshake=new MemoryStream()){
                    handshake.WriteByte(0);byte[] protocol=DiagnosticVarInt(47),name=Encoding.UTF8.GetBytes(host),nameLength=DiagnosticVarInt(name.Length);handshake.Write(protocol,0,protocol.Length);handshake.Write(nameLength,0,nameLength.Length);handshake.Write(name,0,name.Length);handshake.WriteByte((byte)(port>>8));handshake.WriteByte((byte)port);handshake.WriteByte(1);
                    byte[] packet=handshake.ToArray(),length=DiagnosticVarInt(packet.Length);stream.Write(length,0,length.Length);stream.Write(packet,0,packet.Length);stream.Write(new byte[]{1,0},0,2);return ReadDiagnosticStatus(stream,DateTime.UtcNow.AddSeconds(5),cancel);
                }
            }
        }catch(Exception error){cancel.ThrowIfCancellationRequested();return new MinecraftStatus{Failure=error is TimeoutException?"応答待ちがタイムアウトしました。":error is SocketException?"TCPへ接続できません。":error is IOException?"TCP接続後にMinecraft応答を確認できません。":"Minecraftステータス応答を確認できません。"};}
    }
    static ConnectionCheck DiagnosticGameRow(string stage,MinecraftStatus status,bool local)
    {
        return new ConnectionCheck{Stage=stage,State=status.Responded?"応答あり":"未確認",Detail=status.Responded?"Minecraft応答をこのPCから確認。版 "+status.Version+" / 人数 "+status.Players+"人。"+(local?"":"外部参加者からの到達性の証明ではありません。"):status.Failure,
            Next=status.Responded?(local?"続けて公開接続先・参加許可・クライアント構成を確認。":"別回線の参加者に同じ接続先で確認してもらう。"):(local?"起動完了、server-port/server-ip、Javaとサーバーログを確認。":"公開先とローカル転送先、プロバイダーの状態を確認。防御設定は自動変更しません。")};
    }
    static string FormatConnectionChecks(IEnumerable<ConnectionCheck> checks)
    {
        var text=new StringBuilder("接続診断（読み取り専用）\r\nローカルの観測と外部到達性は別です。設定変更・トンネル作成・ログの外部送信はしません。\r\n");
        foreach(var row in checks)text.AppendLine().AppendLine(row.Stage+" — "+row.State).AppendLine(row.Detail).AppendLine("次に: "+row.Next);return text.ToString();
    }
    void ShowConnectionDiagnostics(bool snapshot=false)
    {
        using(var lifetime=new CancellationTokenSource())
        using(var dialog=new Form{Text="友達が入れないときの接続診断",Size=new Size(880,720),MinimumSize=new Size(640,480),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font}){
            dialog.FormClosing+=delegate{lifetime.Cancel();};
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=2,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));dialog.Controls.Add(layout);
            var report=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Card,ForeColor=Ink,Text="「診断を実行」で、設定とJava、ローカル/公開アドレスのMinecraftステータス応答を確認します。\r\n音声UDPの外部到達性や参加許可の個人別確認は断定しません。",AccessibleName="接続診断の段階と次の操作"};layout.Controls.Add(report,0,0);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};layout.Controls.Add(actions,0,1);Button run=null;
            run=FlowBtn(actions,"診断を実行",async delegate{
                run.Enabled=false;report.Text="Java・接続先を読み取り専用で確認しています…";
                try{
                string java=JavaPath(),endpoint=address.Text.Trim(),provider=hosting.Provider,properties=File.ReadAllText(Path.Combine(ServerDir,"server.properties"));int port=ServerPort;bool active=Running(server),isReady=ready,publicActive=Running(tunnel);var voices=VoiceEndpoints(ServerDir,port);var voiceMap=new Dictionary<string,string>(hosting.VoiceAddresses??new Dictionary<string,string>());string fallbackVoice=hosting.VoiceAddress;var cancel=lifetime.Token;
                    var checks=await Task.Run(delegate{
                        cancel.ThrowIfCancellationRequested();var rows=new List<ConnectionCheck>();try{var runtime=ParseJavaRuntime(RunJavaProbe(java,"-XshowSettings:properties -version"));rows.Add(new ConnectionCheck{Stage="1. Java",State="確認",Detail="64bit Java "+runtime.Major+"。パックが要求する版とは照合が必要です。",Next="配布元の必要Java版とプロファイルを確認。"});}catch(Exception){rows.Add(new ConnectionCheck{Stage="1. Java",State="未確認",Detail="設定済みJavaの版を確認できません。",Next="サーバー設定のJavaパス・必要版を確認。"});}
                        cancel.ThrowIfCancellationRequested();rows.Add(new ConnectionCheck{Stage="2. サーバープロセス",State=active?(isReady?"起動完了":"準備中"):"停止中",Detail="このワークスペースの常駐ホストによる観測。",Next=active?(isReady?"ローカル応答へ進む。":"起動ログの完了を待つ。停止・再起動は自動実行しません。 "):"主催者がサーバーを起動し、完了を待つ。"});
                        rows.Add(DiagnosticGameRow("3. ローカルTCP / Minecraft :"+port,ProbeMinecraftStatus("127.0.0.1",port,cancel),true));
                        rows.Add(new ConnectionCheck{Stage="4. 公開接続",State=provider=="direct"||provider=="port-tool"?"外部設定は未確認":publicActive?"プロセスあり":"プロセスなし",Detail="公開方法: "+provider+"。ツールの起動は転送成功を保証しません。",Next="作成済みトンネルのゲームTCPポートとローカル転送先を確認。"});
                        string host;int remotePort;if(DiagnosticEndpoint(endpoint,out host,out remotePort))rows.Add(DiagnosticGameRow("5. 公開先（このPCから）",ProbeMinecraftStatus(host,remotePort,cancel),false));else rows.Add(new ConnectionCheck{Stage="5. 公開先",State="未設定 / 不正",Detail="ゲーム接続先をhost:portで確認してください。",Next="既存プロバイダーで発行されたゲームTCP接続先を保存。"});
                        rows.Add(new ConnectionCheck{Stage="6. 参加許可・クライアント",State="個別確認が必要",Detail="white-list="+DiagnosticProperty(properties,"white-list","false")+" / online-mode="+DiagnosticProperty(properties,"online-mode","true")+"。個人名や許可リストは共有しません。",Next="主催者がユーザー名・参加許可を確認。ゲーム版とMOD構成は「参加者MOD照合」で確認。認証を無効化しない。"});
                        foreach(var voice in voices){string mapped;if(!voiceMap.TryGetValue(voice.Port.ToString(),out mapped))mapped=voices.Select(v=>v.Port).Distinct().Count()==1?fallbackVoice:"";bool listening=false;try{listening=IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(e=>e.Port==voice.Port);}catch(NetworkInformationException){}
                            rows.Add(new ConnectionCheck{Stage="7. "+voice.Name+" / UDP "+voice.Port,State="外部到達性は不明",Detail=(listening?"同ポートのUDP待受あり（所有プロセスは未確認）。":"UDP待受を確認できません。無応答だけでは障害と断定できません。")+(provider=="ngrok"?"ngrok TCPは音声UDPを中継しません。":VoiceAddressValid(mapped)?"対応する公開先の保存あり。":"対応する音声UDP公開先は未設定。"),Next="ゲームTCPとは別に音声UDPの転送先/ポートを確認し、別回線の参加者と音声接続を試す。"});}
                        if(voices.Count==0)rows.Add(new ConnectionCheck{Stage="7. 音声UDP",State="検出なし",Detail="対応する音声MODと設定の組を検出しませんでした。",Next="音声を使う場合はMODの公式設定手順を確認。"});return rows;
                    });
                    if(!dialog.IsDisposed)report.Text=FormatConnectionChecks(checks);
                }catch(Exception){if(!dialog.IsDisposed)report.Text="診断中に情報が変わったか、読み取りできませんでした。設定を確認して再試行してください。";}finally{if(!run.IsDisposed)run.Enabled=true;}
            });FlowBtn(actions,"閉じる",delegate{dialog.Close();});StyleControls(dialog);ThemeDialog(dialog);if(snapshot)CaptureOperationsDialog(dialog,"ui-connection-diagnostics.png",delegate{report.Text=FormatConnectionChecks(new[]{DiagnosticGameRow("ローカルTCP / Minecraft",new MinecraftStatus{Responded=true,Players=0},true),DiagnosticGameRow("公開先（このPCから）",new MinecraftStatus{Responded=true,Players=0},false),new ConnectionCheck{Stage="音声UDP",State="外部到達性は不明",Detail="合成fixture: ゲームTCP応答は音声UDPの成功を保証しません。",Next="UDP設定と別回線からの確認。"}});});dialog.ShowDialog(this);
        }
    }
    static string DiagnosticProperty(string properties,string key,string fallback)
    {
        var match=System.Text.RegularExpressions.Regex.Match(properties,@"(?m)^\s*"+System.Text.RegularExpressions.Regex.Escape(key)+@"\s*=\s*([^\r\n]*)");return match.Success?match.Groups[1].Value.Trim():fallback;
    }
    static void RunConnectionDiagnosticChecks()
    {
        string host;int port;Require(DiagnosticEndpoint("test.example.invalid:25570",out host,out port)&&port==25570,"diagnostic endpoint port");Require(!DiagnosticEndpoint("test.invalid:65536",out host,out port)&&!DiagnosticEndpoint("https://example.invalid",out host,out port)&&!DiagnosticEndpoint("user:pass@example.invalid",out host,out port),"diagnostic endpoint rejects URI and invalid port");
        string json="{\"version\":{\"name\":\"Fixture\",\"protocol\":47},\"players\":{\"online\":2,\"max\":20},\"description\":\"private MOTD\"}";byte[] payload=Encoding.UTF8.GetBytes(json);var body=new List<byte>{0};body.AddRange(DiagnosticVarInt(payload.Length));body.AddRange(payload);var packet=new List<byte>(DiagnosticVarInt(body.Count));packet.AddRange(body);using(var input=new MemoryStream(packet.ToArray())){var result=ReadDiagnosticStatus(input);Require(result.Responded&&result.Players==2&&result.Version=="Fixture","bounded Minecraft status parser");}
        foreach(byte[] bad in new[]{new byte[]{255,255,255,255,127},new byte[]{3,0,4,123},new byte[]{1,0}}){bool rejected=false;try{using(var input=new MemoryStream(bad))ReadDiagnosticStatus(input);}catch(IOException){rejected=true;}catch(InvalidDataException){rejected=true;}Require(rejected,"truncated / oversized Minecraft response rejected");}
        bool timedOut=false;try{using(var input=new MemoryStream(packet.ToArray()))ReadDiagnosticStatus(input,DateTime.UtcNow.AddSeconds(-1));}catch(TimeoutException){timedOut=true;}Require(timedOut,"diagnostic response has a total deadline, not only a per-read timeout");
        bool cancelled=false;using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{using(var input=new MemoryStream(packet.ToArray()))ReadDiagnosticStatus(input,default(DateTime),cancel.Token);}catch(OperationCanceledException){cancelled=true;}}Require(cancelled,"closed diagnostic cancels packet reads");
        string report=FormatConnectionChecks(new[]{DiagnosticGameRow("public",new MinecraftStatus{Responded=true,Players=0},false)});Require(report.Contains("外部参加者からの到達性の証明ではありません")&&!report.Contains("private MOTD"),"public probe does not claim external reachability or expose MOTD");
    }
}
