using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

partial class CraftHarbor
{
    readonly List<string> logRecords=new List<string>();
    ComboBox logSourceFilter;
    Label logCounter;
    static int LogChannel(string line){if(line.StartsWith("[MC]",StringComparison.Ordinal)||line.StartsWith("> ",StringComparison.Ordinal))return 1;if(line.StartsWith("[playit]",StringComparison.OrdinalIgnoreCase)||line.StartsWith("[ngrok]",StringComparison.OrdinalIgnoreCase)||line.StartsWith("[公開",StringComparison.Ordinal)||line.StartsWith("[音声]",StringComparison.Ordinal)||line.StartsWith("[playit設定]",StringComparison.Ordinal))return 2;return 3;}
    bool LogVisible(string line){return logSourceFilter==null||logSourceFilter.SelectedIndex<=0||LogChannel(line)==logSourceFilter.SelectedIndex;}
    void AppendLogRecords(IEnumerable<string> lines){var incoming=lines.ToArray();if(incoming.Length==0)return;logRecords.AddRange(incoming);if(logRecords.Count>4000){logRecords.RemoveRange(0,logRecords.Count-4000);RebuildLogView();return;}var selected=incoming.Where(LogVisible).ToArray();if(selected.Length>0)log.AppendText(string.Join(Environment.NewLine,selected)+Environment.NewLine);if(log.TextLength>500000){while(logRecords.Count>0&&log.TextLength>500000){logRecords.RemoveRange(0,Math.Min(100,logRecords.Count));log.Text=string.Join(Environment.NewLine,logRecords.Where(LogVisible));}}UpdateLogCount();}
    void UpdateLogCount(){if(logCounter!=null)logCounter.Text=logRecords.Count(LogVisible)+" / "+logRecords.Count+"件を表示";}
    void RebuildLogView(){if(log==null)return;log.Text=string.Join(Environment.NewLine,logRecords.Where(LogVisible));log.SelectionStart=log.TextLength;log.ScrollToCaret();UpdateLogCount();}
    Control LogHeader(Control searchActions)
    {
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};header.RowStyles.Add(new RowStyle(SizeType.Absolute,48));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var filters=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};filters.Controls.Add(new Label{Text="表示するログ",AutoSize=true,ForeColor=Muted,Margin=new Padding(0,12,12,0)});logSourceFilter=new HarborChoice{Width=240,DropDownWidth=260,AccessibleName="ログの種類",AccessibleDescription="Minecraft、公開接続、管理メッセージ、すべてから選択して表示を切り替えます。"};logSourceFilter.Items.AddRange(new[]{"すべて","Minecraft","playit・ngrok / 公開接続","管理"});int selected=0;string preference=Path.Combine(root,"manager-log-filter.txt");if(File.Exists(preference)){int value;if(int.TryParse(File.ReadAllText(preference).Trim(),out value)&&value>=0&&value<=3)selected=value;}logSourceFilter.SelectedIndex=selected;filters.Controls.Add(logSourceFilter);logCounter=new Label{AutoSize=true,ForeColor=Muted,Margin=new Padding(16,12,0,0),AccessibleName="表示中のログ件数"};filters.Controls.Add(logCounter);header.Controls.Add(filters,0,0);
        searchActions.Dock=DockStyle.Fill;header.Controls.Add(searchActions,0,1);logSourceFilter.SelectedIndexChanged+=delegate{RebuildLogView();File.WriteAllText(preference,logSourceFilter.SelectedIndex.ToString(),new UTF8Encoding(false));};UpdateLogCount();return header;
    }
}
