using System;
using System.IO;
static class BootFixture
{
    static int Main(string[] args)
    {
        if(Array.IndexOf(args,"-version")>=0) {
            Console.Error.WriteLine("java.specification.version = 17\nsun.arch.data.model = 64\njava.vm.name = OpenJDK 64-Bit Server VM");
            return 0;
        }
        if(Array.IndexOf(args,"-jar")>=0) {
            File.AppendAllText("boot-launches.txt","start\n");
            Console.WriteLine("Done (0.1s)! For help, type help");
        }
        string line;
        while((line=Console.ReadLine())!=null) {
            if(line=="stop")return 0;
            if(line=="ping")Console.WriteLine("BOOT_CHECK_PONG");
        }
        return 0;
    }
}
