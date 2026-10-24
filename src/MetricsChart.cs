using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;

partial class CraftHarbor
{
    void InitializeMetricChart()
    {
        var area=history.ChartAreas[0];var font=new Font("Yu Gothic UI",12);
        area.AxisX.IsLabelAutoFit=area.AxisY.IsLabelAutoFit=area.AxisY2.IsLabelAutoFit=false;history.Legends[0].IsTextAutoFit=false;
        area.AxisX.LabelStyle.Font=area.AxisY.LabelStyle.Font=area.AxisY2.LabelStyle.Font=font;
        area.AxisY.TitleFont=area.AxisY2.TitleFont=font;area.AxisX.IntervalType=DateTimeIntervalType.Seconds;area.AxisX.Interval=60;
        area.AxisY.LineColor=area.AxisX.LineColor=area.AxisY2.LineColor=Muted;area.AxisX.MajorTickMark.LineColor=area.AxisY.MajorTickMark.LineColor=area.AxisY2.MajorTickMark.LineColor=Muted;
        area.AxisY2.Maximum=8;history.Legends[0].Font=font;
        foreach(var series in history.Series){series.ChartType=SeriesChartType.Line;series.MarkerSize=6;}
        history.Titles.Add(new Title{ Name="state",Text="サーバー起動後にCPU・RAMの実測グラフを表示します",Font=font,ForeColor=Muted,Docking=Docking.Top });
        ArrangeMetricChart();history.Resize+=delegate{ArrangeMetricChart();};SetChartWindow(DateTime.Now);
    }
    void ArrangeMetricChart()
    {
        // Reserve predictable space for 12pt axes; automatic layout can squeeze the plot
        // to a few pixels once a state title and legend are present.
        var area=history.ChartAreas[0];area.Position=new ElementPosition(0,24,100,76);
        area.InnerPlotPosition=new ElementPosition(14,4,72,70);area.AxisY.Interval=25;
        history.Titles["state"].Position=new ElementPosition(2,0,96,12);
        history.Legends[0].Position=new ElementPosition(48,12,50,12);history.Legends[0].Alignment=StringAlignment.Far;
    }
    void SetChartWindow(DateTime now)
    {
        var area=history.ChartAreas[0];var points=history.Series[0].Points;DateTime first=points.Count==0?now.AddMinutes(-1):DateTime.FromOADate(points[0].XValue).AddSeconds(-1);DateTime minimum=first>now.AddMinutes(-5)?first:now.AddMinutes(-5);DateTime maximum=now.AddSeconds(1)>minimum.AddSeconds(30)?now.AddSeconds(1):minimum.AddSeconds(30);
        area.AxisX.Minimum=minimum.ToOADate();area.AxisX.Maximum=maximum.ToOADate();area.AxisX.Interval=(maximum-minimum).TotalSeconds<120?10:60;
        double peak=history.Series[1].Points.Count==0?0:history.Series[1].Points.Max(p=>p.YValues[0]);area.AxisY2.Maximum=Math.Max(1,Math.Ceiling(peak*1.15));
        area.RecalculateAxesScale();history.Invalidate();
    }
    void AppendMetricPoint(DateTime now,double cpu,double ram)
    {
        if(double.IsNaN(cpu)||double.IsInfinity(cpu)||double.IsNaN(ram)||double.IsInfinity(ram))throw new InvalidDataException("メトリクスが数値ではありません。");
        double x=now.ToOADate();history.Series[0].Points.AddXY(x,Math.Max(0,Math.Min(100,cpu)));history.Series[1].Points.AddXY(x,Math.Max(0,ram));
        while(history.Series[0].Points.Count>300||(history.Series[0].Points.Count>1&&history.Series[0].Points[0].XValue<now.AddMinutes(-5).ToOADate()))foreach(var s in history.Series)s.Points.RemoveAt(0);
        foreach(var s in history.Series)s.MarkerStyle=s.Points.Count==1?MarkerStyle.Circle:MarkerStyle.None;
        history.Titles["state"].Text="直近5分 / "+now.ToString("HH:mm:ss")+" / CPU "+cpu.ToString("0.0")+"%・RAM "+ram.ToString("0.00")+" GiB";SetChartWindow(now);
    }
    void ClearMetricHistory(){if(history==null)return;foreach(var series in history.Series)series.Points.Clear();history.Titles["state"].Text="サーバー起動中：最初のメトリクスを待っています";SetChartWindow(DateTime.Now);}
    void CheckMetricChart()
    {
        ClearMetricHistory();var start=new DateTime(2026,1,1,12,0,0);for(int i=0;i<=600;i++)AppendMetricPoint(start.AddSeconds(i),10+8*Math.Sin(i/18.0),4+i/1000.0);
        var area=history.ChartAreas[0];Require(history.Series.All(s=>s.Points.Count==300),"rolling graph retains only 300 samples");Require(area.AxisX.Minimum>start.ToOADate()&&area.AxisX.Maximum>history.Series[0].Points[299].XValue,"graph bounds follow recent samples after rollover");Require(area.AxisY2.Maximum>4.6,"RAM scale includes current sample");
        AppendMetricPoint(start.AddHours(1),0,4);Require(history.Series.All(s=>s.Points.Count==1&&s.MarkerStyle==MarkerStyle.Circle),"first sample visible after a data gap");
        ClearMetricHistory();for(int i=0;i<180;i++)AppendMetricPoint(DateTime.Now.AddSeconds(i-179),5+4*Math.Sin(i/11.0),3.2+i/300.0);history.Titles["state"].Text="合成データ：表示検証用 / 実サーバーの値ではありません";
    }
    static int CheckLiveMetricRendering(string[] args)
    {
        if(args.Length!=3)return 2;string scratch=Path.GetFullPath(args[2]);if(Directory.Exists(scratch)&&Directory.EnumerateFileSystemEntries(scratch).Any())return 2;
        Directory.CreateDirectory(scratch);try{int pid;if(!int.TryParse(args[1],out pid))throw new ArgumentException("JavaのPIDを指定してください。");
            using(var process=Process.GetProcessById(pid)){if(process.ProcessName!="java"&&process.ProcessName!="javaw")throw new ArgumentException("Javaのプロセスを指定してください。");
                WorkspaceRoot=scratch;ManagerDataOverride=Path.Combine(scratch,"check-data");Directory.CreateDirectory(Path.Combine(scratch,"server"));File.WriteAllText(Path.Combine(scratch,"server","server.properties"),"server-port=25579\nview-distance=8\nsimulation-distance=6\n");File.WriteAllBytes(Path.Combine(scratch,"server","server.jar"),new byte[0]);
                using(var manager=new CraftHarbor()){manager.timer.Stop();manager.ClearMetricHistory();process.Refresh();var previousCpu=process.TotalProcessorTime;var previousAt=DateTime.UtcNow;
                    for(int i=0;i<6;i++){Thread.Sleep(1000);process.Refresh();var now=DateTime.UtcNow;var cpu=process.TotalProcessorTime;manager.AppendMetricPoint(DateTime.Now,100*(cpu-previousCpu).TotalSeconds/(now-previousAt).TotalSeconds/Environment.ProcessorCount,process.WorkingSet64/1073741824.0);previousCpu=cpu;previousAt=now;}
                    manager.history.Titles["state"].Text="稼働中Javaの実測CPU・RAM / 読み取り専用・再起動なし";manager.history.Parent.Controls.Remove(manager.history);manager.history.Size=new Size(1200,420);manager.history.SaveImage(Path.Combine(scratch,"graph-real-process.png"),ChartImageFormat.Png);manager.history.Dispose();
                    File.WriteAllText(Path.Combine(scratch,"graph-verification.txt"),"PASS: 6 live process CPU/RAM samples rendered with the application's chart and rolling axes. Read-only process statistics; no Minecraft command, process control, settings change to the real server, tunnel or Discord operation.");
                }
            }return 0;
        }catch(Exception error){File.WriteAllText(Path.Combine(scratch,"graph-verification.txt"),"FAIL: "+error.Message);return 1;}
    }
}
