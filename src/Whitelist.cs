using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class WhitelistIdentity { public string Name="",Uuid=""; }
partial class CraftHarbor
{
    bool whitelistBusy;
    static void ValidatePlayerName(string name)
    {
        if(!Regex.IsMatch(name??"",@"^[A-Za-z0-9_]{1,16}$"))throw new ArgumentException("Java版ユーザー名を英数字と _ 、16文字以内で入力してください。");
    }
    bool OnlineAuthentication()
    {
        string value;return !ReadProperties().TryGetValue("online-mode",out value)||!value.Equals("false",StringComparison.OrdinalIgnoreCase);
    }
    static WhitelistIdentity ParseWhitelistIdentity(string json,string requested)
    {
        var row=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(json);string name,id;Guid uuid;
        if(row==null||!row.TryGetValue("name",out name)||!row.TryGetValue("id",out id)||!string.Equals(name,requested,StringComparison.OrdinalIgnoreCase)
            ||!Guid.TryParseExact(id,"N",out uuid)||uuid==Guid.Empty)throw new InvalidDataException("Minecraftのユーザー名とUUIDを確認できませんでした。");
        ValidatePlayerName(name);return new WhitelistIdentity{Name=name,Uuid=uuid.ToString("D")};
    }
    static WhitelistIdentity OfflineIdentity(string name)
    {
        ValidatePlayerName(name);
        byte[] bytes;using(var md5=MD5.Create())bytes=md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:"+name));
        bytes[6]=(byte)((bytes[6]&15)|48);bytes[8]=(byte)((bytes[8]&63)|128);
        string hex=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        return new WhitelistIdentity{Name=name,Uuid=Guid.ParseExact(hex,"N").ToString("D")};
    }
    static WhitelistIdentity ResolveWhitelistIdentity(string name,bool online)
    {
        ValidatePlayerName(name);if(!online)return OfflineIdentity(name);
        // One bounded HTTPS lookup. No login token, fallback identity or guessed online UUID.
        var request=(HttpWebRequest)WebRequest.Create("https://api.mojang.com/users/profiles/minecraft/"+Uri.EscapeDataString(name));
        request.Method="GET";request.Timeout=10000;request.ReadWriteTimeout=10000;request.AllowAutoRedirect=false;
        request.UserAgent="CraftHarbor";request.Accept="application/json";
        try {
            using(var response=(HttpWebResponse)request.GetResponse()){
                if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>16384)throw new InvalidDataException("Minecraftのユーザー情報を取得できませんでした。");
                using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){
                    var buffer=new char[16385];int total=0,read;
                    while(total<buffer.Length&&(read=reader.Read(buffer,total,buffer.Length-total))>0)total+=read;
                    if(total>16384)throw new InvalidDataException("ユーザー情報の応答が大きすぎます。");
                    return ParseWhitelistIdentity(new string(buffer,0,total),name);
                }
            }
        }catch(WebException){throw new IOException("Minecraftのユーザー情報を取得できません。名前・通信状態を確認して再試行してください。許可リストは変更していません。");}
    }
    static void EditStoppedWhitelist(string directory,string operation,string name,WhitelistIdentity identity)
    {
        ValidatePlayerName(name);if(operation!="add"&&operation!="remove")throw new ArgumentException("無効な参加許可操作です。");
        string path=Path.Combine(directory,"whitelist.json");
        var rows=File.Exists(path)?new JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(path)):new List<Dictionary<string,object>>();
        if(rows==null||rows.Any(row=>row==null))throw new InvalidDataException("whitelist.jsonを読み取れません。既存ファイルは変更していません。");
        if(operation=="add"){
            Guid id;if(identity==null||!string.Equals(identity.Name,name,StringComparison.OrdinalIgnoreCase)||!Guid.TryParse(identity.Uuid,out id)||id==Guid.Empty)throw new InvalidDataException("参加者のUUIDが無効です。");
            rows.RemoveAll(row=>string.Equals(StringValue(row,"name"),name,StringComparison.OrdinalIgnoreCase)||string.Equals(StringValue(row,"uuid"),identity.Uuid,StringComparison.OrdinalIgnoreCase));
            rows.Add(new Dictionary<string,object>{{"uuid",identity.Uuid},{"name",identity.Name}});
        }else rows.RemoveAll(row=>string.Equals(StringValue(row,"name"),name,StringComparison.OrdinalIgnoreCase));
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(rows),new UTF8Encoding(false));
        if(File.Exists(path))File.Replace(temp,path,path+".before-manager.bak");else File.Move(temp,path);
    }
    bool TryLiveWhitelist(string operation,string name)
    {
        HostReply state;
        try{state=HostCall(new HostRequest{Since=long.MaxValue});}catch(TimeoutException){return false;}catch(IOException){return false;}
        if(state.ServerPid==0)return false;
        if(!state.Ready||state.BootInitializing||stopping)throw new InvalidOperationException("サーバーの起動・停止処理が完了してから参加許可を変更してください。");
        HostCall(new HostRequest{Action="command",Text="whitelist "+operation+" "+name});
        Add("[参加許可] サーバーへ変更を送信しました。結果はコンソールで確認できます。");return true;
    }
    async void AllowPlayer(string operation)
    {
        if(whitelistBusy)return;
        string name=player.Text.Trim();if(name.Length==0&&usersGrid.SelectedRows.Count>0)name=Convert.ToString(usersGrid.SelectedRows[0].Cells[0].Value);
        try {
            ValidatePlayerName(name);if(TryLiveWhitelist(operation,name))return;
            whitelistBusy=true;bool online=OnlineAuthentication();
            Add(operation=="add"?"[参加許可] ユーザー情報を確認しています…":"[参加許可] 停止中の許可リストを更新します。");
            var identity=operation=="add"?await Task.Run(()=>ResolveWhitelistIdentity(name,online)):null;
            if(IsDisposed||Disposing)return;
            // A server may have started while the HTTPS lookup was in progress.
            if(TryLiveWhitelist(operation,name))return;
            using(var maintenance=WorkspaceMaintenanceLock(root)){
                EnsureStopped();
                if(OnlineAuthentication()!=online)throw new InvalidOperationException("認証設定が変わりました。もう一度操作してください。");
                EditStoppedWhitelist(ServerDir,operation,name,identity);
            }
            RefreshUsers();Add("[参加許可] "+name+" の"+(operation=="add"?"追加":"解除")+"を保存しました。次回起動で読み込まれます。");
        }catch(Exception error){Add("[参加許可] "+error.Message);if(!IsDisposed)MessageBox.Show(error.Message,"参加許可を変更できませんでした");}
        finally{whitelistBusy=false;}
    }
}
