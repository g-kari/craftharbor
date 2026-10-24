using System;
static class Fixture
{
    static int Main(){Console.WriteLine("Done (0.1s)! For help, type help");string line;while((line=Console.ReadLine())!=null){if(line=="stop")return 0;if(line=="ping")Console.WriteLine("HOST_CHECK_PONG");if(line=="spark tps"){Console.WriteLine("[spark/INFO]: TPS from last 5s, 10s, 1m, 5m, 15m:");Console.WriteLine("[spark/INFO]: [fixture] 20.0, 20.0, 20.0, 20.0, 20.0");Console.WriteLine("[spark/INFO]: Tick durations (min/med/95%ile/max ms) from last 10s, 1m:");Console.WriteLine("[spark/INFO]: [fixture] 0.5/0.8/1.9/5.2; 0.3/0.7/1.4/5.2");}}return 0;}
}
