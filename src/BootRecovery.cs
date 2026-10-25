using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

class BootOptions
{
    public bool Enabled, Connect = true;
    public int DelaySeconds = 30;
    public string OwnerSid = "";
}
class BootReport
{
    public string Phase = "", Message = "", UpdatedUtc = "";
}
partial class CraftHarbor
{
    static volatile bool bootManaged, bootInitializing, bootCancelled, bootTunnelCancelled;
    static FileStream WorkspaceMaintenanceLock(string workspace)
    {
        try { return new FileStream(Path.Combine(workspace,".craftharbor-maintenance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
        catch(IOException) { throw new IOException("バックアップ・復元・起動準備の完了を待ってください。"); }
    }
    void CheckBootPreparation()
    {
        if(!File.Exists(BootOptionsPath(root)))return;
        try {
            var status=HostCall(new HostRequest{Since=long.MaxValue});
            if(status.BootInitializing||status.ServerPid>0)throw new InvalidOperationException("自動復帰の準備中、またはサーバーが稼働中です。準備の中断・サーバー停止後に操作してください。");
        }catch(TimeoutException){}catch(IOException){}
    }
    static string BootOptionsPath(string workspace) { return Path.Combine(workspace, "boot-options.json"); }
    static T ReadBootJson<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new T();
        return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path));
    }
    static void WriteBootReport(string workspace, string phase, string message)
    {
        HostLog("[自動復帰] " + message);
        try {
            string path = Path.Combine(workspace, "boot-status.json");
            var report = new BootReport { Phase = phase, Message = message, UpdatedUtc = DateTime.UtcNow.ToString("o") };
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    static string BootArgument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
    }
    static bool BootOwnerMatches(string owner)
    {
        return !string.IsNullOrEmpty(owner) && owner == WindowsIdentity.GetCurrent().User.Value;
    }
    static Dictionary<string,string> BootProperties(string directory)
    {
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(Path.Combine(directory, "server.properties"))) {
            int equals = line.IndexOf('=');
            if (equals > 0 && !line.TrimStart().StartsWith("#")) result[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
        }
        return result;
    }
    static int BootPort(string directory)
    {
        string text; int port;
        return BootProperties(directory).TryGetValue("server-port", out text) && int.TryParse(text, out port) && port > 0 && port < 65536 ? port : 25565;
    }
    static void CheckBootPort(string directory)
    {
        using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
            listener.ExclusiveAddressUse = true;
            listener.Bind(new IPEndPoint(IPAddress.Loopback, BootPort(directory)));
        }
    }
    static bool BootModded(string directory, ServerProfile selected)
    {
        return selected.ClientMode == "modpack" || new[] { "mods", "plugins" }.Any(d =>
            Directory.Exists(Path.Combine(directory, d)) && Directory.GetFiles(Path.Combine(directory, d), "*.jar").Length > 0);
    }
    static HostRequest BuildBootServerRequest(string workspace)
    {
        string directory = Path.Combine(workspace, "server");
        if (!ValidWorkspace(workspace)) throw new IOException("保存済みサーバーのフォルダーを確認してください。初回セットアップは自動実行しません。");
        string eula = Path.Combine(directory, "eula.txt");
        if (!File.Exists(eula) || !File.ReadAllLines(eula).Any(l => l.Trim().Equals("eula=true", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Minecraft EULAへの同意をサーバー側で確認してください。");
        CheckBootPort(directory);
        var selected = ReadBootJson<ServerProfile>(Path.Combine(workspace, "manager-profile.json"));
        if (selected == null) throw new InvalidDataException("サーバープロファイルを読み取れません。");
        string target = ProfileLaunchTarget(directory, selected);
        var feature = ReadBootJson<Dictionary<string,object>>(Path.Combine(workspace, "manager-options.json"));
        object automaticValue;
        bool automatic = feature == null || !feature.TryGetValue("autoMemory", out automaticValue) || Convert.ToBoolean(automaticValue);
        int safe = SharedBudgetFor(workspace, !automatic), heap = safe;
        string settings = Path.Combine(workspace, "manager-settings.txt");
        if (!automatic) {
            heap = 6; int configured;
            if (File.Exists(settings) && int.TryParse(File.ReadLines(settings).FirstOrDefault(), out configured)) heap = configured;
        }
        if (heap < 4 || heap > safe) throw new InvalidOperationException("他サーバーとOSの予約分を残すとメモリが足りません。");
        string javaFile = Path.Combine(workspace, "java-path.txt");
        string java = File.Exists(javaFile) ? File.ReadAllText(javaFile).Trim() : "java.exe";
        if (java.Length == 0) java = "java.exe";
        var options = ReadBootJson<JavaOptions>(Path.Combine(workspace, "java-options.json"));
        if (options == null) throw new InvalidDataException("Java設定を読み取れません。");
        var runtime = ParseJavaRuntime(RunJavaProbe(java, "-XshowSettings:properties -version"));
        bool modded = BootModded(directory, selected);
        int count = RegisteredWorkspaceCount();
        var plan = PlanJava(runtime, heap, Environment.ProcessorCount, count, modded, options.Automatic, options.Custom);
        RunJavaProbe(java, plan.Tuning + " -Xms32m -Xmx64m -version");
        CheckBootPort(directory);
        var distances = ReadBootJson<DistanceOptions>(Path.Combine(workspace, "performance-settings.json"));
        if (distances == null) throw new InvalidDataException("距離設定を読み取れません。");
        ApplyStartupDistances(directory, selected.MinecraftVersion, heap, count, modded, distances);
        var connection = ReadBootJson<HostingOptions>(Path.Combine(workspace, "connection-settings.json"));
        if (connection == null) throw new InvalidDataException("公開接続の設定を読み取れません。");
        ApplyAutomaticVoice(directory, BootPort(directory), connection);
        HostLog("[Java設定] " + plan.Reason);
        return new HostRequest { Action = "start", BootRequest = true, Kind = "server", Exe = java, Arguments = plan.Arguments + " " + target, Directory = directory, Heap = heap };
    }
    static void RunBootRecovery(string workspace)
    {
        try {
            var options = ReadBootJson<BootOptions>(BootOptionsPath(workspace));
            if (options == null || !options.Enabled || !BootOwnerMatches(options.OwnerSid)) {
                WriteBootReport(workspace, "disabled", "このWindowsユーザーの自動復帰は無効です。"); return;
            }
            RegisterWorkspace(workspace);
            for (int attempt = 1; attempt <= 3 && !hostExit && !bootCancelled; attempt++) {
                try {
                    WriteBootReport(workspace, "starting", "保存済みプロファイルから自動復帰しています（" + attempt + "/3）。");
                    using (var gate = new Mutex(false, MemoryGateName())) {
                        bool acquired = false;
                        try {
                            try { acquired = gate.WaitOne(TimeSpan.FromSeconds(90)); } catch (AbandonedMutexException) { acquired = true; }
                            if (!acquired) throw new IOException("別サーバーのメモリ割り当て完了を待っています。");
                            if (hostExit || bootCancelled) return;
                            if (!Running(hostedServer)) {
                                using(var maintenance=WorkspaceMaintenanceLock(workspace)) {
                                var request = BuildBootServerRequest(workspace);
                                if (hostExit || bootCancelled) return;
                                ExecuteHost(request, workspace);
                                try { ReserveWorkspaceMemory(workspace, hostedServer, request.Heap); }
                                catch { ExecuteHost(new HostRequest { Action = "command", Text = "stop" }, workspace); throw; }
                                }
                            }
                        } finally { if (acquired) gate.ReleaseMutex(); }
                    }
                    DateTime deadline = DateTime.UtcNow.AddMinutes(10);
                    while (!hostedReady && Running(hostedServer) && !hostExit && !bootCancelled && DateTime.UtcNow < deadline) Thread.Sleep(250);
                    if (hostExit || bootCancelled) { WriteBootReport(workspace, "stopped", "利用者の停止操作を優先しました。"); return; }
                    if (!Running(hostedServer)) throw new IOException("サーバーが起動完了前に終了しました。");
                    if (!hostedReady) {
                        WriteBootReport(workspace, "waiting", "起動完了の確認に時間がかかっています。稼働プロセスは停止せず、コンソールで確認できます。");
                        return;
                    }
                    if (options.Connect && !bootTunnelCancelled && !Running(hostedTunnel)) {
                        var connection = ReadBootJson<HostingOptions>(Path.Combine(workspace, "connection-settings.json"));
                        var request = BackgroundTunnelRequest(workspace, connection, BootPort(Path.Combine(workspace, "server")));
                        if (request != null) {
                            request.BootRequest=true; ExecuteHost(request, workspace);
                            Thread.Sleep(2000);
                            if(!Running(hostedTunnel))throw new IOException("公開接続が起動直後に終了しました。認証とネットワーク設定を確認してください。");
                        }
                    }
                    WriteBootReport(workspace, "ready", "サーバーの自動起動が完了しました。");
                    return;
                } catch (Exception error) {
                    WriteBootReport(workspace, "error", "自動復帰を確認してください：" + error.Message);
                    if (attempt < 3) {
                        DateTime retry = DateTime.UtcNow.AddSeconds(30);
                        while (!hostExit && !bootCancelled && DateTime.UtcNow < retry) Thread.Sleep(250);
                    }
                }
            }
        } catch(Exception error) { WriteBootReport(workspace,"error","自動復帰を確認してください："+error.Message); }
        finally { if(bootCancelled)WriteBootReport(workspace,"stopped","今回の自動復帰は中断されました。稼働中のプロセスは管理画面で確認してください。");bootInitializing = false; }
    }
}
