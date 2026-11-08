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
        CheckParticipantGuideFeedback();
        CheckStoppedConsoleDrafts();
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
        File.WriteAllText(Path.Combine(root,"ui-review-verification.txt"),"PASS: participant-guide copy and TXT save use the visible buttons, preserve current text and UTF-8 BOM, show the actual save path, clear stale results on changed or explicit refresh, recover on explicit retry, and retain readable enlarged high-contrast feedback; locked-file save/refresh failure states are checked through the same production actions directly, not through the modal dialog; stopped console submissions preserve the exact unsent draft through the send button and Enter, including repeated attempts and empty input; console log search preserves selection for empty and missing queries, reports next match and wraparound, searches only visible sources, supports Enter, and clears stale feedback; Ctrl+F bubbles from command and settings inputs to focus and select the query without changing logs or the unsent command, F3 advances and Shift+F3 searches backward, both wrap only on the console; reverse search covers current-selection exclusion, end and zero carets, a prefix shorter than the query, literal punctuation, successful filtered reflow, empty queries/logs, adjacent and single matches, repeated direction changes, caret-inside boundaries, source-filter and search-button key bubbling, unchanged unsent drafts and enlarged high-contrast feedback; other modifiers remain untouched; participant selection survives automatic/manual list refresh, inserted and reordered names, and case-only identity changes; removed or unselected participants do not select another person; log record boundaries after source filtering, empty results, 4000-record rollover and 500000-character trimming; 12pt UI text baseline; console command row remains inside its layout at 1024x720 in normal and enlarged/high-contrast modes; 600-sample chart rollover and sample-gap rendering; CPU/memory/server/player distance policy, next-start settings persistence, active-port write protection, backup preservation. Screenshots are synthetic WinForms DrawToBitmap captures; native screen readers and Windows DPI scaling are not verified.");
    }
    void CheckParticipantGuideFeedback()
    {
        Require(ManagerDataOverride!=null&&!timer.Enabled&&!Running(server)&&!Running(tunnel),"participant guide feedback checks use a stopped synthetic workspace");
        var page=workspace.TabPages[7];
        var result=Descendants(page).OfType<Label>().SingleOrDefault(c=>c.AccessibleName=="参加案内の操作結果");
        Require(result!=null,"participant guide copy and TXT save have same-page result feedback");
        int savedPage=workspace.SelectedIndex;Size savedSize=Size;bool savedLarge=largeText.Checked,savedContrast=highContrast.Checked;
        string savedAddress=address.Text,savedGuide=participantGuide.Text,savedResult=result.Text;
        string file=Path.Combine(root,"Discord参加案内.txt"),preference=Path.Combine(root,"manager-log-filter.txt");
        byte[] savedBytes=File.ReadAllBytes(file),savedPreference=File.Exists(preference)?File.ReadAllBytes(preference):null;
        var savedQueue=queue.ToArray();Control savedFocus=Descendants(this).FirstOrDefault(c=>c.Focused);
        try{
            largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);workspace.SelectedIndex=7;Application.DoEvents();
            var copy=Descendants(page).OfType<Button>().Single(c=>c.Text=="Discord用コピー");
            var save=Descendants(page).OfType<Button>().Single(c=>c.Text=="TXTで保存");
            var refresh=Descendants(page).OfType<Button>().Single(c=>c.Text=="接続先を反映");
            Require(copy.Visible&&copy.Enabled&&save.Visible&&save.Enabled,"participant guide actions are available on the visible page");
            address.Text="synthetic.example.invalid:25567";copy.PerformClick();Application.DoEvents();
            Require(Clipboard.GetText()==participantGuide.Text&&participantGuide.Text==GuideText(),"copy button copies the current synthetic guide exactly");
            Require(result.Visible&&result.Text=="参加案内をコピーしました。Discordなどに貼り付けできます。","copy success is visible beside the actions");
            CaptureGenericCheck(this,"ui-guide-copy-result.png",7);
            save.PerformClick();Application.DoEvents();
            Require(File.ReadAllText(file)==participantGuide.Text&&File.ReadAllBytes(file).Take(3).SequenceEqual(new byte[]{239,187,191}),"TXT button preserves the current complete guide and existing UTF-8 BOM encoding");
            Require(result.Text=="TXTを保存しました。保存先: "+file,"TXT success identifies the existing actual destination");
            CheckParticipantGuideResultLayout(result);CaptureGenericCheck(this,"ui-guide-save-result.png",7);
            address.Text="updated.example.invalid:25568";refresh.PerformClick();Application.DoEvents();
            Require(result.Text==""&&participantGuide.Text==GuideText()&&File.ReadAllText(file)==participantGuide.Text,"refreshing changed guide content clears stale success and retains automatic TXT export");
            copy.PerformClick();copy.PerformClick();save.PerformClick();save.PerformClick();Application.DoEvents();
            Require(result.Text.StartsWith("TXTを保存しました。",StringComparison.Ordinal)&&File.ReadAllText(file)==participantGuide.Text,"repeated guide actions confirm the current completed operation");

            refresh.PerformClick();Application.DoEvents();Require(result.Text=="","explicit refresh clears prior success even when guide content is unchanged");
            save.PerformClick();Application.DoEvents();

            // Invoke the same production save action directly to inspect its error state without opening the shared modal dialog.
            // The actual visible save/copy/refresh button paths are exercised above. This failure case is not a modal-dialog UI test.
            var action=typeof(CraftHarbor).GetMethod("SaveParticipantGuide",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Require(action!=null,"TXT button has a production save action for failure coverage");
            byte[] beforeFailure=File.ReadAllBytes(file);int successes=queue.ToArray().Count(line=>line=="[案内] Discord参加案内.txtを保存しました。");bool rejected=false;
            using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){
                try{action.Invoke(this,null);}catch(System.Reflection.TargetInvocationException e){if(!(e.InnerException is IOException))throw;rejected=true;}
            }
            Require(rejected&&result.Text=="TXTを保存できませんでした。エラーを確認して、やり直してください。","failed save replaces prior success before shared error handling");
            Require(File.ReadAllBytes(file).SequenceEqual(beforeFailure)&&queue.ToArray().Count(line=>line=="[案内] Discord参加案内.txtを保存しました。")==successes,"failed save leaves existing TXT bytes and success logs unchanged");
            CheckParticipantGuideResultLayout(result);CaptureGenericCheck(this,"ui-guide-save-failure.png",7);
            save.PerformClick();Application.DoEvents();Require(result.Text=="TXTを保存しました。保存先: "+file,"explicit save retry recovers after a locked file is released");
            var refreshAction=typeof(CraftHarbor).GetMethod("RefreshParticipantGuide",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Require(refreshAction!=null,"refresh button has its production action for error-state coverage");bool refreshRejected=false;
            using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){
                try{refreshAction.Invoke(this,null);}catch(System.Reflection.TargetInvocationException e){if(!(e.InnerException is IOException))throw;refreshRejected=true;}
            }
            Require(refreshRejected&&result.Text==""&&File.ReadAllBytes(file).SequenceEqual(beforeFailure),"failed same-content explicit refresh clears prior success without changing TXT bytes");
            save.PerformClick();Application.DoEvents();
            largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();Application.DoEvents();
            Require(result.ForeColor==Color.White&&result.Font.SizeInPoints>=13.9f,"guide feedback follows enlarged high-contrast styling");
            CheckParticipantGuideResultLayout(result);CaptureGenericCheck(this,"ui-guide-result-accessible.png",7);
        }finally{
            Clipboard.Clear();address.Text=savedAddress;participantGuide.Text=savedGuide;result.Text=savedResult;File.WriteAllBytes(file,savedBytes);
            string discarded;while(queue.TryDequeue(out discarded)){}foreach(string line in savedQueue)queue.Enqueue(line);
            largeText.Checked=savedLarge;highContrast.Checked=savedContrast;ApplyAccessibleTheme();Size=savedSize;
            if(savedPreference==null)File.Delete(preference);else File.WriteAllBytes(preference,savedPreference);
            workspace.SelectedIndex=savedPage;if(savedFocus!=null&&savedFocus.CanFocus)savedFocus.Focus();Application.DoEvents();
        }
        Require(address.Text==savedAddress&&participantGuide.Text==savedGuide&&result.Text==savedResult&&File.ReadAllBytes(file).SequenceEqual(savedBytes)&&queue.ToArray().SequenceEqual(savedQueue),"guide feedback checks restore guide, address, export and log fixtures");
    }
    void CheckParticipantGuideResultLayout(Label result)
    {
        Application.DoEvents();var layout=(TableLayoutPanel)result.Parent;var actions=(FlowLayoutPanel)layout.GetControlFromPosition(0,2);
        Require(result.Visible&&result.Left>=0&&result.Right<=layout.ClientSize.Width&&result.Height>=result.GetPreferredSize(new Size(result.Width,0)).Height&&result.Bottom<=layout.ClientSize.Height,"guide feedback is fully readable in its allocated row: "+LayoutTrace(result)+" preferred="+result.GetPreferredSize(new Size(result.Width,0))+" parent="+layout.ClientSize+" rows="+string.Join(",",layout.RowStyles.Cast<RowStyle>().Select(row=>row.Height.ToString("0.##")).ToArray()));
        Require(actions.Bottom<=result.Top&&participantGuide.Parent.Bottom<=actions.Top,"guide text, actions and feedback do not overlap");
        foreach(Control c in actions.Controls)Require(c.Left>=0&&c.Top>=0&&c.Right<=actions.ClientSize.Width&&c.Bottom<=actions.ClientSize.Height,"participant guide action remains inside its row: "+LayoutTrace(c));
    }
    void SubmitConsoleCommandForCheck(bool enter)
    {
        Require(ManagerDataOverride!=null&&!timer.Enabled,"console submission checks use a synthetic fixture with its timer stopped");
        if(enter){
            var key=new KeyEventArgs(Keys.Enter);
            typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(command,new object[]{key});
            Require(key.SuppressKeyPress,"console Enter suppresses the input key");
        }else{
            var send=Descendants(workspace.TabPages[1]).OfType<Button>().Single(button=>button.Text=="送信");
            Require(send.Visible&&send.Enabled,"console send button is available on the visible console");send.PerformClick();
        }
    }
    void CheckRejectedConsoleDraft(string draft,bool enter,string state)
    {
        command.Text=draft;command.Select(2,4);
        int notices=queue.ToArray().Count(line=>line=="[案内] サーバー起動後に操作してください。"),echoes=queue.ToArray().Count(line=>line.StartsWith("> ",StringComparison.Ordinal));
        int selection=command.SelectionStart,length=command.SelectionLength;
        SubmitConsoleCommandForCheck(enter);
        Require(command.Text==draft,state+" console submission preserves the exact unsent draft via "+(enter?"Enter":"the send button"));
        Require(command.SelectionStart==selection&&command.SelectionLength==length,state+" rejection preserves the draft selection");
        Require(queue.ToArray().Count(line=>line=="[案内] サーバー起動後に操作してください。") == notices+1,state+" rejection retains the existing start-server notice");
        Require(queue.ToArray().Count(line=>line.StartsWith("> ",StringComparison.Ordinal))==echoes,state+" rejection does not report a sent command");
    }
    void CheckStoppedConsoleDrafts()
    {
        Require(ManagerDataOverride!=null&&!timer.Enabled&&!Running(server)&&!Running(tunnel),"console draft checks use a stopped synthetic fixture");
        string savedCommand=command.Text,savedText=log.Text,savedRecent=recentLogs.Text,savedResult=logSearchResult.Text;
        int savedCommandStart=command.SelectionStart,savedCommandLength=command.SelectionLength,savedStart=log.SelectionStart,savedLength=log.SelectionLength,savedRecentStart=recentLogs.SelectionStart,savedRecentLength=recentLogs.SelectionLength;
        int savedPage=workspace.SelectedIndex,savedFilter=logSourceFilter.SelectedIndex;
        var savedRecords=logRecords.ToArray();var savedQueue=queue.ToArray();
        string preference=Path.Combine(root,"manager-log-filter.txt");byte[] savedPreference=File.Exists(preference)?File.ReadAllBytes(preference):null;
        bool savedLarge=largeText.Checked,savedContrast=highContrast.Checked;Size savedSize=Size;
        Control savedFocus=Descendants(this).FirstOrDefault(control=>control.Focused);
        try{
            largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);workspace.SelectedIndex=1;Application.DoEvents();
            foreach(bool enter in new[]{false,true})foreach(string draft in new[]{"say Synthetic unsent draft","  /say Synthetic unsent draft  "}){
                CheckRejectedConsoleDraft(draft,enter,"stopped");CheckRejectedConsoleDraft(draft,enter,"repeated stopped");
            }
            foreach(bool enter in new[]{false,true})foreach(string empty in new[]{"","   ","///"}){
                command.Text=empty;var before=queue.ToArray();SubmitConsoleCommandForCheck(enter);
                Require(command.Text==empty&&queue.ToArray().SequenceEqual(before),"empty console submission leaves its text and queue unchanged");
            }
            logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            command.Text="say Synthetic unsent draft";int beforeNotice=queue.Count;SubmitConsoleCommandForCheck(false);
            AppendLogRecords(queue.ToArray().Skip(beforeNotice));CheckCommandRow();CaptureGenericCheck(this,"ui-console-unsent.png",1);
            largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();CheckRejectedConsoleDraft("say Synthetic unsent draft",true,"enlarged high-contrast stopped");
            CheckCommandRow();CaptureGenericCheck(this,"ui-console-unsent-accessible.png",1);
        }finally{
            largeText.Checked=savedLarge;highContrast.Checked=savedContrast;ApplyAccessibleTheme();Size=savedSize;
            logRecords.Clear();logRecords.AddRange(savedRecords);logSourceFilter.SelectedIndex=savedFilter;RebuildLogView();log.Text=savedText;log.Select(savedStart,savedLength);logSearchResult.Text=savedResult;
            recentLogs.Text=savedRecent;FitRecentLogScroll();recentLogs.Select(savedRecentStart,savedRecentLength);
            string discarded;while(queue.TryDequeue(out discarded)){}foreach(string line in savedQueue)queue.Enqueue(line);
            if(savedPreference==null)File.Delete(preference);else File.WriteAllBytes(preference,savedPreference);
            command.Text=savedCommand;command.Select(savedCommandStart,savedCommandLength);workspace.SelectedIndex=savedPage;
            if(savedFocus!=null&&savedFocus.CanFocus)savedFocus.Focus();Application.DoEvents();
        }
        Require(command.Text==savedCommand&&log.Text==savedText&&logRecords.SequenceEqual(savedRecords)&&queue.ToArray().SequenceEqual(savedQueue),"console draft checks restore input and log fixtures");
    }
    string LogSearchState(){return " selection="+log.SelectionStart+":"+log.SelectionLength+" selected="+log.SelectedText+" inputFocused="+searchText.Focused+" inputCanFocus="+searchText.CanFocus+" inputVisible="+searchText.Visible+" inputEnabled="+searchText.Enabled+" inputHandle="+searchText.IsHandleCreated+" inputDisposed="+searchText.IsDisposed+" parentVisible="+searchText.Parent.Visible+" parentEnabled="+searchText.Parent.Enabled+" parentHandle="+searchText.Parent.IsHandleCreated+" logFocused="+log.Focused+" active="+(ActiveControl==null?"null":ActiveControl.GetType().Name);}
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
        string savedCommand=command.Text;
        string savedRecent=recentLogs.Text;int savedRecentStart=recentLogs.SelectionStart,savedRecentLength=recentLogs.SelectionLength;
        string preference=Path.Combine(root,"manager-log-filter.txt");byte[] savedPreference=File.Exists(preference)?File.ReadAllBytes(preference):null;
        Control savedFocus=Descendants(this).FirstOrDefault(control=>control.Focused);
        bool savedLarge=largeText.Checked,savedContrast=highContrast.Checked;Size savedSize=Size;
        try{
            largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();Size=new Size(1024,720);
            workspace.SelectedIndex=1;logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
            AppendLogRecords(new[]{"[MC] Alpha first","[公開] Synthetic tunnel","[MC] alpha second"});
            log.Select(5,5);searchText.Text="";searchText.Focus();Application.DoEvents();
            string initialSearchState=LogSearchState();
            var search=Descendants(workspace.TabPages[1]).OfType<Button>().Single(button=>button.Text=="ログ内を検索");
            Require(search.Visible&&search.Enabled,"log search button is available on the visible console");
            search.PerformClick();Application.DoEvents();
            Require(log.SelectedText=="Alpha","empty log search preserves the existing selection;"+LogSearchState());
            Require(searchText.Focused,"empty log search keeps input focus; before="+initialSearchState+" after="+LogSearchState());
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
            CheckLogSearchShortcuts();
            CheckPreviousLogSearch();
        }finally{
            largeText.Checked=savedLarge;highContrast.Checked=savedContrast;ApplyAccessibleTheme();Size=savedSize;
            logRecords.Clear();logRecords.AddRange(savedRecords);logSourceFilter.SelectedIndex=savedFilter;RebuildLogView();
            log.Text=savedText;searchText.Text=savedQuery;log.Select(savedStart,savedLength);logSearchResult.Text=savedResult;command.Text=savedCommand;
            recentLogs.Text=savedRecent;FitRecentLogScroll();recentLogs.Select(savedRecentStart,savedRecentLength);
            if(savedPreference==null)File.Delete(preference);else File.WriteAllBytes(preference,savedPreference);
            workspace.SelectedIndex=savedPage;if(savedFocus!=null&&savedFocus.CanFocus)savedFocus.Focus();Application.DoEvents();
        }
        Require(log.Text==savedText&&logRecords.SequenceEqual(savedRecords)&&searchText.Text==savedQuery&&log.SelectionStart==savedStart&&log.SelectionLength==savedLength&&logSearchResult.Text==savedResult&&logSourceFilter.SelectedIndex==savedFilter,"log search checks restore the original records, query, selection, feedback and source filter");
    }
    bool InvokeLogSearchShortcut(Control source,Keys keys)
    {
        var message=Message.Create(source.Handle,0x100,(IntPtr)(int)(keys&Keys.KeyCode),IntPtr.Zero);
        var method=typeof(Control).GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        return (bool)method.Invoke(source,new object[]{message,keys});
    }
    void CheckLogSearchShortcuts()
    {
        // Exercise WinForms command-key bubbling from child controls with synthetic key data.
        // This does not inject physical keyboard events or change the system clipboard.
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();
        workspace.SelectedIndex=1;logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
        AppendLogRecords(new[]{"[MC] Alpha first","[公開] Synthetic hidden tunnel","[MC] alpha second"});
        searchText.Text="alpha";log.Select(5,5);command.Text="say Synthetic unsent draft";command.Focus();Application.DoEvents();
        string text=log.Text;var records=logRecords.ToArray();
        Require(InvokeLogSearchShortcut(command,Keys.Control|Keys.F),"Ctrl+F from the command input is handled");Application.DoEvents();
        Require(workspace.SelectedIndex==1&&searchText.Focused&&searchText.SelectedText=="alpha","Ctrl+F focuses and selects the existing search query");
        Require(log.Text==text&&log.SelectionStart==5&&log.SelectedText=="Alpha"&&logRecords.SequenceEqual(records)&&command.Text=="say Synthetic unsent draft"&&logSourceFilter.SelectedIndex==0,"opening search preserves logs, log selection, the source filter and the unsent command");
        Require(InvokeLogSearchShortcut(searchText,Keys.Control|Keys.F)&&searchText.SelectedText=="alpha","repeated Ctrl+F keeps the query selected");
        CaptureGenericCheck(this,"ui-log-shortcut-query.png",1);
        int second=log.Text.IndexOf("alpha second",StringComparison.Ordinal);
        Require(InvokeLogSearchShortcut(searchText,Keys.F3),"F3 from the query is handled");Application.DoEvents();
        Require(log.SelectionStart==second&&log.SelectedText=="alpha"&&log.Focused,"F3 advances from the existing log selection");
        Require(InvokeLogSearchShortcut(log,Keys.F3),"F3 from the readonly log is handled");Application.DoEvents();
        Require(log.SelectionStart==5&&log.SelectedText=="Alpha"&&logSearchResult.Text=="先頭に戻り、一致する箇所を選択しました。","F3 wraps through the same search path");
        foreach(var keys in new[]{Keys.Control|Keys.F3,Keys.Control|Keys.Shift|Keys.F3,Keys.Alt|Keys.F3,Keys.Alt|Keys.F,Keys.Control|Keys.Shift|Keys.F}){
            InvokeLogSearchShortcut(log,keys);Application.DoEvents();
            Require(workspace.SelectedIndex==1&&log.SelectionStart==5&&log.SelectedText=="Alpha"&&command.Text=="say Synthetic unsent draft","other shortcut modifiers do not search or change the command");
        }
        workspace.SelectedIndex=3;motdSetting.Focus();Application.DoEvents();
        InvokeLogSearchShortcut(motdSetting,Keys.F3);Application.DoEvents();
        Require(workspace.SelectedIndex==3&&log.SelectionStart==5&&searchText.Text=="alpha","F3 on another page does not navigate or search");
        Require(InvokeLogSearchShortcut(motdSetting,Keys.Control|Keys.F),"Ctrl+F from settings bubbles to the manager");Application.DoEvents();
        Require(workspace.SelectedIndex==1&&searchText.Focused&&searchText.SelectedText=="alpha","Ctrl+F opens the console from another page");
        logSourceFilter.SelectedIndex=1;log.Select(5,5);searchText.Text="tunnel";
        Require(InvokeLogSearchShortcut(searchText,Keys.F3),"F3 handles a query for a hidden source");Application.DoEvents();
        Require(log.SelectedText=="Alpha"&&searchText.Focused&&logSearchResult.Text=="表示中のログに見つかりません。","shortcut search respects the displayed source filter");
        searchText.Text="";Require(InvokeLogSearchShortcut(searchText,Keys.F3),"F3 handles an empty query");Application.DoEvents();
        Require(log.SelectedText=="Alpha"&&searchText.Focused&&logSearchResult.Text=="検索語を入力してください。","F3 explains an empty query without replacing the log selection");
        largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();command.Focus();
        Require(InvokeLogSearchShortcut(command,Keys.Control|Keys.F),"Ctrl+F remains available with enlarged high-contrast text");Application.DoEvents();
        Require(searchText.Focused&&searchText.ForeColor==Color.White,"shortcut search retains accessible focus and contrast");
        CheckLogSearchLayout();CaptureGenericCheck(this,"ui-log-shortcut-accessible.png",1);
    }
    void CheckPreviousLogSearch()
    {
        largeText.Checked=false;highContrast.Checked=false;ApplyAccessibleTheme();
        workspace.SelectedIndex=1;logRecords.Clear();logSourceFilter.SelectedIndex=0;RebuildLogView();
        AppendLogRecords(new[]{"[MC] Alpha first","[公開] Synthetic hidden tunnel","[MC] alpha last"});
        string text=log.Text;var records=logRecords.ToArray();string draft="say Synthetic reverse-search draft";command.Text=draft;
        int first=text.IndexOf("Alpha",StringComparison.Ordinal),last=text.IndexOf("alpha last",StringComparison.Ordinal);
        searchText.Text="alpha";log.Select(last,5);command.Focus();Application.DoEvents();
        Require(InvokeLogSearchShortcut(command,Keys.Shift|Keys.F3),"Shift+F3 from command input is handled on the console");Application.DoEvents();
        Require(log.SelectionStart==first&&log.SelectedText=="Alpha"&&log.Focused&&logSearchResult.Text=="一致する箇所を選択しました。","previous search excludes the current match and ignores case");
        Require(InvokeLogSearchShortcut(log,Keys.Shift|Keys.F3),"Shift+F3 from the readonly log is handled");Application.DoEvents();
        Require(log.SelectionStart==last&&log.SelectedText=="alpha"&&logSearchResult.Text=="末尾に戻り、一致する箇所を選択しました。","previous search wraps from the first match to the last and explains it");
        CaptureGenericCheck(this,"ui-log-search-previous-wrapped.png",1);
        Require(InvokeLogSearchShortcut(log,Keys.F3),"forward search still works after a previous search");Application.DoEvents();
        Require(log.SelectionStart==first&&logSearchResult.Text=="先頭に戻り、一致する箇所を選択しました。","changing direction returns to the first match through the existing forward path");

        log.Select(log.TextLength,0);Require(InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3),"previous search accepts an end-of-log caret");Application.DoEvents();
        Require(log.SelectionStart==last&&logSearchResult.Text=="一致する箇所を選択しました。","end-of-log caret searches backward without wrapping");
        log.Select(0,0);InvokeLogSearchShortcut(log,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.SelectionStart==last&&logSearchResult.Text=="末尾に戻り、一致する箇所を選択しました。","zero caret wraps without a negative search index");
        searchText.Text="alpha last";log.Select(1,0);InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.SelectionStart==last&&log.SelectedText=="alpha last"&&logSearchResult.Text=="末尾に戻り、一致する箇所を選択しました。","query longer than the prefix wraps to the complete last match");
        Require(log.Text==text&&logRecords.SequenceEqual(records)&&command.Text==draft&&logSourceFilter.SelectedIndex==0,"reverse shortcuts preserve logs, source filter and unsent commands");

        searchText.Text="alpha";log.Select(first,5);workspace.SelectedIndex=3;motdSetting.Focus();Application.DoEvents();
        InvokeLogSearchShortcut(motdSetting,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(workspace.SelectedIndex==3&&log.SelectionStart==first&&searchText.Text=="alpha","Shift+F3 on another page does not navigate or search");
        workspace.SelectedIndex=1;logSourceFilter.SelectedIndex=1;Application.DoEvents();
        Require(log.SelectionStart==log.TextLength&&log.SelectionLength==0&&logSearchResult.Text=="","changing the source filter rebuilds the log with a clean end caret");
        searchText.Text="alpha";logSourceFilter.Focus();Application.DoEvents();
        Require(InvokeLogSearchShortcut(logSourceFilter,Keys.Shift|Keys.F3),"Shift+F3 bubbles from the source filter");Application.DoEvents();
        Require(log.SelectionStart==log.Text.IndexOf("alpha last",StringComparison.Ordinal)&&log.SelectedText=="alpha"&&logSearchResult.Text=="一致する箇所を選択しました。","reverse search after filtering finds the last visible match from the rebuilt end caret");
        var searchButton=Descendants(workspace.TabPages[1]).OfType<Button>().Single(button=>button.Text=="ログ内を検索");searchButton.Focus();Application.DoEvents();
        Require(InvokeLogSearchShortcut(searchButton,Keys.Shift|Keys.F3),"Shift+F3 bubbles from the search button");Application.DoEvents();
        Require(log.SelectionStart==first&&log.SelectedText=="Alpha"&&logSearchResult.Text=="一致する箇所を選択しました。","reverse search steps to the previous visible match after source reflow");
        Require(logRecords.SequenceEqual(records)&&command.Text==draft&&logSourceFilter.SelectedIndex==1&&!log.Text.Contains("tunnel"),"filtered reverse search preserves records, filter and unsent draft");
        log.Select(first,5);searchText.Text="tunnel";
        Require(InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3),"previous search handles a hidden-source query");Application.DoEvents();
        Require(log.SelectedText=="Alpha"&&searchText.Focused&&logSearchResult.Text=="表示中のログに見つかりません。","previous search respects source filters without losing selection");
        searchText.Text="";InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.SelectedText=="Alpha"&&searchText.Focused&&logSearchResult.Text=="検索語を入力してください。","previous search explains an empty query and preserves selection");
        searchText.Text="[MC]";log.Select(log.TextLength,0);InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.SelectedText=="[MC]"&&log.SelectionStart==log.Text.LastIndexOf("[MC]",StringComparison.Ordinal),"previous search treats punctuation as literal text");
        logRecords.Clear();RebuildLogView();searchText.Text="alpha";InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.Text==""&&log.SelectionStart==0&&searchText.Focused&&logSearchResult.Text=="表示中のログに見つかりません。","previous search on an empty log reports no match without an invalid index");

        logSourceFilter.SelectedIndex=0;AppendLogRecords(new[]{"Alphaalpha"});searchText.Text="alpha";log.Select(5,5);
        for(int repeat=0;repeat<3;repeat++){
            InvokeLogSearchShortcut(log,Keys.Shift|Keys.F3);Application.DoEvents();
            Require(log.SelectionStart==0&&log.SelectedText=="Alpha"&&logSearchResult.Text=="一致する箇所を選択しました。","previous search includes the adjacent complete match at index zero");
            InvokeLogSearchShortcut(log,Keys.F3);Application.DoEvents();
            Require(log.SelectionStart==5&&log.SelectedText=="alpha"&&logSearchResult.Text=="一致する箇所を選択しました。","repeated direction changes retain adjacent matches without skipping");
        }
        log.Select(2,0);InvokeLogSearchShortcut(log,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.SelectionStart==5&&log.SelectedText=="alpha"&&logSearchResult.Text=="末尾に戻り、一致する箇所を選択しました。","a caret inside a match excludes the incomplete prefix and wraps");
        logRecords.Clear();RebuildLogView();AppendLogRecords(new[]{"Alpha"});log.Select(0,5);
        for(int repeat=0;repeat<2;repeat++){
            InvokeLogSearchShortcut(log,Keys.Shift|Keys.F3);Application.DoEvents();
            Require(log.SelectionStart==0&&log.SelectedText=="Alpha"&&logSearchResult.Text=="末尾に戻り、一致する箇所を選択しました。","a single match wraps backward to itself repeatedly");
            InvokeLogSearchShortcut(log,Keys.F3);Application.DoEvents();
            Require(log.SelectionStart==0&&log.SelectedText=="Alpha"&&logSearchResult.Text=="先頭に戻り、一致する箇所を選択しました。","a single match still wraps forward to itself");
        }
        Require(command.Text==draft&&logSourceFilter.SelectedIndex==0,"boundary searches preserve the unsent draft and filter");
        logRecords.Clear();RebuildLogView();

        logSourceFilter.SelectedIndex=0;AppendLogRecords(new[]{"[MC] Alpha first","[MC] alpha last"});searchText.Text="alpha";log.Select(first,5);
        largeText.Checked=true;highContrast.Checked=true;ApplyAccessibleTheme();InvokeLogSearchShortcut(searchText,Keys.Shift|Keys.F3);Application.DoEvents();
        Require(log.SelectedText=="alpha"&&logSearchResult.Text=="末尾に戻り、一致する箇所を選択しました。"&&logSearchResult.ForeColor==Color.White,"previous-search wrap feedback retains enlarged high contrast");
        CheckLogSearchLayout();CaptureGenericCheck(this,"ui-log-search-previous-accessible.png",1);
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
