using System;
using System.IO;
static class ScheduleFixture
{
    static int Main(string[] args)
    {
        if(Array.IndexOf(args,"-version")>=0){Console.Error.WriteLine("java.specification.version = 17\nsun.arch.data.model = 64\njava.vm.name = OpenJDK 64-Bit Server VM");return 0;}
        File.AppendAllText("schedule-launches.txt","start\n");
        Console.WriteLine("Done (0.1s)! For help, type help");
        string line;
        while((line=Console.ReadLine())!=null){
            File.AppendAllText("schedule-commands.txt",line+"\n");
            if(line=="stop")return 0;
            if(line=="list"){
                string count=File.Exists("players.txt")?File.ReadAllText("players.txt").Trim():"0";
                if(count!="unknown")Console.WriteLine("There are "+count+" of a max of 20 players online:");
            }
        }
        return 0;
    }
}
