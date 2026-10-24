using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class HostingOptions { public string Provider="playit",AgentExe="",NgrokExe="",PortToolExe="",VoiceAddress="";public bool AutoVoice=true;public Dictionary<string,string> VoiceAddresses=new Dictionary<string,string>(); }
class VoiceEndpoint { public string Name,Config;public int Port; }
class PlayitSnapshot { public string Agent="",Game="",Voice="";public Dictionary<int,string> VoiceMap=new Dictionary<int,string>();public string Status{get{return Game.Length>0?"公開接続先を取得済み":"連携済み・ゲーム用トンネルが未作成";}} }
partial class CraftHarbor
{
    HostingOptions hosting=new HostingOptions();
    Label hostingSummary;
    string playitAgentId="",discoveredGame="",discoveredVoice="",connectionStatus="未確認";
    int hostingWorker;
    DateTime lastHostingPoll=DateTime.MinValue;
    string HostingFile{get{return Path.Combine(root,"connection-settings.json");}}
    string HostingName{get{return hosting.Provider=="ngrok"?"ngrok":hosting.Provider=="direct"?"直接公開":hosting.Provider=="port-tool"?"ポート開放ツール":"playit.gg";}}
    void LoadHosting(){if(File.Exists(HostingFile))try{hosting=new JavaScriptSerializer().Deserialize<HostingOptions>(File.ReadAllText(HostingFile))??new HostingOptions();}catch(Exception e){Add("[公開設定] "+e.Message);}hosting.Provider=hosting.Provider??"playit";}
    static Dictionary<string,object> ObjectValue(object value){return value as Dictionary<string,object>??new Dictionary<string,object>();}
    static object Field(Dictionary<string,object> obj,string key){object value;return obj.TryGetValue(key,out value)?value:null;}
    static string StringValue(Dictionary<string,object> obj,string key){return Convert.ToString(Field(obj,key));}
    static IEnumerable<Dictionary<string,object>> Objects(object value){var array=value as IEnumerable;if(array==null)yield break;foreach(var item in array){var obj=item as Dictionary<string,object>;if(obj!=null)yield return obj;}}
    Dictionary<string,object> PlayitCall(string endpoint,object body)
    {
        string file=Path.Combine(PlayitDir,"agent-secret.toml");if(!File.Exists(file))throw new InvalidOperationException("playitのアカウント連携が必要です。");string raw=File.ReadAllText(file);var match=Regex.Match(raw,@"(?m)^\s*secret_key\s*=\s*""([^""]+)""");if(!match.Success)throw new InvalidDataException("playitの連携ファイルを読み取れません。");
        var request=(HttpWebRequest)WebRequest.Create("https://api.playit.gg/v1/"+endpoint);request.Method="POST";request.AllowAutoRedirect=false;request.Timeout=10000;request.ReadWriteTimeout=10000;request.ContentType="application/json";request.Headers["Authorization"]="Agent-Key "+match.Groups[1].Value;byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));request.ContentLength=data.Length;using(var stream=request.GetRequestStream())stream.Write(data,0,data.Length);
        using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){string json=reader.ReadToEnd();if(json.Length>2097152)throw new InvalidDataException("playitからの応答が大きすぎます。");var result=new JavaScriptSerializer{MaxJsonLength=2097152}.Deserialize<Dictionary<string,object>>(json);if(StringValue(result,"status")!="success")throw new InvalidOperationException("playitの設定を確認してください。");return ObjectValue(Field(result,"data"));}
    }
    List<VoiceEndpoint> DetectVoice(){return VoiceEndpoints(ServerDir,ServerPort);}
    PlayitSnapshot ReadPlayitTunnels()
    {
        var data=PlayitCall("agents/rundata",new{});var result=new PlayitSnapshot{Agent=StringValue(data,"agent_id")};var detected=DetectVoice();int gamePort=ServerPort;
        foreach(var item in Objects(Field(data,"tunnels"))){if(StringValue(item,"disabled_reason").Length>0)continue;var fields=Objects(Field(ObjectValue(Field(item,"agent_config")),"fields")).ToArray();string ip=fields.Where(f=>StringValue(f,"name")=="local_ip").Select(f=>StringValue(f,"value")).FirstOrDefault();if(ip!="127.0.0.1"&&ip!="localhost"&&ip!="::1")continue;
            string p=fields.Where(f=>StringValue(f,"name")=="local_port").Select(f=>StringValue(f,"value")).FirstOrDefault();int port;int.TryParse(p,out port);string protocol=StringValue(item,"port_type"),type=StringValue(item,"tunnel_type"),endpoint=StringValue(item,"display_address");if(!ValidPublicEndpoint(endpoint))continue;
            if((port==gamePort||(port==0&&gamePort==25565))&&(protocol=="tcp"||protocol=="both")&&(type=="minecraft-java"||type.Length==0))result.Game=endpoint;
            if(detected.Any(v=>v.Port==port)&&(protocol=="udp"||protocol=="both")){result.Voice=endpoint;result.VoiceMap[port]=endpoint;}}
        return result;
    }
    void AcceptPlayitSnapshot(PlayitSnapshot result){playitAgentId=result.Agent;discoveredVoiceMap=result.VoiceMap;discoveredGame=result.Game;discoveredVoice=result.Voice;connectionStatus=result.Status;}
    static string HostingFailure(Exception error){if(error is WebException)return "playitに接続できません。ネットワークとアカウント連携を確認して再試行してください。";if(error is InvalidOperationException||error is InvalidDataException)return error.Message;return "playitの連携・トンネル設定を確認して再試行してください。";}
    void RefreshHosting(bool force=false)
    {
        if(hosting.Provider!="playit"||(!force&&(DateTime.UtcNow-lastHostingPoll).TotalSeconds<30)||Interlocked.CompareExchange(ref hostingWorker,1,0)!=0)return;lastHostingPoll=DateTime.UtcNow;
        Task.Run(delegate{try{var result=ReadPlayitTunnels();if(hosting.Provider=="playit")AcceptPlayitSnapshot(result);}catch(Exception error){if(hosting.Provider=="playit")connectionStatus=HostingFailure(error);}finally{Interlocked.Exchange(ref hostingWorker,0);}});
    }
    static bool ValidPublicEndpoint(string endpoint){if(string.IsNullOrWhiteSpace(endpoint)||endpoint.Length>253)return false;return Regex.IsMatch(endpoint,@"^(?:[A-Za-z0-9][A-Za-z0-9.-]*)(?::[0-9]{1,5})?$");}
    void ApplyDiscoveredAddresses(){if(hosting.VoiceAddresses==null)hosting.VoiceAddresses=new Dictionary<string,string>();bool changed=false;foreach(var pair in discoveredVoiceMap){string key=pair.Key.ToString();string existing;if(!hosting.VoiceAddresses.TryGetValue(key,out existing)||existing!=pair.Value){hosting.VoiceAddresses[key]=pair.Value;changed=true;}}if(changed)File.WriteAllText(HostingFile,new JavaScriptSerializer().Serialize(hosting),new UTF8Encoding(false));if(hosting.AutoVoice&&!Running(server))ApplyVoiceAutomatically();if(discoveredGame.Length>0&&address.Text!=discoveredGame){address.Text=discoveredGame;SaveSettings();RefreshGuide();Add("[公開先] ゲーム用接続先を取得しました。");}if(discoveredVoice.Length>0&&hosting.VoiceAddress!=discoveredVoice){hosting.VoiceAddress=discoveredVoice;File.WriteAllText(HostingFile,new JavaScriptSerializer().Serialize(hosting),new UTF8Encoding(false));Add("[音声] UDPトンネルの公開接続先を取得しました。停止中に公開方法の画面からMODへ反映できます。");}if(hostingSummary!=null)hostingSummary.Text=HostingName+" / "+connectionStatus;}
    void StartHosting()
    {
        if(Running(tunnel))return;if(hosting.Provider=="direct"){Add("[公開方法] 直接公開を選択中です。保存した公開アドレスとルーターの転送設定を確認してください。");return;}
        if(hosting.Provider=="port-tool"){if(!File.Exists(hosting.PortToolExe)||!hosting.PortToolExe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new Exception("ポート開放ツールのexeを公開方法の設定から選択してください。");Process.Start(new ProcessStartInfo(hosting.PortToolExe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(hosting.PortToolExe)});Add("[公開方法] 指定したポート開放ツールを開きました。MinecraftのTCP "+ServerPort+" と音声用UDPを設定してください。");return;}
        if(hosting.Provider=="ngrok"){if(!File.Exists(hosting.NgrokExe))throw new Exception("ngrok.exeを指定し、公式手順でアカウント認証を設定してください。");tunnel=LaunchPersistent("tunnel",hosting.NgrokExe,"tcp 127.0.0.1:"+ServerPort+" --log=stdout --log-format=json",Path.GetDirectoryName(hosting.NgrokExe),0);return;}
        string agent=hosting.AgentExe;if(string.IsNullOrWhiteSpace(agent))agent=File.Exists(Path.Combine(PlayitDir,"playit.exe"))?Path.Combine(PlayitDir,"playit.exe"):Path.Combine(PlayitDir,"playit-0.17.1.exe");if(!File.Exists(agent))throw new Exception("playitの公式エージェントを公開方法の設定から指定してください。");
        bool legacy=Path.GetFileName(agent).Contains("0.17");string args=legacy?"--stdout --secret_path ":"--secret-path ";args+=Q(Path.Combine(PlayitDir,"agent-secret.toml"));if(!legacy)args+=" --socket-path "+Q(@"\\.\pipe\CraftHarbor-playit-"+WorkspaceMutexName(root).Substring(6));tunnel=LaunchPersistent("tunnel",agent,args,Path.GetDirectoryName(agent),0);RefreshHosting(true);
    }
    void HostingSettings(bool snapshot=false)
    {
        using(var dialog=new HarborDialog{Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath),Text="公開方法・音声チャット",Size=new Size(980,830),MinimumSize=new Size(860,700),BackColor=Bg,ForeColor=Ink,Font=new Font("Yu Gothic UI",12),StartPosition=FormStartPosition.CenterParent}){
            var form=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24)};dialog.Controls.Add(form);form.Controls.Add(new Label{Text="このサーバーの公開方法",Font=new Font("Yu Gothic UI",20,FontStyle.Bold),AutoSize=true});
            var provider=ChoiceSetting(form,"接続方法",new[]{"playit.gg","ngrok","直接公開（ポート転送）","外部ポート開放ツール"});provider.Width=570;provider.SelectedIndex=hosting.Provider=="ngrok"?1:hosting.Provider=="direct"?2:hosting.Provider=="port-tool"?3:0;
            var explanation=new Label{AutoSize=true,MaximumSize=new Size(880,0),ForeColor=Muted,Margin=new Padding(0,12,0,18)};form.Controls.Add(explanation);Action describe=delegate{explanation.Text=provider.SelectedIndex==0?"ルーターでのポート開放は不要です。公式エージェントとアカウント連携・公開トンネルが必要です。接続先は自動取得します。":provider.SelectedIndex==1?"公式ngrokエージェントのTCP接続を開始します。アカウント認証はngrok側で設定してください。無料TCPの利用にはカード確認が必要です。UDP音声は別の公開方法が必要です。":"ルーターのポート転送と、外部から接続できるネットワークが必要です。サーバーの待受を外部向けに変更する場合は下のチェックを選んで保存してください。";};provider.SelectedIndexChanged+=delegate{describe();};describe();
            var agent=TextSetting(form,"playit.exe",570);agent.Text=hosting.AgentExe;Control agentRow=agent.Parent;
            var ngrok=TextSetting(form,"ngrok.exe",570);ngrok.Text=hosting.NgrokExe;Control ngrokRow=ngrok.Parent;
            var tool=TextSetting(form,"開放くん等のexe",570);tool.Text=hosting.PortToolExe;Control toolRow=tool.Parent;
            var buttons=new FlowLayoutPanel{AutoSize=true};form.Controls.Add(buttons);
            var browseAgent=FlowBtn(buttons,"実行ファイルを選択",delegate{TextBox input=provider.SelectedIndex==0?agent:provider.SelectedIndex==1?ngrok:tool;using(var picker=new OpenFileDialog{Filter="実行ファイル|*.exe",CheckFileExists=true})if(picker.ShowDialog(dialog)==DialogResult.OK)input.Text=picker.FileName;});
            var inlineStatus=new Label{Text="playit："+connectionStatus,AutoSize=true,MaximumSize=new Size(870,0),ForeColor=Accent,Margin=new Padding(0,8,0,12),AccessibleName="公開接続先の取得状態"};form.Controls.Add(inlineStatus);
            var external=new HarborCheckBox{Text="直接公開用に待受アドレスを0.0.0.0へ変更する（サーバー停止中のみ）",AutoSize=true,Margin=new Padding(0,16,0,10)};form.Controls.Add(external);
            var voice=DetectVoice();form.Controls.Add(new Label{Text="音声チャット",Font=new Font("Yu Gothic UI",15,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,18,0,8)});form.Controls.Add(new Label{Text=voice.Count==0?"音声MODの設定ファイルは未検出です。導入・初回起動後に再確認します。":string.Join("\n",voice.Select(v=>v.Name+" / UDP "+v.Port)),AutoSize=true,ForeColor=Muted,MaximumSize=new Size(870,0)});
            var autoVoice=new HarborCheckBox{Text="音声ポートと公開先を自動検出し、停止中・次回起動前にMODへ反映する",AutoSize=true,Checked=hosting.AutoVoice};form.Controls.Add(autoVoice);var voiceAddress=TextSetting(form,"音声の公開接続先",570);voiceAddress.Text=hosting.VoiceAddress;form.Controls.Add(new Label{Text="Simple Voice Chatは別のUDPトンネルが必要です。読み取り専用のplayit連携では新規作成は公式画面で行い、作成後のアドレスを自動取得します。\n「MODへ反映」は音声の公開先を変更し、変更前の設定を.bakへ保存します。反映は停止中に行ってください。",AutoSize=true,MaximumSize=new Size(870,0),ForeColor=Muted});
            var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true,MaximumSize=new Size(870,0),Margin=new Padding(0,18,0,0)};form.Controls.Add(actions);
            FlowBtn(actions,"設定を保存",delegate{if(Running(tunnel))throw new Exception("公開接続を停止してから接続方法を変更してください。");string endpoint=voiceAddress.Text.Trim();if(endpoint.Length>0&&!VoiceAddressValid(endpoint))throw new Exception("音声接続先をhost:portの形式で確認してください。");var next=new HostingOptions{Provider=new[]{"playit","ngrok","direct","port-tool"}[provider.SelectedIndex],AgentExe=agent.Text.Trim(),NgrokExe=ngrok.Text.Trim(),PortToolExe=tool.Text.Trim(),VoiceAddress=endpoint,AutoVoice=autoVoice.Checked,VoiceAddresses=hosting.VoiceAddresses??new Dictionary<string,string>()};discoveredGame="";discoveredVoice="";discoveredVoiceMap=new Dictionary<int,string>();lastHostingPoll=DateTime.MinValue;if(external.Checked){EnsureStopped();if(provider.SelectedIndex<2)throw new Exception("外部待受は直接公開・ポート開放方式で選択してください。");string props=Path.Combine(ServerDir,"server.properties"),text=File.ReadAllText(props);File.Copy(props,props+".before-public.bak",true);text=Regex.Replace(text,@"(?m)^server-ip=.*(?:\r?\n|$)","");File.WriteAllText(props,text.TrimEnd()+"\r\nserver-ip=0.0.0.0\r\n",new UTF8Encoding(false));}hosting=next;File.WriteAllText(HostingFile,new JavaScriptSerializer().Serialize(hosting),new UTF8Encoding(false));connectionStatus="保存済み";connect.AccessibleName=HostingName+"の接続を開始";disconnect.AccessibleName=HostingName+"の接続を停止";RefreshGuide();dialog.Close();});
            var createTunnel=FlowBtn(actions,"playitでトンネル作成",delegate{Open("https://playit.gg/account/setup/new-tunnel"+(playitAgentId.Length>0?"?agent_id="+playitAgentId:""));});Button refreshAddress=null;refreshAddress=FlowBtn(actions,"接続先を再取得",async delegate{refreshAddress.Enabled=false;inlineStatus.Text="playitの接続先を確認しています…";try{var result=await Task.Run(()=>ReadPlayitTunnels());if(dialog.IsDisposed)return;inlineStatus.Text=result.Status+(result.Game.Length>0?"："+result.Game:"");if(hosting.Provider=="playit")AcceptPlayitSnapshot(result);}catch(Exception error){if(!dialog.IsDisposed)inlineStatus.Text=HostingFailure(error);}finally{if(!refreshAddress.IsDisposed)refreshAddress.Enabled=true;}});var applyVoice=FlowBtn(actions,"音声設定をMODへ反映",delegate{EnsureStopped();string value=voiceAddress.Text.Trim();if(!VoiceAddressValid(value))throw new Exception("音声接続先をhost:portで入力してください。");if(voice.Select(v=>v.Port).Distinct().Count()>1)throw new Exception("音声ポートが複数あります。対応するトンネルの自動取得を使うか、各MODの設定を個別に確認してください。");foreach(var item in voice)WriteVoiceAddress(item,value);Add("[音声] 公開接続先を反映しました。次回起動に適用します。");});
            applyVoice.Enabled=voice.Count>0;Action updateFields=delegate{int selection=provider.SelectedIndex;agentRow.Visible=selection==0;ngrokRow.Visible=selection==1;toolRow.Visible=selection==3;browseAgent.Visible=selection!=2;external.Visible=selection>=2;if(selection<2)external.Checked=false;createTunnel.Visible=refreshAddress.Visible=inlineStatus.Visible=selection==0;};provider.SelectedIndexChanged+=delegate{updateFields();};updateFields();
            StyleControls(form);ThemeDialog(dialog);if(snapshot)dialog.Shown+=delegate{dialog.BeginInvoke((Action)delegate{for(int choice=0;choice<4;choice++){provider.SelectedIndex=choice;Application.DoEvents();Require(agentRow.Visible==(choice==0)&&ngrokRow.Visible==(choice==1)&&toolRow.Visible==(choice==3),"provider-specific fields");Require(createTunnel.Visible==(choice==0)&&refreshAddress.Visible==(choice==0),"provider-specific playit actions");using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"ui-hosting-"+choice+".png"),System.Drawing.Imaging.ImageFormat.Png);if(choice==0)image.Save(Path.Combine(root,"ui-hosting.png"),System.Drawing.Imaging.ImageFormat.Png);}}dialog.Close();});};dialog.ShowDialog(this);
        }
    }
}
