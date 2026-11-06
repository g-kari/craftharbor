using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class CraftHarbor
{
    void ObserveKeyboardReview(Control container)
    {
        if(ManagerDataOverride==null||Array.IndexOf(Environment.GetCommandLineArgs(),"--keyboard-review")<0)return;
        foreach(var control in new[]{container}.Concat(Descendants(container))){var observed=control;observed.GotFocus+=delegate{File.AppendAllText(Path.Combine(root,"keyboard-focus.txt"),DateTime.UtcNow.ToString("o")+" | "+observed.GetType().Name+" | "+observed.AccessibleName+Environment.NewLine);};}
    }
    void CheckReviewScreens()
    {
        CheckLogSearch();
        CheckLogRecordBoundaries();
        CheckParticipantSelectionRefresh();
        foreach(var c in Descendants(this).Where(c=>c is TextBox||c is Button||c is Label||c is ComboBox||c is CheckBox))Require(c.Font.SizeInPoints>=12,"UI text is at least 12pt before enlargement");
        previewJavaPath=JavaPath();javaProbeError="Synthetic Java check error";RefreshJavaPreview();RefreshJavaPreview();Require(javaPreview.Text.Contains("Synthetic Java check error"),"Java probe failure survives preview refresh");javaProbeError="";RefreshJavaPreview();
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);
        CaptureGenericCheck(this,"ui-console-1024.png",1);CheckCommandRow();CaptureGenericCheck(this,"ui-dashboard-1024.png",0);
        Size=new Size(1360,960);workspace.SelectedIndex=3;Application.DoEvents();var settings=(FlowLayoutPanel)workspace.TabPages[3].Controls[0];settings.ScrollControlIntoView(autoDistances.Parent);Application.DoEvents();CaptureGenericCheck(this,"ui-distance-settings.png",3);
        settings.ScrollControlIntoView(autoJava.Parent);Application.DoEvents();CaptureGenericCheck(this,"ui-java-settings.png",3);
        Size=new Size(1024,720);largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();CaptureGenericCheck(this,"ui-console-accessible-1024.png",1);CheckCommandRow();Size=new Size(1360,960);
        File.WriteAllText(Path.Combine(root,"ui-review-verification.txt"),"PASS: console log search preserves selection for empty and missing queries, reports next match and wraparound, searches only visible sources, supports Enter, and clears stale feedback; participant selection survives automatic/manual list refresh, inserted and reordered names, and case-only identity changes; removed or unselected participants do not select another person; log record boundaries after source filtering, empty results, 4000-record rollover and 500000-character trimming; 12pt UI text baseline; console command row remains inside its layout at 1024x720 in normal and enlarged/high-contrast modes; 600-sample chart rollover and sample-gap rendering; CPU/memory/server/player distance policy, next-start settings persistence, active-port write protection, backup preservation. Screenshots are synthetic WinForms DrawToBitmap captures; native screen readers and Windows DPI scaling are not verified.");
    }
    void CheckLogSearchLayout()
    {
        Application.DoEvents();var header=(TableLayoutPanel)logSearchResult.Parent;var searches=(FlowLayoutPanel)header.Controls[1];
        Require(logSearchResult.Bottom<=header.ClientSize.Height&&logSearchResult.Height>=logSearchResult.Font.Height,"log search feedback fits its allocated header row");
        foreach(Control control in searches.Controls)Require(control.Left>=0&&control.Top>=0&&control.Right<=searches.ClientSize.Width&&control.Bottom<=searches.ClientSize.Height,"console search controls remain fully inside their actions row: "+LayoutTrace(control));
        Require(searches.Bottom<=logSearchResult.Top,"search feedback does not overlap search actions");
    }
    void CheckLogSearch()
    {
        Require(ManagerDataOverride!=null&&!timer.Enabled&&!Running(server)&&!Running(tunnel),"log search checks use a stopped synthetic fixture");
        var savedRecords=logRecords.ToArray();string savedText=log.Text,savedQuery=searchText.Text,savedResult=logSearchResult.Text;
        int savedStart=log.SelectionStart,savedLength=log.SelectionLength,savedPage=workspace.SelectedIndex,savedFilter=logSourceFilter.SelectedIndex;
        string savedRecent=recentLogs.Text;int savedRecentStart=recentLogs.SelectionStart,savedRecentLength=recentLogs.SelectionLength;
        string preference=Path.Combine(root,"manager-log-filter.txt");byte[] savedPreference=File.Exists(preference)?File.ReadAllBytes(preference):null;
        Control savedFocus=Descendants(this).FirstOrDefault(control=>control.Focused);
        bool savedLarge=largeText.Checked,savedContrast=highContrast.Checked;Size savedSize=Size;
        try{
            largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);
            workspace.SelectedIndex=1;logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            AppendLogRecords(new[]{"[MC] Alpha first","[公開] Synthetic tunnel","[MC] alpha second"});
            log.Select(5,5);searchText.Text="";searchText.Focus();Application.DoEvents();
            var search=Descendants(workspace.TabPages[1]).OfType<Button>().Single(button=>button.Text=="ログ内を検索");
            Require(search.Visible&&search.Enabled,"log search button is available on the visible console");
            search.PerformClick();Application.DoEvents();
            Require(log.SelectedText=="Alpha"&&searchText.Focused,"empty log search preserves the existing selection and keeps input focus");
            Require(logSearchResult.Text=="検索語を入力してください。","empty search explains the required input");
            searchText.Text="missing";Require(logSearchResult.Text=="","editing the query clears the previous result");search.PerformClick();Application.DoEvents();
            Require(log.SelectedText=="Alpha"&&searchText.Focused&&logSearchResult.Text=="表示中のログに見つかりません。","unsuccessful search explains no match without losing selection or input focus");
            CheckLogSearchLayout();CaptureGenericCheck(this,"ui-log-search-missing.png",1);

            searchText.Text="alpha";log.Select(0,0);search.PerformClick();Application.DoEvents();
            Require(log.SelectionStart==5&&log.SelectedText=="Alpha"&&log.Focused&&logSearchResult.Text=="一致する箇所を選択しました。","search selects the first literal case-insensitive match");
            int second=log.Text.IndexOf("alpha second",StringComparison.Ordinal);search.PerformClick();Application.DoEvents();
            Require(log.SelectionStart==second&&log.SelectedText=="alpha","repeated search advances to the next match");
            search.PerformClick();Application.DoEvents();
            Require(log.SelectionStart==5&&log.SelectedText=="Alpha"&&logSearchResult.Text=="先頭に戻り、一致する箇所を選択しました。","search wraps to the first match and explains the wrap");
            CaptureGenericCheck(this,"ui-log-search-wrapped.png",1);

            logSourceFilter.SelectedIndex=1;Require(logSearchResult.Text=="","changing displayed log sources clears the previous result");
            log.Select(5,5);searchText.Text="tunnel";search.PerformClick();Application.DoEvents();
            Require(log.SelectedText=="Alpha"&&logSearchResult.Text=="表示中のログに見つかりません。","search does not find a hidden source record");
            searchText.Text="alpha";log.Select(0,0);searchText.Focus();var enter=new KeyEventArgs(Keys.Enter);
            typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(searchText,new object[]{enter});Application.DoEvents();
            Require(enter.Handled&&enter.SuppressKeyPress&&log.SelectionStart==5&&log.SelectedText=="Alpha"&&log.Focused,"Enter from the search field invokes the same search and suppresses the input key");
            AppendLogRecords(new[]{"[MC] Synthetic appended record"});Require(logSearchResult.Text=="","a displayed log append clears the previous result");
            searchText.Text="[MC]";log.Select(0,0);search.PerformClick();Require(log.SelectedText=="[MC]","search terms are literal, including punctuation");
            logRecords.Clear();RebuildLogView();search.PerformClick();Require(log.Text==""&&logSearchResult.Text=="表示中のログに見つかりません。","an empty displayed log reports no match");
            largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();searchText.Text="";search.PerformClick();Application.DoEvents();
            Require(logSearchResult.Visible&&logSearchResult.Height>=logSearchResult.Font.Height&&logSearchResult.ForeColor==Color.White,"search feedback remains visible with enlarged high-contrast text");
            CheckLogSearchLayout();CaptureGenericCheck(this,"ui-log-search-accessible.png",1);
        }finally{
            largeText.Checked=savedLarge;highContrast.Checked=savedContrast;ApplyAccessibleTheme();Size=savedSize;
            logRecords.Clear();logRecords.AddRange(savedRecords);logSourceFilter.SelectedIndex=savedFilter;RebuildLogView();
            log.Text=savedText;searchText.Text=savedQuery;log.Select(savedStart,savedLength);logSearchResult.Text=savedResult;
            recentLogs.Text=savedRecent;FitRecentLogScroll();recentLogs.Select(savedRecentStart,savedRecentLength);
            if(savedPreference==null)File.Delete(preference);else File.WriteAllBytes(preference,savedPreference);
            workspace.SelectedIndex=savedPage;if(savedFocus!=null&&savedFocus.CanFocus)savedFocus.Focus();Application.DoEvents();
        }
        Require(log.Text==savedText&&logRecords.SequenceEqual(savedRecords)&&searchText.Text==savedQuery&&log.SelectionStart==savedStart&&log.SelectionLength==savedLength&&logSearchResult.Text==savedResult&&logSourceFilter.SelectedIndex==savedFilter,"log search checks restore the original records, query, selection, feedback and source filter");
    }
    void RequireParticipantSelection(string name,int column,string context)
    {
        Require(usersGrid.SelectedRows.Count==1&&Convert.ToString(usersGrid.SelectedRows[0].Cells[0].Value)==name,context+": selected participant identity");
        Require(usersGrid.CurrentCell!=null&&Convert.ToString(usersGrid.CurrentCell.OwningRow.Cells[0].Value)==name&&usersGrid.CurrentCell.ColumnIndex==column,context+": current cell stays with the selected participant");
    }
    void CheckParticipantSelectionRefresh()
    {
        Require(ManagerDataOverride!=null&&!timer.Enabled&&!Running(server)&&!Running(tunnel),"participant selection checks use a stopped synthetic fixture");
        foreach(string file in new[]{"whitelist.json","ops.json","banned-players.json"})Require(File.ReadAllText(Path.Combine(ServerDir,file)).Trim()=="[]","participant selection checks require empty synthetic player files");
        string savedOnlineNames=onlineNames;int savedPage=workspace.SelectedIndex;
        string savedSelected=usersGrid.SelectedRows.Count==0?null:Convert.ToString(usersGrid.SelectedRows[0].Cells[0].Value);
        string savedCurrent=usersGrid.CurrentCell==null?null:Convert.ToString(usersGrid.CurrentCell.OwningRow.Cells[0].Value);
        int savedColumn=usersGrid.CurrentCell==null?0:usersGrid.CurrentCell.ColumnIndex,savedScroll=usersGrid.FirstDisplayedScrollingRowIndex;
        var savedRows=usersGrid.Rows.Cast<DataGridViewRow>().Select(row=>row.Cells.Cast<DataGridViewCell>().Select(cell=>Convert.ToString(cell.Value)).ToArray()).ToArray();
        Control savedFocus=Descendants(this).FirstOrDefault(control=>control.Focused);
        try{
            workspace.SelectedIndex=2;onlineNames="Alice, Bob, Carol";RefreshUsers();Application.DoEvents();
            Require(usersGrid.Rows.Cast<DataGridViewRow>().Select(row=>Convert.ToString(row.Cells[0].Value)).SequenceEqual(new[]{"Alice","Bob","Carol"}),"participant selection fixture contains only synthetic names");
            var bob=usersGrid.Rows.Cast<DataGridViewRow>().Single(row=>Convert.ToString(row.Cells[0].Value)=="Bob");
            usersGrid.CurrentCell=bob.Cells[3];usersGrid.ClearSelection();bob.Selected=true;usersGrid.Focus();Application.DoEvents();
            RequireParticipantSelection("Bob",3,"non-first participant fixture selection");

            // The same production method runs on the normal 15-second refresh path.
            RefreshUsers();Application.DoEvents();
            RequireParticipantSelection("Bob",3,"automatic participant refresh retains non-first selection");
            var refresh=Descendants(workspace.TabPages[2]).OfType<Button>().Single(button=>button.Text=="一覧更新");
            Require(refresh.Visible&&refresh.Enabled,"participant refresh button is available on the visible page");
            // SendQuiet returns without host IPC because this fixture has no server.
            refresh.PerformClick();Application.DoEvents();
            RequireParticipantSelection("Bob",3,"visible participant refresh button retains selection");

            onlineNames="Carol, Aaron, Bob, Alice";RefreshUsers();Application.DoEvents();
            Require(usersGrid.Rows.Cast<DataGridViewRow>().Select(row=>Convert.ToString(row.Cells[0].Value)).SequenceEqual(new[]{"Aaron","Alice","Bob","Carol"}),"participant refresh inserts an earlier name and sorts the reordered input");
            RequireParticipantSelection("Bob",3,"participant selection follows identity after row positions change");
            onlineNames="CAROL, bOB, Aaron, Alice";RefreshUsers();Application.DoEvents();
            RequireParticipantSelection("bOB",3,"participant selection accepts a case-only name change");
            CaptureGenericCheck(this,"ui-users-selection.png",2);

            onlineNames="Carol, Aaron, Alice";RefreshUsers();Application.DoEvents();
            Require(usersGrid.Rows.Count==3&&usersGrid.SelectedRows.Count==0&&usersGrid.CurrentCell==null,"removed participant does not select a different person");
            RefreshUsers();Application.DoEvents();
            Require(usersGrid.SelectedRows.Count==0&&usersGrid.CurrentCell==null,"repeated refresh after participant removal remains unselected");
            onlineNames="Alice, Bob, Carol";RefreshUsers();Application.DoEvents();
            Require(usersGrid.SelectedRows.Count==0,"a returning participant is not automatically reselected");

            usersGrid.CurrentCell=usersGrid.Rows[1].Cells[2];usersGrid.ClearSelection();
            Require(usersGrid.SelectedRows.Count==0&&usersGrid.CurrentCell!=null,"unselected fixture can retain a keyboard current cell");
            RefreshUsers();Application.DoEvents();
            Require(usersGrid.SelectedRows.Count==0,"refresh does not turn an unselected current cell into a participant selection");
            bob=usersGrid.Rows.Cast<DataGridViewRow>().Single(row=>Convert.ToString(row.Cells[0].Value)=="Bob");
            usersGrid.CurrentCell=bob.Cells[0];usersGrid.ClearSelection();bob.Selected=true;
            onlineNames="";RefreshUsers();Application.DoEvents();
            Require(usersGrid.Rows.Count==0&&usersGrid.SelectedRows.Count==0&&usersGrid.CurrentCell==null,"empty participant refresh clears selection and current cell");
            onlineNames="Alice, Bob";RefreshUsers();Application.DoEvents();
            Require(usersGrid.Rows.Count==2&&usersGrid.SelectedRows.Count==0,"first populated refresh without a selected participant remains unselected");
        }finally{
            onlineNames=savedOnlineNames;RefreshUsers();usersGrid.CurrentCell=null;usersGrid.ClearSelection();
            var current=usersGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row=>Convert.ToString(row.Cells[0].Value)==savedCurrent);
            if(current!=null)usersGrid.CurrentCell=current.Cells[savedColumn];
            usersGrid.ClearSelection();
            var selected=usersGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row=>Convert.ToString(row.Cells[0].Value)==savedSelected);
            if(selected!=null)selected.Selected=true;
            if(savedScroll>=0&&savedScroll<usersGrid.Rows.Count)usersGrid.FirstDisplayedScrollingRowIndex=savedScroll;
            workspace.SelectedIndex=savedPage;if(savedFocus!=null&&savedFocus.CanFocus)savedFocus.Focus();Application.DoEvents();
        }
        Require(onlineNames==savedOnlineNames&&workspace.SelectedIndex==savedPage,"participant selection checks restore the online names and page");
        Require(usersGrid.Rows.Count==savedRows.Length&&usersGrid.Rows.Cast<DataGridViewRow>().Select((row,index)=>row.Cells.Cast<DataGridViewCell>().Select(cell=>Convert.ToString(cell.Value)).SequenceEqual(savedRows[index])).All(equal=>equal),"participant selection checks restore the screenshot rows");
        Require((usersGrid.SelectedRows.Count==0?null:Convert.ToString(usersGrid.SelectedRows[0].Cells[0].Value))==savedSelected,"participant selection checks restore the original selection");
        Require((usersGrid.CurrentCell==null?null:Convert.ToString(usersGrid.CurrentCell.OwningRow.Cells[0].Value))==savedCurrent&&(savedCurrent==null||usersGrid.CurrentCell.ColumnIndex==savedColumn),"participant selection checks restore the original current cell");
    }
    void RequireLogView(string[] expected,int total,string context)
    {
        if(expected.Length==0)Require(log.Text=="",context+": no text or blank line without visible records");
        else{
            var actual=log.Text.Split(new[]{Environment.NewLine},StringSplitOptions.None);
            Require(actual.Length==expected.Length+1&&actual[actual.Length-1]=="",context+": one final record delimiter and no extra blank line");
            Require(actual.Take(expected.Length).SequenceEqual(expected),context+": visible records retain their content, order and separation");
        }
        Require(logCounter.Text==expected.Length+" / "+total+"件を表示",context+": visible and retained counts");
    }
    void CheckLogRecordBoundaries()
    {
        var savedRecords=logRecords.ToArray();int savedFilter=logSourceFilter.SelectedIndex;string savedText=log.Text;int savedStart=log.SelectionStart,savedLength=log.SelectionLength;
        string savedRecent=recentLogs.Text;int savedRecentStart=recentLogs.SelectionStart,savedRecentLength=recentLogs.SelectionLength;
        string preference=Path.Combine(root,"manager-log-filter.txt");byte[] savedPreference=File.Exists(preference)?File.ReadAllBytes(preference):null;
        try{
            logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            RequireLogView(new string[0],0,"empty rebuild");AppendLogRecords(new string[0]);RequireLogView(new string[0],0,"empty append");
            var mixed=new[]{"[MC] Synthetic game before filter","[公開] Synthetic public before filter","[管理] Synthetic manager before filter"};
            AppendLogRecords(mixed);RequireLogView(mixed,3,"initial mixed append");
            logSourceFilter.SelectedIndex=1;RequireLogView(new[]{mixed[0]},3,"game filter rebuild");
            AppendLogRecords(new[]{"[管理] Synthetic hidden append","[MC] Synthetic game after filter"});
            RequireLogView(new[]{mixed[0],"[MC] Synthetic game after filter"},5,"append after filter rebuild");
            logSourceFilter.SelectedIndex=0;
            RequireLogView(new[]{mixed[0],mixed[1],mixed[2],"[管理] Synthetic hidden append","[MC] Synthetic game after filter"},5,"all sources restored");

            logRecords.Clear();logSourceFilter.SelectedIndex=3;RebuildLogView();
            AppendLogRecords(new[]{"[MC] Synthetic unmatched game"});RequireLogView(new string[0],1,"no matching records");
            logSourceFilter.SelectedIndex=2;logSourceFilter.SelectedIndex=3;RequireLogView(new string[0],1,"no matching filter rebuild");
            AppendLogRecords(new[]{"[MC] Synthetic unmatched append"});RequireLogView(new string[0],2,"hidden append remains empty");
            AppendLogRecords(new[]{"[管理] Synthetic first visible record"});RequireLogView(new[]{"[管理] Synthetic first visible record"},3,"first match has no leading blank line");
            AppendLogRecords(new string[0]);RequireLogView(new[]{"[管理] Synthetic first visible record"},3,"empty batch preserves visible content");

            logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            var shortLines=Enumerable.Range(0,4002).Select(i=>"[MC] Synthetic retained line "+i).ToArray();
            AppendLogRecords(shortLines.Take(4000));RequireLogView(shortLines.Take(4000).ToArray(),4000,"at line retention limit");
            AppendLogRecords(new[]{shortLines[4000]});RequireLogView(shortLines.Skip(1).Take(4000).ToArray(),4000,"line retention rollover");
            AppendLogRecords(new[]{shortLines[4001]});RequireLogView(shortLines.Skip(2).ToArray(),4000,"append after line retention rollover");
            Require(log.TextLength<=500000,"short retained records remain within character limit");

            logRecords.Clear();RebuildLogView();
            var longLines=Enumerable.Range(0,100).Select(i=>{string prefix="[MC] Synthetic long line "+i.ToString("D3")+" ";return prefix+new string('x',5000-Environment.NewLine.Length-prefix.Length);}).ToArray();
            AppendLogRecords(longLines);RequireLogView(longLines,100,"at character retention limit");
            Require(log.TextLength==500000,"character retention limit includes each final record delimiter");
            AppendLogRecords(new[]{"[MC] Synthetic character trim trigger"});
            RequireLogView(new[]{"[MC] Synthetic character trim trigger"},1,"character trimming removes the oldest 100 records");
            Require(log.TextLength<=500000,"character trimming enforces visible text limit");
            AppendLogRecords(new[]{"[MC] Synthetic record after character trim"});
            RequireLogView(new[]{"[MC] Synthetic character trim trigger","[MC] Synthetic record after character trim"},2,"append after character trimming");
            Require(log.TextLength<=500000,"append after character trimming stays within limit");

            logRecords.Clear();RebuildLogView();
            var beforeDoubleLimit=Enumerable.Range(0,4000).Select(i=>"[MC] Synthetic short line "+i).ToArray();
            for(int batch=0;batch<10;batch++)AppendLogRecords(beforeDoubleLimit.Skip(batch*400).Take(400));
            RequireLogView(beforeDoubleLimit,4000,"short records fill the record cap before the double limit");
            var overlappingLimits=Enumerable.Range(0,400).Select(i=>"[MC] Synthetic double limit "+i.ToString("D4")+" "+new string('x',2000)).ToArray();
            AppendLogRecords(overlappingLimits);
            Require(log.TextLength<=500000,"one tick-sized incoming batch enforces both record and character limits");
            Require(logRecords.Count<=4000,"double limit batch preserves the record cap");
            var expectedDoubleLimit=overlappingLimits.Skip(200).ToArray();
            RequireLogView(expectedDoubleLimit,200,"double limit trim retains the newest 200 records without duplicate append");
            AppendLogRecords(new[]{"[MC] Synthetic after double limit"});
            RequireLogView(expectedDoubleLimit.Concat(new[]{"[MC] Synthetic after double limit"}).ToArray(),201,"append after simultaneous record and character trimming");
            Require(log.TextLength<=500000,"following append stays within the character limit");

        }finally{
            logRecords.Clear();logRecords.AddRange(savedRecords);logSourceFilter.SelectedIndex=savedFilter;RebuildLogView();log.Text=savedText;log.Select(savedStart,savedLength);
            recentLogs.Text=savedRecent;FitRecentLogScroll();recentLogs.Select(savedRecentStart,savedRecentLength);
            if(savedPreference==null)File.Delete(preference);else File.WriteAllBytes(preference,savedPreference);
        }
        Require(log.Text==savedText&&logRecords.SequenceEqual(savedRecords)&&logSourceFilter.SelectedIndex==savedFilter&&recentLogs.Text==savedRecent,"log boundary checks restore the screenshot fixture");
    }
    static string LayoutTrace(Control control){string text="";for(Control c=control;c!=null;c=c.Parent)text+=c.GetType().Name+"="+c.Bounds+"; ";return text;}
    void CheckCommandRow()
    {
        Application.DoEvents();var frame=command.Parent;var panel=frame.Parent;Require(frame.Height>=command.PreferredSize.Height+14&&frame.Bottom<=panel.ClientSize.Height&&frame.Right<=panel.ClientSize.Width,"console input has enough vertical space");
        foreach(var button in panel.Controls.OfType<Button>())Require(button.Bottom<=panel.ClientSize.Height&&button.Right<=panel.ClientSize.Width&&button.RectangleToScreen(button.ClientRectangle).Right<=RectangleToScreen(ClientRectangle).Right&&button.Height>=button.Font.Height+12,"console send button is fully inside its row: "+LayoutTrace(button));
        Require(logSourceFilter.ForeColor!=Color.Black||highContrast.Checked,"log filter uses readable foreground");
    }
}
