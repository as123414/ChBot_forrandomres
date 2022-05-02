using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace ChBot
{
    public partial class BotClient : Form
    {
        public int Attempt { get; private set; }
        public double Count { get; private set; }
        public bool Working { get; private set; }
        public BotInstance ui;
        public BotContext context;
        public bool proceccing;
        public long processStartTime = 0;
        public long searchProcessStartTime = 0;
        public int SearchCount;
        public int searchAttempt = 0;
        public Dictionary<BotThread, double> powerList = new Dictionary<BotThread, double>();
        public bool searchProccesing = false;
        public List<BotThread> notifiedThreads = new List<BotThread>();
        public bool hasLastChance = true;
        public bool hasLastChance2 = true;
        public Exception lastRefreshException = null;

        public BotClient(BotInstance ui, BotContext context)
        {
            InitializeComponent();

#if DEBUG
            button2.Visible = true;
#endif

            Attempt = 0;
            Count = context.Interval;
            SearchCount = 60;
            Working = false;
            this.ui = ui;
            this.context = context;
            proceccing = false;
            label6.Text = Attempt.ToString() + "/10";
            RestTimeLabel.Text = Count.ToString();
            label9.Text = SearchCount.ToString();
        }

        public void Start()
        {
            if (Working)
                return;

            try
            {
                Attempt = 0;
                hasLastChance = true;
                hasLastChance2 = true;
                Count = context.Interval;
                Working = true;
                timer1.Enabled = true;
                SearchCount = 1;
                timer2.Enabled = true;
                button1.Text = "停止";
                button1.BackColor = Color.Red;
                button1.ForeColor = Color.White;
            }
            catch
            {
                Working = false;
                timer1.Enabled = false;
                timer2.Enabled = false;
                button1.Text = "開始";
                button1.BackColor = SystemColors.Control;
                button1.ForeColor = SystemColors.ControlText;
                throw;
            }
        }

        public async Task Stop()
        {
            if (!Working)
                return;

            while (proceccing || searchProccesing)
                await Task.Delay(100);

            timer1.Enabled = false;
            timer2.Enabled = false;
            Working = false;
            button1.Text = "開始";
            button1.BackColor = SystemColors.Control;
            button1.ForeColor = SystemColors.ControlText;
        }

        private void WriteResult(string result)
        {
            ResultWebBrowser.DocumentText = result;
            ui.ResultWebBrowser.DocumentText = result;
        }

        private void Client_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (Working)
            {
                await Stop();
            }
            else
            {
                Start();
            }

            ui.UpdateUI();
            ui.manager.UpdateUI();
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            Count -= 0.1;
            Count = Count < 0 ? 0 : Count;
            RestTimeLabel.Text = Count.ToString();
            ui.UpdateUI(BotInstance.UIParts.Other);

            var startTime = UnixTime.Now();

            if (Count <= 0)
            {
                if (!proceccing)
                {
                    await Proccess();
                    Count = context.Interval;
                }
                else
                {
                    Count = 0;
                }

                RestTimeLabel.Text = Count.ToString();
                ui.UpdateUI(BotInstance.UIParts.Other);
            }

            if (UnixTime.Now() - startTime < 2)
            {
                timer1.Enabled = false;
                await Task.Delay(1000);
                timer1.Enabled = true;
            }
        }

        private async void timer2_Tick(object sender, EventArgs e)
        {
            SearchCount--;
            SearchCount = SearchCount < 0 ? 0 : SearchCount;
            label9.Text = SearchCount.ToString();

            if (SearchCount == 0)
            {
                timer2.Enabled = false;
                try
                {
                    await RefreshThread();
                }
                catch (Exception er)
                {
                    WriteLog(er.Message);
                    searchAttempt++;
                    lastRefreshException = er;
                }
                finally
                {
                    if (searchAttempt < 10)
                    {
                        timer2.Enabled = true;
                        SearchCount = 8;
                        label9.Text = SearchCount.ToString();
                        ui.UpdateUI();
                    }
                    else
                    {
                        await Stop();
                        try
                        {
                            await Network.SendLineMessage("[" + ui.InstanceName + "] 更新エラーにより動作停止" + (lastRefreshException != null ? "\n" + lastRefreshException.Message : ""));
                        }
                        catch { }
                    }
                }
            }
        }

        private async Task RefreshThread()
        {
            try
            {
                searchProcessStartTime = UnixTime.Now();
                searchProccesing = true;
                await context.FullSearchThread(new Action<int, int>((i, cnt) =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            label9.Text = (i + 1) + "/" + cnt;
                        }));
                    }
                    else
                    {
                        label9.Text = (i + 1) + "/" + cnt;
                    }
                }));
                await SetPower();
                var fixedPowerList = powerList.Where(pair => pair.Value > 0.05).ToDictionary(pair => pair.Key, pair => pair.Value);
                var newThreads = fixedPowerList.Select(pair => pair.Key).Where(thread => notifiedThreads.All(t => t.Key != thread.Key)).ToList();
                notifiedThreads.AddRange(newThreads);
                if (notifiedThreads.Count > 100)
                {
                    notifiedThreads = notifiedThreads.GetRange(notifiedThreads.Count - 100, 100);
                }
                if (newThreads.Count > 0)
                {
                    foreach (var thread in newThreads)
                    {
                        await Network.SendLineMessage("新規スレッドを検知 [" + notifiedThreads.Last().Title + "]");
                    }
                }
                searchProccesing = false;
            }
            finally
            {
                searchProccesing = false;
            }
        }

        private async Task SetPower()
        {
            powerList = new Dictionary<BotThread, double>();
            var enabledList = context.ThreadContext.GetEnabled().Where(thread => !context.ThreadContext.IsIgnoredContains(thread)).ToBotThreadList();
            foreach (var thread in enabledList)
            {
                if (thread.ResListCache == null)
                    thread.ResListCache = Network.DatToDetailResList(await Network.GetDat(thread, context.ApiSid));
                var anchorResList = thread.ResListCache.Where(res => Regex.IsMatch(res["Message"], @">>\d+.*[^\d\.\-\s>]", RegexOptions.Singleline));
                var power = anchorResList.Aggregate(0.0, (p, res) => 1.0 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                var ageResList = thread.ResListCache.Where(res => int.Parse(res["No"]) >= 8 && res["Mail"] != "sage");
                power += ageResList.Aggregate(0.0, (p, res) => 0.1 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                powerList.Add(thread, power);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Count = 2;
        }

        private async Task Proccess()
        {
            try
            {
                var current = SetNextCurrent();
                if (current == null || context.ThreadContext.IsIgnored(current))
                {
                    WriteResult("");
                    return;
                }

                ReportProgress("MAIN_STARTED");
                processStartTime = UnixTime.Now();
                proceccing = true;
                timer1.Enabled = false;

                ReportProgress("CHANGE_IP_STARTED");
                await Network.ChangeIP();
                ReportProgress("CHANGE_IP_COMPLETED");

                if (context.HomeIP.Split('.').Length == 4)
                {
                    ReportProgress("GET_IP_STARTED");
                    await CheckIP();
                    ReportProgress("GET_IP_COMPLETED");
                }

                for (var i = 0; i < 3; i++)
                {
                    ReportProgress("GENERATE_MESSAGE_STARTED");
                    await context.SetNewMessage();
                    ReportProgress("GENERATE_MESSAGE_COMPLETED");

                    if (context.Message == string.Empty)
                    {
                        WriteResult("");
                        continue;
                    }

                    ReportProgress("POST" + (i + 1) + "_STARTED");
                    AddHistory(current);
                    SetUAKey();

                    try
                    {
                        var result = await Network.Post(current, context.Message, context.Name, context.Mail, context.UserAgent, context.MonaKey);
                        WriteResult(result);
                    }
                    catch (Exception er)
                    {
                        var _er = er as AggregateException == null ? er : er.InnerException;
                        if (_er as PostFailureException == null)
                            throw _er;
                        else
                            await ProcessPostError(current, _er);
                    }
                }

                // 書き込み成功後の処理
                ReportProgress("POST_COMPLETED");
                Attempt = 0;
                proceccing = false;
                timer1.Enabled = true;
                if (hasLastChance == false)
                {
                    hasLastChance = true;
                    try
                    {
                        await Network.SendLineMessage("モバイル回線の復旧に成功しました。");
                    }
                    catch { }
                }
            }
            catch (Exception er)
            {
                await ProcessError(er);
            }
            finally
            {
                ReportProgress("MAIN_COMPLETED");
            }
        }

        private async Task ProcessPostError(BotThread current, Exception err)
        {
            WriteResult((err as PostFailureException).Result);

            if ((err as PostFailureException).Result.IndexOf("このスレッドには") >= 0
                || (err as PostFailureException).Result.IndexOf("該当する") >= 0
                || (err as PostFailureException).Result.IndexOf("We hate Continuous") >= 0
                )
            {
                context.ThreadContext.AddIgnored(current, true);
                if ((err as PostFailureException).Result.IndexOf("We hate Continuous") >= 0)
                {
                    try
                    {
                        await Network.SendLineMessage("[Info]\n[" + current.Title + "]\n" + (err as PostFailureException).Result);
                    }
                    catch { }
                }
            }
            else
            {
                throw err as PostFailureException;
            }
        }

        private void SetUAKey()
        {
            var pair = context.UAMonaKeyPairs[0];
            context.UAMonaKeyPairs.Remove(pair);
            context.UAMonaKeyPairs.Add(pair);
            context.UserAgent = pair.UA;
            context.MonaKey = pair.MonaKey;
        }

        private void AddHistory(BotThread current)
        {
            current.Wrote = UnixTime.Now();
            context.ThreadContext.GetHistory().Sort(BotThreadList.SortMode.Wrote);
            context.ThreadContext.AddHistory(current);
        }

        private async Task ProcessError(Exception er)
        {
            var err = er as AggregateException == null ? er : er.InnerException;

            Attempt++;
            proceccing = false;

            WriteLog(err.Message + " " + err.StackTrace);

            ReportProgress("MAIN_FAILED");

            if (Attempt < 10)
            {
                timer1.Enabled = true;
            }
            else if (hasLastChance)
            {
                Attempt = 0;
                hasLastChance = false;

                ReportProgress("RESTART_USB_STARTED");
                try
                {
                    await Network.SendLineMessage("エラーのためコマンドを試行します");
                }
                catch { }
                ReportProgress("RESTART_USB_COMPLETED");

                await Network.RestartUsb();
                timer1.Enabled = true;
            }
            else
            {
                await Stop();
                ReportProgress("MAIN_FAIL_STOP_COMPLETED");

                try
                {
                    await Network.SendLineMessage(ui.InstanceName + ":" + err.Message);
                    var message = ResultWebBrowser.Document.Body.InnerText;
                    if (err as PostFailureException != null)
                        await Network.SendLineMessage(message);
                }
                catch (Exception er2)
                {
                    var _er2 = er2 as AggregateException == null ? er2 : er2.InnerException;
                    WriteLog(_er2.Message);
                }
            }
        }

        private BotThread SetNextCurrent()
        {
            var fixedPowerList = powerList.Where(pair => pair.Value > 0.05).ToDictionary(pair => pair.Key, pair => pair.Value);
            if (fixedPowerList.Count > 0)
            {
                var powerSum = fixedPowerList.Aggregate(0.0, (sum, pair) => pair.Value + sum);
                var rateList = fixedPowerList.ToDictionary(pair => pair.Key, pair => pair.Value / powerSum);
                var r = Generator.getRandomNumber(0, 100000000) / 100000000.0;
                var pickedKey = fixedPowerList.Keys.First();
                var acc = 0.0;
                foreach (var pair in rateList)
                {
                    acc += pair.Value;
                    if (acc > r)
                    {
                        pickedKey = pair.Key;
                        break;
                    }
                }
                context.ThreadContext.SetCurrent(pickedKey, true);
            }
            else
            {
                context.ThreadContext.SetCurrent(null, false);
            }

            return context.ThreadContext.GetCurrent();
        }

        private async Task CheckIP()
        {
            var ip = await Network.GetIPAddress();

            if (
                   (ip.Split('.')[0] == context.HomeIP.Split('.')[0] || context.HomeIP.Split('.')[0] == "*")
                && (ip.Split('.')[1] == context.HomeIP.Split('.')[1] || context.HomeIP.Split('.')[1] == "*")
                && (ip.Split('.')[2] == context.HomeIP.Split('.')[2] || context.HomeIP.Split('.')[2] == "*")
                && (ip.Split('.')[3] == context.HomeIP.Split('.')[3] || context.HomeIP.Split('.')[3] == "*")
            )
                throw new Exception("自宅回線になっています。");
        }

        private void WriteLog(string text)
        {
            textBox1.AppendText(text + "\r\n");
        }

        private void ReportProgress(string state)
        {
            switch (state)
            {
                case "MAIN_STARTED":
                    label8.Visible = true;
                    break;
                case "CHANGE_IP_STARTED":
                    toolStripLabel1.Text = "IP変更中…";
                    break;
                case "CHANGE_IP_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "GET_IP_STARTED":
                    toolStripLabel1.Text = "IP取得中…";
                    break;
                case "GET_IP_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "REFRESH_THREAD_LIST_STARTED":
                    toolStripLabel1.Text = "スレ覧更新中…";
                    break;
                case "REFRESH_THREAD_LIST_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "GENERATE_MESSAGE_STARTED":
                    toolStripLabel1.Text = "レス生成中…";
                    break;
                case "GENERATE_MESSAGE_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "CHECK_WACCHOI_STARTED":
                    toolStripLabel1.Text = "ﾜｯﾁｮｲ有無確認中…";
                    break;
                case "CHECK_WACCHOI_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "CHECK_FISHING_STARTED":
                    toolStripLabel1.Text = "釣りスレ確認中…";
                    break;
                case "CHECK_FISHING_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "POST1_STARTED":
                    toolStripLabel1.Text = "書き込み中…(1)";
                    break;
                case "POST2_STARTED":
                    toolStripLabel1.Text = "書き込み中…(2)";
                    break;
                case "POST3_STARTED":
                    toolStripLabel1.Text = "書き込み中…(3)";
                    break;
                case "POST_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "RESTART_USB_STARTED":
                    toolStripLabel1.Text = "USB再起動中…";
                    break;
                case "RESTART_USB_COMPLETED":
                    toolStripLabel1.Text = "";
                    break;
                case "MAIN_FAILED":
                    label8.Visible = false;
                    break;
                case "MAIN_FAIL_STOP_COMPLETED":
                    ui.manager.UpdateUI();
                    break;
                case "MAIN_COMPLETED":
                    label8.Visible = false;
                    toolStripLabel1.Text = "";
                    label6.Text = Attempt.ToString() + "/10";
                    ui.UpdateUI();
                    break;
                default:
                    break;
            }
        }
    }
}
