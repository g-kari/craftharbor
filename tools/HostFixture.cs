using System;
using System.IO;
using System.Threading;
static class Fixture
{
    static int Main(string[] args)
    {
        string control=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-fixture");
        if(Array.IndexOf(args,"--probe-timeout-fixture")>=0){
            File.WriteAllText(control+".timeout-pid",System.Diagnostics.Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Thread.Sleep(30000);return 0;
        }
        if(Array.IndexOf(args,"-version")>=0){
            bool settings=Array.IndexOf(args,"-XshowSettings:properties")>=0;
            if(settings&&File.Exists(control+".hold")){
                File.AppendAllText(control+".probes","probe\n");File.WriteAllText(control+".started","ready");
                DateTime deadline=DateTime.UtcNow.AddSeconds(6);
                while(!File.Exists(control+".release")&&DateTime.UtcNow<deadline)Thread.Sleep(20);
                if(!File.Exists(control+".release")){File.AppendAllText(control+".results","gate-timeout\n");return 2;}
            }
            if(File.Exists(control+".fail")){File.AppendAllText(control+".results","deliberate-failure\n");return 1;}
            Console.Error.WriteLine("java.specification.version = 17\nsun.arch.data.model = 64\njava.vm.name = OpenJDK 64-Bit Server VM");
            if(File.Exists(control+".private-output"))Console.Error.WriteLine("user.home = PRIVATE-FIXTURE-DO-NOT-LOG");
            File.AppendAllText(control+".results",settings?"settings-success\n":"flags-success\n");return 0;
        }
        File.AppendAllText(control+".launches","start\n");
        Console.WriteLine("Done (0.1s)! For help, type help");string line;while((line=Console.ReadLine())!=null){if(line=="stop")return 0;if(line=="ping")Console.WriteLine("HOST_CHECK_PONG");if(line=="spark tps"){Console.WriteLine("[spark/INFO]: TPS from last 5s, 10s, 1m, 5m, 15m:");Console.WriteLine("[spark/INFO]: [fixture] 20.0, 20.0, 20.0, 20.0, 20.0");Console.WriteLine("[spark/INFO]: Tick durations (min/med/95%ile/max ms) from last 10s, 1m:");Console.WriteLine("[spark/INFO]: [fixture] 0.5/0.8/1.9/5.2; 0.3/0.7/1.4/5.2");}}return 0;
    }
}
