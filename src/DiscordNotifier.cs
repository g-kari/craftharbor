using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class AstralManager
{
    TextBox webhookInput;
    CheckBox webhookEnabled,webhookLifecycle,webhookBackups;
    Label webhookStatus;
    volatile bool notificationsEnabled,notifyLifecycle,notifyBackups;
    volatile string activeWebhook="";
    volatile bool readyNotified;
    readonly ConcurrentQueue<string> discordQueue=new ConcurrentQueue<string>();
    int discordWorker;
    DateTime discordCloseDeadline=DateTime.MinValue;
    string DiscordConfig {get{return Path.Combine(root,"discord-settings.json");}}
    string DiscordSecret {get{return Path.Combine(root,"discord-webhook.dpapi");}}
    static string ValidateWebhook(string input)
    {
        Uri u;if(!Uri.TryCreate(input.Trim(),UriKind.Absolute,out u)||u.Scheme!="https"||!u.IsDefaultPort||u.Host!="discord.com"||u.UserInfo.Length>0||u.Query.Length>0||u.Fragment.Length>0||!Regex.IsMatch(u.AbsolutePath,@"^/api(?:/v\d+)?/webhooks/\d+/[A-Za-z0-9_-]+$"))throw new ArgumentException("discord.com の公式Webhook URLを入力してください。URLはDiscordのチャンネル設定→連携サービス→Webhookから取得できます。");return u.AbsoluteUri;
    }
    void AddDiscordPage()
    {
        var page=Page(workspace,"Discord通知");var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8)};page.Controls.Add(layout);
        layout.Controls.Add(new Label{Text="サーバーの状態をDiscordへ",AutoSize=true,Font=new Font("Yu Gothic UI",16,FontStyle.Bold)});
        layout.Controls.Add(new Label{Text="Discordの送信先チャンネルで作成したWebhookを登録します。URLはこのWindowsユーザー用に暗号化して保存します。",AutoSize=true,MaximumSize=new Size(830,0),ForeColor=Muted,Margin=new Padding(0,12,0,16)});
        layout.Controls.Add(new Label{Text="Webhook URL",AutoSize=true});webhookInput=new TextBox{Width=780,UseSystemPasswordChar=true,BackColor=Card,ForeColor=Ink,AccessibleName="DiscordのWebhook URL",AccessibleDescription="秘密の送信先URL。表示は伏せられ、暗号化して保存されます。"};layout.Controls.Add(webhookInput);
        webhookEnabled=CheckSetting(layout,"Discord通知を有効にする");webhookLifecycle=CheckSetting(layout,"起動完了・停止・異常終了を通知");webhookBackups=CheckSetting(layout,"完全バックアップ・復元の結果を通知");webhookLifecycle.Checked=webhookBackups.Checked=true;
        var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=new Padding(0,12,0,12)};
        FlowBtn(actions,"設定を保存",delegate{SaveDiscord();});FlowBtn(actions,"テスト送信",delegate{if(MessageBox.Show("設定したWebhookのDiscordチャンネルへテスト通知を1件送ります。","Discord通知",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;SaveDiscord();if(!notificationsEnabled)throw new Exception("通知を有効にして保存してください。");QueueDiscord("テスト通知","Astral Managerからの接続確認です。");});
        FlowBtn(actions,"送信先を解除",delegate{if(MessageBox.Show("このPCに保存したWebhook設定を解除します。Discord側のWebhook自体は残ります。","通知解除",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;notificationsEnabled=false;activeWebhook="";webhookEnabled.Checked=false;webhookInput.Clear();string ignored;while(discordQueue.TryDequeue(out ignored)){}if(File.Exists(DiscordSecret))File.Delete(DiscordSecret);SaveDiscordOptions();webhookStatus.Text="通知は無効です。";});layout.Controls.Add(actions);
        webhookStatus=new Label{Text="通知は無効です。",AutoSize=true,ForeColor=Accent,MaximumSize=new Size(820,0),AccessibleName="Discord通知の状態"};layout.Controls.Add(webhookStatus);
        layout.Controls.Add(new Label{Text="・初期状態は無効。定期通知や全員メンションは送りません。\n・通知内容はサーバー名と運用結果のみ。ログ全文、認証情報、参加者名は送信しません。\n・参加者向けの長い案内は「参加者向け案内」からコピーして投稿できます。\n・管理アプリが動作している間に通知します。",AutoSize=true,ForeColor=Muted,MaximumSize=new Size(820,0),Margin=new Padding(0,20,0,0)});
        LoadDiscord();
    }
    void SaveDiscordOptions(){var d=new Dictionary<string,object>{{"enabled",webhookEnabled.Checked},{"lifecycle",webhookLifecycle.Checked},{"backups",webhookBackups.Checked}};File.WriteAllText(DiscordConfig,new JavaScriptSerializer().Serialize(d));}
    void SaveDiscord()
    {
        string url=webhookInput.Text.Trim();if(url.Length>0)url=ValidateWebhook(url);if(webhookEnabled.Checked&&url.Length==0)throw new Exception("通知先のWebhook URLを入力してください。");
        if(url.Length>0){byte[] bytes=Encoding.UTF8.GetBytes(url);try{File.WriteAllBytes(DiscordSecret,ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));}finally{Array.Clear(bytes,0,bytes.Length);}}
        SaveDiscordOptions();activeWebhook=url;notificationsEnabled=webhookEnabled.Checked;notifyLifecycle=webhookLifecycle.Checked;notifyBackups=webhookBackups.Checked;webhookStatus.Text=notificationsEnabled?"保存済み。イベント発生時に通知します。":"保存済み。通知は無効です。";
    }
    void LoadDiscord()
    {
        try{
            if(File.Exists(DiscordSecret)){byte[] bytes=ProtectedData.Unprotect(File.ReadAllBytes(DiscordSecret),null,DataProtectionScope.CurrentUser);try{webhookInput.Text=ValidateWebhook(Encoding.UTF8.GetString(bytes));}finally{Array.Clear(bytes,0,bytes.Length);}}
            if(File.Exists(DiscordConfig)){var d=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(DiscordConfig));webhookEnabled.Checked=Convert.ToBoolean(d["enabled"]);webhookLifecycle.Checked=Convert.ToBoolean(d["lifecycle"]);webhookBackups.Checked=Convert.ToBoolean(d["backups"]);}
            activeWebhook=webhookInput.Text;notificationsEnabled=webhookEnabled.Checked&&activeWebhook.Length>0;notifyLifecycle=webhookLifecycle.Checked;notifyBackups=webhookBackups.Checked;webhookStatus.Text=notificationsEnabled?"設定済み。通知は有効です。":"通知は無効です。";
        }catch{notificationsEnabled=false;activeWebhook="";webhookEnabled.Checked=false;webhookInput.Clear();webhookStatus.Text="保存したURLを読み込めません。同じWindowsユーザーで開くか、URLを登録し直してください。";}
    }
    void NotifyDiscord(string category,string title,string description)
    {
        if(!notificationsEnabled||(category=="lifecycle"&&!notifyLifecycle)||(category=="backup"&&!notifyBackups))return;QueueDiscord(title,description);
    }
    static string DiscordPayload(string title,string description)
    {
        var data=new Dictionary<string,object>{{"username","Astral Manager"},{"content","🚀 **Create: Astral — "+title+"**\n"+description},{"allowed_mentions",new Dictionary<string,object>{{"parse",new string[0]}}}};return new JavaScriptSerializer().Serialize(data);
    }
    void QueueDiscord(string title,string description)
    {
        if(!notificationsEnabled||activeWebhook.Length==0)return;
        if(discordQueue.Count>=20){Add("[Discord] 通知が集中したため、追加の通知を省略しました。");return;}
        discordQueue.Enqueue(DiscordPayload(title,description));StartDiscordWorker();
    }
    void StartDiscordWorker()
    {
        if(Interlocked.CompareExchange(ref discordWorker,1,0)!=0)return;
        Task.Run(delegate{try{string payload;while(discordQueue.TryDequeue(out payload)){if(!notificationsEnabled||activeWebhook.Length==0)continue;string endpoint=activeWebhook;bool sent=false;
                    for(int attempt=0;attempt<2&&!sent;attempt++){
                        try{ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072;var req=(HttpWebRequest)WebRequest.Create(endpoint+"?wait=true");req.Method="POST";req.ContentType="application/json; charset=utf-8";req.AllowAutoRedirect=false;req.Timeout=10000;req.ReadWriteTimeout=10000;byte[] body=Encoding.UTF8.GetBytes(payload);req.ContentLength=body.Length;using(var stream=req.GetRequestStream())stream.Write(body,0,body.Length);using(var response=(HttpWebResponse)req.GetResponse()){if((int)response.StatusCode>=200&&(int)response.StatusCode<300)sent=true;}if(sent){Add("[Discord] 通知を送信しました。");SetDiscordStatus("通知を送信しました。");}}
                        catch(WebException e){var response=e.Response as HttpWebResponse;int status=response==null?0:(int)response.StatusCode;if(status==429&&attempt==0){double delay=2;if(response!=null){double value;if(double.TryParse(response.Headers["Retry-After"],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))delay=Math.Max(1,Math.Min(30,value));response.Dispose();}Thread.Sleep((int)(delay*1000));continue;}if(response!=null)response.Dispose();Add("[Discord] 送信失敗（"+(status==0?"通信エラー":status.ToString())+"）。設定を確認してください。");SetDiscordStatus("送信に失敗しました。Webhookの設定と接続を確認してください。");break;}
                        catch{Add("[Discord] 通知を送信できませんでした。");SetDiscordStatus("通知を送信できませんでした。");break;}
                    }Thread.Sleep(1000);
                }}finally{Interlocked.Exchange(ref discordWorker,0);if(!discordQueue.IsEmpty)StartDiscordWorker();}});
    }
    void SetDiscordStatus(string text){if(!IsDisposed&&IsHandleCreated)try{BeginInvoke((Action)delegate{webhookStatus.Text=text;});}catch(InvalidOperationException){}}
    bool DiscordDrainedForExit()
    {
        if(Volatile.Read(ref discordWorker)==0&&discordQueue.IsEmpty)return true;
        if(discordCloseDeadline==DateTime.MinValue)discordCloseDeadline=DateTime.UtcNow.AddSeconds(20);
        return DateTime.UtcNow>=discordCloseDeadline;
    }
}
