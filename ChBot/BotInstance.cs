using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace ChBot
{
    public partial class BotInstance : Form
    {
        public string InstanceName;
        public BotContext context;
        public bool toClose = false;
        public InstanceManager manager = null;
        public bool disableEvents = false;
        public SearchConditionsForm searchConditionsForm;

        //コンストラクタ
        public BotInstance(string name, InstanceManager manager)
        {
            InitializeComponent();
            comboBox1.Items.AddRange(Network.DeviceIDList.ToArray());
            comboBox1.SelectedIndex = 1;
            InstanceName = name;
            this.manager = manager;
            context = new BotContext(this);
            searchConditionsForm = new SearchConditionsForm(context);
            UpdateUI();
        }

        //UIを更新
        public void UpdateUI(UIParts part = UIParts.List | UIParts.Other, bool forceRefreshListView = false)
        {
            if ((part & UIParts.Other) == UIParts.Other)
                DisplaySetting();

            if ((part & UIParts.List) == UIParts.List)
            {
                ListUp(forceRefreshListView);
            }
        }

        public enum UIParts : byte
        {
            List = 1,
            Other = 2
        }

        //プロキシ一覧を表示
        private void ListUpProxy()
        {
            var refresh = false;
            var shownClinetList = new List<BotClient>();
            foreach (ListViewItem item in listView1.Items)
                shownClinetList.Add((BotClient)item.Tag);
            if (shownClinetList.Count != context.ClientList.Count)
            {
                refresh = true;
            }
            else
            {
                for (var i = 0; i < shownClinetList.Count; i++)
                {
                    if (shownClinetList[i] != context.ClientList[i])
                    {
                        refresh = true;
                        break;
                    }
                }
            }
            if (refresh)
            {
                listView1.Items.Clear();
                foreach (var client in context.ClientList)
                {
                    ListViewItem item = new ListViewItem(new[] { Network.DeviceIDList[client.DeviceIndex], client.PostCount.ToString(), client.Attempt.ToString() + "/10" });
                    item.BackColor = client.Working ? Color.Red : Color.White;
                    item.ForeColor = client.Working ? Color.White : Color.Black;
                    item.Tag = client;
                    listView1.Items.AddRange(new[] { item });
                }
            }
            else
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    var client = (BotClient)item.Tag;
                    item.BackColor = client.Working ? Color.Red : Color.White;
                    item.ForeColor = client.Working ? Color.White : Color.Black;
                    item.SubItems[0].Text = Network.DeviceIDList[client.DeviceIndex];
                    item.SubItems[1].Text = client.PostCount.ToString();
                    item.SubItems[2].Text = client.Attempt.ToString() + "/10";
                }
            }
        }

        //開始停止ボタン
        private async void StartStopButton_Click(object sender, EventArgs e)
        {
            try
            {
                StartStopButton.Enabled = false;
                if (context.Working)
                {
                    try
                    {
                        await context.StopAttack();
                    }
                    catch (Exception er)
                    {
                        MessageBox.Show("停止失敗\n" + er.Message);
                    }
                }
                else
                {
                    try
                    {
                        await context.StartAttack();
                    }
                    catch (Exception er)
                    {
                        MessageBox.Show("開始失敗\n" + er.Message);
                        await context.StopAttack();
                    }
                }
            }
            finally
            {
                StartStopButton.Enabled = true;
            }

            UpdateUI();
            manager.UpdateUI();
        }

        //検索ボタン
        private async void SearchButton_Click(object sender, EventArgs e)
        {
            SearchButton.Enabled = false;
            try
            {
                context.everMatchList.Clear();
                await context.MiddleSearchThread();
            }
            catch (Exception er)
            {
                MessageBox.Show("エラー\n" + er.Message);
            }
            SearchButton.Enabled = true;
            UpdateUI(forceRefreshListView: true);
        }

        //リストビューに表示
        private void ListUp(bool forceRefresh = false)
        {
            var list = new BotThreadList();
            if (context.ListMode == BotContext.ListModes.Search)
                list = context.ThreadContext.SearchResult;
            else if (context.ListMode == BotContext.ListModes.History)
                list = context.ThreadContext.GetHistory();

            var existsChange = true;
            if (ThreadListListView.Tag != null)
            {
                var tag = (object[])ThreadListListView.Tag;
                var prevListMode = (BotContext.ListModes)tag[0];
                var prevList = (BotThreadList)tag[1];
                if (prevListMode == context.ListMode && prevList.Count == list.Count)
                {
                    existsChange = prevList.Zip(list, (item1, item2) => new[] { item1, item2 }).Any(item => !item[0].Equals(item[1]) || item[0].Rank != item[1].Rank || item[0].Res != item[1].Res);
                }
            }

            ThreadListListView.Tag = new object[] { context.ListMode, list.Clone() };

            if (existsChange || forceRefresh)
            {
                ThreadListListView.Items.Clear();
                foreach (BotThread thread in list)
                {
                    ListViewItem item = null;
                    if (context.ListMode == BotContext.ListModes.Search)
                    {
                        item = new ListViewItem(new[] { thread.Rank.ToString(), thread.Title, thread.Res.ToString() });
                    }
                    else if (context.ListMode == BotContext.ListModes.History)
                    {
                        item = new ListViewItem(new[] { null, thread.Title, null });
                    }
                    item.Tag = thread;
                    ThreadListListView.Items.AddRange(new[] { item });
                }
            }

            ColorThread();
        }

        //スレ覧を色付け
        public void ColorThread()
        {
            foreach (ListViewItem item in ThreadListListView.Items)
            {
                item.BackColor = SystemColors.Window;
                item.ForeColor = SystemColors.WindowText;
                item.Font = new Font(item.Font, FontStyle.Regular);

                if (context.ThreadContext.IsEnabled((BotThread)item.Tag))
                {
                    item.BackColor = Color.Red;
                    item.ForeColor = Color.White;
                }

                if (context.ThreadContext.IsIgnored((BotThread)item.Tag))
                {
                    item.BackColor = Color.Silver;
                    item.ForeColor = Color.White;
                }

                if (context.ClientList.Any(client => client.current != null && client.current.Equals((BotThread)item.Tag)))
                {
                    item.BackColor = Color.Yellow;
                    item.ForeColor = Color.Red;
                }

                if (((BotThread)item.Tag).Priority)
                {
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }
            }
        }

        //全スレ有効解除
        private void ClearEnabledButton_Click(object sender, EventArgs e)
        {
            context.ThreadContext.ClearEnabled();
            UpdateUI();
        }

        //全スレ無視解除
        private void ClearIgnoredButton_Click(object sender, EventArgs e)
        {
            context.ThreadContext.ClearIgnored();
            UpdateUI();
        }

        //履歴クリア
        private void ClearHistoryButton_Click(object sender, EventArgs e)
        {
            context.ThreadContext.ClearHistory();
            UpdateUI();
        }

        //選択スレを無視
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            context.ThreadContext.AddIgnored(GetSelectedThreads());
            UpdateUI();
        }

        //ブラウザで開く
        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            BotThread thread = GetSelectedThreads().First;

            if (thread != null)
            {
                string url = thread.Url;
                Process.Start(url);
            }
        }

        //URLをコピー
        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            BotThread thread = GetSelectedThreads().First;

            if (thread != null)
            {
                try
                {
                    Clipboard.SetText(thread.Url);
                }
                catch
                {
                    MessageBox.Show("クリップボードへのコピーに失敗しました。");
                }
            }
        }

        //選択スレを無視解除
        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            context.ThreadContext.RemoveIgnored(GetSelectedThreads());
            UpdateUI();
        }

        //選択スレを有効
        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            context.ThreadContext.AddEnabled(GetSelectedThreads());
            UpdateUI();
        }

        //選択スレを有効解除
        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            context.ThreadContext.RemoveEnabled(GetSelectedThreads());
            UpdateUI();
        }

        //JaneStyleで開く
        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            BotThread thread = GetSelectedThreads().First;

            if (thread != null)
            {
                while (true)
                {
                    if (context.JanePath == "")
                    {
                        if (SelectJaneStyleFolderFolderBrowserDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            context.JanePath = SelectJaneStyleFolderFolderBrowserDialog.SelectedPath;
                        }
                        else
                        {
                            break;
                        }
                    }

                    try
                    {
                        Process.Start(context.JanePath + "\\Jane2ch.exe", thread.Url);
                        break;
                    }
                    catch
                    {
                        MessageBox.Show("JaneStyleのフォルダの設定が正しくありません。");
                        context.JanePath = "";
                    }
                }
            }
        }

        //DAT取得
        private async void toolStripMenuItem9_Click(object sender, EventArgs e)
        {
            var thread = GetSelectedThreads().First;

            try
            {
                if (thread != null)
                {
                    var viewer = new ThreadViewer(context);
                    await viewer.Open(thread);
                }
            }
            catch (Exception er)
            {
                MessageBox.Show("エラー\n" + er.Message);
            }
        }

        //選択スレを取得
        private BotThreadList GetSelectedThreads()
        {
            BotThreadList selectedThreads = new BotThreadList();
            foreach (ListViewItem item in ThreadListListView.SelectedItems)
            {
                selectedThreads.Add((BotThread)item.Tag);
            }
            return selectedThreads;
        }

        //状態を表示
        private void DisplaySetting()
        {
            disableEvents = true;

            context.Loginer.Visible = Visible && context.Loginer.Logining;
            Text = InstanceName + (context.Loginer.Logining ? " - ログイン中" : "");
            splitContainer1.Enabled = !context.Loginer.Logining;
            MessageTextBox.Text = context.Message;
            BoardTextBox.Text = context.Board;
            MailTextBox.Text = context.Mail;
            NameTextBox.Text = context.Name;
            IntervalNumericUpDown.Value = context.Interval;
            UserNameTextBox.Text = context.User;
            PassMaskedTextBox.Text = context.Password;
            SelectJaneStyleFolderFolderBrowserDialog.SelectedPath = context.JanePath;
            SearchResultRadioButton.Checked = context.ListMode == BotContext.ListModes.Search;
            HistoryRadioButton.Checked = context.ListMode == BotContext.ListModes.History;
            textBox2.Text = context.HomeIP;

            searchConditionsForm.UpdateUI();

            button11.Visible = true;

            splitContainer5.Panel1Collapsed = true;

            if (context.Working)
            {
                StartStopButton.Text = "停止";
                StartStopButton.BackColor = Color.Red;
                StartStopButton.ForeColor = Color.White;
            }
            else
            {
                StartStopButton.Text = "開始";
                StartStopButton.BackColor = SystemColors.Control;
                StartStopButton.ForeColor = SystemColors.ControlText;
            }

            foreach (var client in context.ClientList)
                client.Text = InstanceName + ": " + Network.DeviceIDList[client.DeviceIndex];

            context.Loginer.Text = InstanceName + ": " + "ログイン";

            ListUpProxy();

            disableEvents = false;
        }

        //フォームを閉じた時
        private async void BotInstance_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (toClose)
            {
                await context.StopAttack();
                context.Loginer.toClose = true;
                context.Loginer.Close();
            }
            else
            {
                e.Cancel = true;
                Hide();
                foreach (var client in context.ClientList)
                    client.Hide();
                context.Loginer.Hide();
            }
        }

        //浪人ログイン・ログアウト
        private void LoginButton_Click(object sender, EventArgs e)
        {
            context.Login();

            UpdateUI(UIParts.Other);
            manager.UpdateUI();
        }

        //キーイベント
        private void ThreadListListView_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Enter)
            {
                if (e.KeyCode == Keys.Delete)
                {
                    //選択スレを有効解除
                    context.ThreadContext.RemoveEnabled(GetSelectedThreads());
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    //選択スレを有効
                    context.ThreadContext.AddEnabled(GetSelectedThreads());
                }
                UpdateUI();
            }
        }

        private async Task<string[]> getRandomReses(int num)
        {
            BotThreadList all = await Network.GetThreadList(context.Board);
            List<string> list = new List<string>();
            Random r = new Random((int)(DateTime.Now.ToFileTime() % int.MaxValue));
            while (list.Count < num)
            {
                string[] dat = await Network.GetResList(all[r.Next(all.Count)], context.ApiSid);
                if (dat.Length >= 3)
                {
                    string res = dat[r.Next(1, dat.Length)];
                    if (res.Length <= 50)
                    {
                        Regex r1 = new Regex(@">>\d+");
                        Regex r2 = new Regex(@"\n");
                        Regex r3 = new Regex(@"http");
                        if (!(r1.IsMatch(res) || r2.IsMatch(res) || r3.IsMatch(res)))
                        {
                            list.Add(res);
                        }
                    }
                }
            }
            return list.ToArray();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                BotClient client = listView1.SelectedItems[0].Tag as BotClient;
                client.Show();
                client.BringToFront();
                client.WindowState = FormWindowState.Normal;
            }
            catch
            {

            }
        }

        private void IntervalNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.Interval = (int)IntervalNumericUpDown.Value;
        }

        private void MessageTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.Message = MessageTextBox.Text;
        }

        private void BoardTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.Board = BoardTextBox.Text;
            UpdateUI(UIParts.Other);
        }

        private void NameTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.Name = NameTextBox.Text;
        }

        private void MailTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.Mail = MailTextBox.Text;
        }

        private void UserNameTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.User = UserNameTextBox.Text;
        }

        private void PassMaskedTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.Password = PassMaskedTextBox.Text;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            context.SaveSettings();
            MessageBox.Show("保存しました。");
        }

        private async void button8_Click(object sender, EventArgs e)
        {
            button8.Enabled = false;

            try
            {
                await context.SearchThread();
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }

            button8.Enabled = true;
        }

        private async void button9_Click(object sender, EventArgs e)
        {
            button9.Enabled = false;

            try
            {
                BotThread thread = GetSelectedThreads().First;

                if (thread != null)
                    await Network.GetDat(thread, context.ApiSid);
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }

            button9.Enabled = true;
        }

        private async void button10_Click(object sender, EventArgs e)
        {
            button10.Enabled = false;

            try
            {
                BotThread thread = GetSelectedThreads().First;

                if (thread != null)
                    await Network.Post(thread, UnixTime.Now().ToString(), context.Name, context.Mail, context.UAMonaKeyPair, comboBox1.SelectedIndex - 1);
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
                if (er as PostFailureException != null)
                    MessageBox.Show(((PostFailureException)er).Result);
            }

            button10.Enabled = true;
        }

        private void SearchResultRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.ListMode = BotContext.ListModes.Search;
            UpdateUI();
        }

        private void HistoryRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.ListMode = BotContext.ListModes.History;
            UpdateUI();
        }

        private void BotInstance_Activated(object sender, EventArgs e)
        {
            BringToFront();
            StartStopButton.Focus();

            if (context.Loginer.Logining)
            {
                context.Loginer.Show();
                context.Loginer.Focus();
            }
        }

        private void BotInstance_Shown(object sender, EventArgs e)
        {
            BringToFront();
            StartStopButton.Focus();

            if (context.Loginer.Logining)
            {
                context.Loginer.Show();
                context.Loginer.Focus();
            }
        }

        private async void button11_Click(object sender, EventArgs e)
        {
            button11.Enabled = false;
            try
            {
                await context.SetNewMessage();
            }
            catch (Exception er)
            {
                MessageBox.Show("エラー\n" + er.Message);
            }
            button11.Enabled = true;
            UpdateUI(UIParts.Other);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            context.HomeIP = textBox2.Text.Trim();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            context.Loginer.Login();
        }

        private async void BodySearchButton_Click(object sender, EventArgs e)
        {
            BodySearchButton.Enabled = false;
            try
            {
                context.everMatchList.Clear();
                await context.FullSearchThread();
            }
            catch (Exception er)
            {
                MessageBox.Show("エラー\n" + er.Message);
            }
            BodySearchButton.Enabled = true;
            UpdateUI(forceRefreshListView: true);
        }

        private void OpenSearchConditionsButton_Click(object sender, EventArgs e)
        {
            searchConditionsForm.Show();
            searchConditionsForm.BringToFront();
            searchConditionsForm.WindowState = FormWindowState.Normal;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            try
            {
                MessageBox.Show(await Network.GetIPAddress(comboBox1.SelectedIndex - 1));
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }
            button1.Enabled = true;
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;

            try
            {
                BotThread thread = GetSelectedThreads().First;

                if (thread != null)
                    await Network.Post(thread, context.Message, context.Name, context.Mail, context.UAMonaKeyPair, comboBox1.SelectedIndex - 1);
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
                if (er as PostFailureException != null)
                    MessageBox.Show(((PostFailureException)er).Result);
            }

            button3.Enabled = true;
        }

        private async void button12_Click(object sender, EventArgs e)
        {
            button12.Enabled = false;
            try
            {
                await Network.SendLineMessage("test");
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }

            button12.Enabled = true;
        }

        private async void button14_Click(object sender, EventArgs e)
        {
            button14.Enabled = false;
            try
            {
                MessageBox.Show(await Network.GetLineMessage());
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }

            button14.Enabled = true;
        }

        private async void button15_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
            {
                MessageBox.Show("デバイスを選んでください");
                return;
            }

            button15.Enabled = false;
            await Network.RestartUsb(comboBox1.SelectedIndex - 1);
            button15.Enabled = true;
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
            {
                MessageBox.Show("デバイスを選んでください");
                return;
            }

            button7.Enabled = false;
            await Network.ChangeIP(comboBox1.SelectedIndex - 1);
            button7.Enabled = true;
        }

        private async void button16_Click(object sender, EventArgs e)
        {
            button16.Enabled = false;

            try
            {
                var UA = Network.GetRandomUseragent(BotUA.ChMate);
                var Mona = await Network.GetMonaKey(UA, comboBox1.SelectedIndex - 1);
                context.UAMonaKeyPair = new BotUAMonaKeyPair(UA, Mona, false);
                MessageBox.Show(UA + "\n" + Mona);
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
                if (er as PostFailureException != null)
                    MessageBox.Show(((PostFailureException)er).Result);
            }

            button16.Enabled = true;
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;

            try
            {
                await context.GatherMonaKey();
                MessageBox.Show(context.UAMonaKeyPairs.Count + "個取得");
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }

            button2.Enabled = true;
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            button4.Enabled = false;

            try
            {
                await Network.Build("mi.5ch.net", "news4vip", UnixTime.Now().ToString(), UnixTime.Now().ToString(), context.Name, context.Mail, context.UAMonaKeyPair, comboBox1.SelectedIndex - 1);
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
                if (er as PostFailureException != null)
                    MessageBox.Show(((PostFailureException)er).Result);
            }

            button4.Enabled = true;
        }

        private void button18_Click(object sender, EventArgs e)
        {
            context.LoadSettings();
            MessageBox.Show("ロードしました。");
            UpdateUI(UIParts.Other);
        }

        private void button19_Click(object sender, EventArgs e)
        {
            var exist = context.ClientList.Any(client => client.DeviceIndex == comboBox1.SelectedIndex - 1);
            if (!exist)
            {
                context.ClientList.Add(new BotClient(this, context, comboBox1.SelectedIndex - 1));
                UpdateUI(UIParts.Other);
            }
            else
            {
                MessageBox.Show("既に存在します。");
            }
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            button5.Enabled = false;

            try
            {
                await context.FillMonaKey();
                MessageBox.Show("計" + context.UAMonaKeyPairs.Count + "個");
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }

            button5.Enabled = true;
        }

        private async void button17_Click(object sender, EventArgs e)
        {
            var selected = listView1.SelectedItems;
            if (selected.Count == 1)
            {
                var delProxies = new List<BotClient>();
                foreach (var client in context.ClientList)
                {
                    if (((BotClient)selected[0].Tag) != client)
                    {
                        delProxies.Add(client);
                    }
                    else
                    {
                        await client.Stop();
                        client.Close();
                    }
                }
                context.ClientList = delProxies;
            }
            UpdateUI(UIParts.Other);
        }

        private void button13_Click(object sender, EventArgs e)
        {
            context.everMatchList.Clear();
        }
    }
}