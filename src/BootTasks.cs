using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class CraftHarbor
{
    const string BootTaskPrefix = "CraftHarbor-Boot-";
    static string BootTaskName(string workspace, string sid)
    {
        return BootTaskPrefix + sid + "-" + WorkspaceMutexName(workspace).Substring("Local\\CraftHarborWorkspace-".Length);
    }
    static dynamic OpenTaskService()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
        service.Connect(); return service;
    }
    static void ReleaseTaskObject(object value)
    {
        if(value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
    static dynamic MakeBootDefinition(dynamic service, string host, string workspace, string sid, int delay)
    {
        dynamic definition = service.NewTask(0);
        definition.RegistrationInfo.Description = "CraftHarbor: saved server recovery before Windows sign-in. Managed from the Operations page.";
        definition.RegistrationInfo.Author = "CraftHarbor";
        definition.Principal.UserId = sid;
        definition.Principal.LogonType = 1; // Password/batch logon: network and per-user encrypted settings are available.
        definition.Principal.RunLevel = 0; // LUA/least privilege, even though registration requires elevation.
        dynamic trigger = definition.Triggers.Create(8); // TASK_TRIGGER_BOOT; never a registration trigger.
        trigger.Delay = "PT" + Math.Max(0, Math.Min(600, delay)) + "S";
        definition.Settings.MultipleInstances = 2; // IgnoreNew
        definition.Settings.ExecutionTimeLimit = "PT0S";
        definition.Settings.DisallowStartIfOnBatteries = false;
        definition.Settings.StopIfGoingOnBatteries = false;
        definition.Settings.StartWhenAvailable = true;
        definition.Settings.AllowHardTerminate = false;
        definition.Settings.Enabled = true;
        dynamic action = definition.Actions.Create(0);
        action.Path = Path.GetFullPath(host);
        action.Arguments = "--boot-start --workspace " + Q(CanonicalWorkspace(workspace)) + " --owner-sid " + Q(sid);
        action.WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(host));
        return definition;
    }
    static dynamic FindBootTask(dynamic folder, string name)
    {
        try { return folder.GetTask(name); }
        catch(COMException error) {
            if(error.ErrorCode == unchecked((int)0x80070002) || error.ErrorCode == unchecked((int)0x8004130F)) return null;
            throw;
        }
    }
    static string CurrentSid() { return WindowsIdentity.GetCurrent().User.Value; }
    static bool IsAdministrator() { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
    static string BootHostExecutable { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CraftHarbor.Host.exe"); } }
    static bool OwnedBootTask(dynamic task, string sid, string host)
    {
        if(task == null || !((string)task.Name).StartsWith(BootTaskPrefix + sid + "-",StringComparison.Ordinal)) return false;
        dynamic definition = task.Definition;
        if((int)definition.Actions.Count != 1) return false;
        dynamic action = definition.Actions[1];
        return (int)action.Type == 0 && string.Equals((string)action.Path,host,StringComparison.OrdinalIgnoreCase);
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct BootCredentialInfo
    {
        public int Size; public IntPtr Parent; public string Message,Caption; public IntPtr Banner;
    }
    [DllImport("credui.dll",CharSet=CharSet.Unicode)]
    static extern int CredUIPromptForCredentialsW(ref BootCredentialInfo info,string target,IntPtr reserved,int error,
        StringBuilder user,int userSize,StringBuilder password,int passwordSize,[MarshalAs(UnmanagedType.Bool)] ref bool save,int flags);
    static bool RegisterBootWithCredential(dynamic folder,dynamic definition,string taskName,string ownerName,string sid)
    {
        var info = new BootCredentialInfo { Size=Marshal.SizeOf(typeof(BootCredentialInfo)),Caption="CraftHarbor 自動復帰",
            Message="サインイン前にサーバーを起動するため、このWindowsアカウントのパスワードを入力してください（PINではありません）。保存はWindowsのタスクスケジューラが行います。" };
        var user = new StringBuilder(ownerName,514); var password = new StringBuilder(257); bool save=false;
        try {
            int result=CredUIPromptForCredentialsW(ref info,"CraftHarbor Windows boot task",IntPtr.Zero,0,user,user.Capacity,password,password.Capacity,ref save,
                0x2|0x80|0x40000|0x100000|0x8); // DoNotPersist, AlwaysShowUI, Generic, KeepUsername, ExcludeCertificates.
            if(result==1223)return false;
            if(result!=0)throw new Win32Exception(result);
            var entered = (SecurityIdentifier)new NTAccount(user.ToString()).Translate(typeof(SecurityIdentifier));
            if(entered.Value!=sid)throw new InvalidOperationException("CraftHarborを使用しているWindowsアカウントを指定してください。");
            if(password.Length==0)throw new InvalidOperationException("サインイン前の自動復帰にはWindowsアカウントのパスワードが必要です。");
            // The password is passed only to Windows COM. Never write it to settings, arguments or logs.
            dynamic registered=folder.RegisterTaskDefinition(taskName,definition,6,ownerName,password.ToString(),1,
                "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;"+sid+")");
            ReleaseTaskObject(registered);return true;
        } finally { for(int i=0;i<password.Length;i++)password[i]='\0';password.Clear(); }
    }
    static void SaveBootOptions(string workspace,BootOptions options)
    {
        AtomicManagerWrite(BootOptionsPath(workspace),new JavaScriptSerializer().Serialize(options));
    }
    static int ConfigureBootTask(string[] args)
    {
        dynamic service=null,folder=null,definition=null,existing=null;
        try {
            string sid=BootArgument(args,"--owner-sid");
            if(!BootOwnerMatches(sid))throw new InvalidOperationException("元のWindowsアカウントで登録してください。別の管理者アカウントへの切り替えには対応していません。");
            if(!IsAdministrator())throw new InvalidOperationException("Windows起動タスクの登録・解除には管理者の承認が必要です。");
            string workspace=CanonicalWorkspace(BootArgument(args,"--workspace"));
            if(!ValidWorkspace(workspace))throw new IOException("保存済みサーバーのフォルダーを確認してください。");
            bool enable=Array.IndexOf(args,"--enable")>=0;
            service=OpenTaskService();folder=service.GetFolder("\\");
            string name=BootTaskName(workspace,sid);
            existing=FindBootTask(folder,name);
            if(existing!=null&&!OwnedBootTask(existing,sid,BootHostExecutable))
                throw new InvalidOperationException("同名の起動タスクは別のインストール先で使用されています。そのアプリから解除してください。");
            if(!enable) {
                var disabled=ReadBootJson<BootOptions>(BootOptionsPath(workspace))??new BootOptions();
                disabled.Enabled=false;SaveBootOptions(workspace,disabled);
                if(existing!=null)folder.DeleteTask(name,0);
                return 0; // Removing the registration does not stop the running server.
            }
            if(!File.Exists(BootHostExecutable))throw new FileNotFoundException("CraftHarbor.Host.exeが見つかりません。");
            int delay;
            if(!int.TryParse(BootArgument(args,"--delay"),out delay)||delay<0||delay>600)throw new ArgumentException("起動待機は0〜600秒で指定してください。");
            bool connect=Array.IndexOf(args,"--connect")>=0;
            var connection=ReadBootJson<HostingOptions>(Path.Combine(workspace,"connection-settings.json"));
            if(connect&&(connection==null||connection.Provider=="port-tool"))
                throw new InvalidOperationException("画面操作が必要なポート開放ツールはサインイン前に起動できません。公開接続の自動起動を外してください。");
            definition=MakeBootDefinition(service,BootHostExecutable,workspace,sid,delay);
            if(!RegisterBootWithCredential(folder,definition,name,WindowsIdentity.GetCurrent().Name,sid))return 1223;
            SaveBootOptions(workspace,new BootOptions { Enabled=true,Connect=connect,DelaySeconds=delay,OwnerSid=sid });
            return 0;
        } catch(Exception error) {
            // COM errors can originate in credential validation; keep provider details out of messages/logs.
            string message=error is COMException ? "Windowsの起動タスクを登録できませんでした。パスワード、バッチログオン権限、タスクスケジューラを確認してください。コード: 0x"+error.HResult.ToString("X8") : error.Message;
            MessageBox.Show(message,"CraftHarbor 自動復帰",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
        } finally { ReleaseTaskObject(existing);ReleaseTaskObject(definition);ReleaseTaskObject(folder);ReleaseTaskObject(service); }
    }
    static List<string> InstalledBootTasks(dynamic folder,string sid)
    {
        var names=new List<string>();dynamic tasks=folder.GetTasks(1);
        try { foreach(dynamic task in tasks)try { if(OwnedBootTask(task,sid,BootHostExecutable))names.Add((string)task.Name); }finally{ReleaseTaskObject(task);} }
        finally{ReleaseTaskObject(tasks);}return names;
    }
    static int RemoveInstalledBootTasks()
    {
        dynamic service=null,folder=null;
        try {
            service=OpenTaskService();folder=service.GetFolder("\\");string sid=CurrentSid();
            var names=InstalledBootTasks(folder,sid);if(names.Count==0)return 0;
            // Per-task DACL grants this owner deletion, so uninstall does not need the account password.
            foreach(string name in names)folder.DeleteTask(name,0);return 0;
        }catch{return 1;}finally{ReleaseTaskObject(folder);ReleaseTaskObject(service);}
    }
    string DescribeBootTask()
    {
        dynamic service=null,folder=null,task=null;
        try {
            var options=ReadBootJson<BootOptions>(BootOptionsPath(root))??new BootOptions();
            service=OpenTaskService();folder=service.GetFolder("\\");task=FindBootTask(folder,BootTaskName(root,CurrentSid()));
            string status=task==null?"未登録":!OwnedBootTask(task,CurrentSid(),BootHostExecutable)?"別のインストール先で登録されています":
                !options.Enabled||!(bool)task.Enabled?"無効":"有効 — サインイン前に起動 / "+options.DelaySeconds+"秒待機";
            if(task!=null)status+="\r\nWindows実行結果: 0x"+((int)task.LastTaskResult).ToString("X8");
            var report=ReadBootJson<BootReport>(Path.Combine(root,"boot-status.json"));
            if(report!=null&&report.UpdatedUtc.Length>0) {
                DateTime updated;string time=DateTime.TryParse(report.UpdatedUtc,out updated)?updated.ToLocalTime().ToString("g"):report.UpdatedUtc;
                status+="\r\n最終記録 ("+time+"): "+report.Message;
            }
            return status;
        } catch { return "Windows起動タスクの状態を取得できません。タスクスケジューラを確認してください。"; }
        finally { ReleaseTaskObject(task);ReleaseTaskObject(folder);ReleaseTaskObject(service); }
    }
    void AddBootRecoveryControls(FlowLayoutPanel parent)
    {
        parent.Controls.Add(new Label{Text="Windows起動後の自動復帰",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(3,24,3,10)});
        parent.Controls.Add(new Label{Text="このサーバーをサインイン前から起動します。登録時に管理者の承認とWindowsパスワードが必要です。\r\n実行は通常権限です。画面を閉じても稼働を続けます。登録しても今すぐ起動・再起動はしません。",
            AutoSize=true,MaximumSize=new Size(740,0),ForeColor=Muted});
        BootOptions options;try{options=ReadBootJson<BootOptions>(BootOptionsPath(root))??new BootOptions();}catch{options=new BootOptions();}
        var publicConnection=CheckSetting(parent,"公開接続も起動する（playit.gg / ngrok / 直接公開）");publicConnection.Checked=options.Connect;
        var waitRow=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
        waitRow.Controls.Add(new Label{Text="Windows起動からの待機（秒）",AutoSize=true,Margin=new Padding(3,8,10,3)});
        var delay=new HarborNumber{Minimum=0,Maximum=600,Value=Math.Max(0,Math.Min(600,options.DelaySeconds)),Width=100,AccessibleName="Windows起動後の待機秒数"};
        waitRow.Controls.Add(delay);parent.Controls.Add(waitRow);
        var state=new Label{AutoSize=true,MaximumSize=new Size(740,0),AccessibleName="Windows自動復帰の登録状態",Text="状態を取得しています…"};
        var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true,MaximumSize=new Size(740,0)};
        Action refresh=delegate{state.Text=DescribeBootTask();};
        Action<bool> configure=delegate(bool enable) {
            if(enable) { SaveSettings();SaveFeatureSettings(); }
            string args="--configure-boot-task --workspace "+Q(root)+" --owner-sid "+Q(CurrentSid())+
                (enable?" --enable --delay "+((int)delay.Value)+(publicConnection.Checked?" --connect":""):" --disable");
            try { using(var process=Process.Start(new ProcessStartInfo(Application.ExecutablePath,args){UseShellExecute=true,Verb="runas"})) {
                process.WaitForExit();
                if(process.ExitCode==0)Add(enable?"[自動復帰] 次回のWindows起動から有効です。":"[自動復帰] 次回のWindows起動から無効です。");
                else if(process.ExitCode!=1223)Add("[自動復帰] 登録を変更できませんでした。");
            }}catch(Win32Exception error){if(error.NativeErrorCode!=1223)throw;}
            refresh();
        };
        FlowBtn(actions,"登録・更新",delegate{configure(true);});
        FlowBtn(actions,"登録を解除",delegate{configure(false);});
        FlowBtn(actions,"状態を確認",delegate{refresh();});
        FlowBtn(actions,"今回の自動復帰を中断",delegate{HostCall(new HostRequest{Action="cancel-boot"});Add("[自動復帰] 起動準備・再試行を中断しました。稼働中のサーバーは「保存して停止」で止められます。");refresh();});
        parent.Controls.Add(actions);parent.Controls.Add(state);
        parent.Controls.Add(new Label{Text="停止操作は次のWindows起動まで維持します。パスワードを変更したら登録を更新してください。\r\nJava・サーバー・公開接続は先に手動で導入・連携してください。スリープ復帰は対象外です。",AutoSize=true,MaximumSize=new Size(740,0),ForeColor=Muted});
        // Query only when shown, after normal startup; the synthetic UI check does not register any task.
        state.VisibleChanged+=delegate{if(state.Visible)refresh();};
    }
}
