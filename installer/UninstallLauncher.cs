using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
[assembly:AssemblyTitle("CraftHarbor Uninstall")]
[assembly:AssemblyProduct("CraftHarbor")]
[assembly:AssemblyCompany("g-kari")]
[assembly:AssemblyVersion("0.3.0.0")]
static class UninstallLauncher
{
    [STAThread]static int Main(){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"unins000.exe");if(!File.Exists(path)){MessageBox.Show("インストール済みのCraftHarborで実行してください。ポータブル版はアプリ本体を削除してアンインストールできます。ワールド・バックアップは残してください。","CraftHarbor");return 1;}Process.Start(new ProcessStartInfo(path){UseShellExecute=false});return 0;}
}
