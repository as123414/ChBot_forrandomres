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
        public double PostCount { get; private set; }
        public bool Working { get; private set; }
        public BotInstance ui;
        public BotContext context;
        public bool timer1Proceccing = false;
        public long timer1ProccessStartTime = 0;
        public bool timer2Proceccing = false;
        public long timer2ProccessStartTime = 0;
        public int SearchCount;
        public int searchAttempt = 0;
        public Dictionary<BotThread, double> powerList = new Dictionary<BotThread, double>();
        public List<BotThread> notifiedThreads = new List<BotThread>();
        public bool hasLastChance = true;
        public bool hasLastChance2 = true;
        public Exception lastRefreshException = null;
        public bool toStop = false;

        public BotClient(BotInstance ui, BotContext context)
        {
            InitializeComponent();

#if DEBUG
            button2.Visible = true;
#endif

            Attempt = 0;
            PostCount = context.Interval;
            SearchCount = 0;
            Working = false;
            this.ui = ui;
            this.context = context;
            label6.Text = Attempt.ToString() + "/10";
            RestTimeLabel.Text = PostCount.ToString();
            label9.Text = SearchCount.ToString();
        }

        public void Start()
        {
            if (Working)
                return;

            Attempt = 0;
            searchAttempt = 0;
            hasLastChance = true;
            hasLastChance2 = true;
            PostCount = context.Interval;
            Working = true;
            timer1.Enabled = true;
            SearchCount = 0;
            timer2.Enabled = true;
            button1.Text = "停止";
            button1.BackColor = Color.Red;
            button1.ForeColor = Color.White;
        }

        public async Task Stop()
        {
            if (!Working)
                return;

            toStop = true;
            while (timer1Proceccing || timer2Proceccing)
                await Task.Delay(100);
            toStop = false;

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
                button1.Enabled = false;
                await Stop();
                button1.Enabled = true;
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
            if (toStop) return;

            PostCount -= 0.1;
            PostCount = PostCount < 0 ? 0 : PostCount;
            RestTimeLabel.Text = PostCount.ToString();
            ui.UpdateUI(BotInstance.UIParts.Other);

            var code = 0;
            try
            {
                timer1Proceccing = true;
                timer1ProccessStartTime = UnixTime.Now();
                timer1.Enabled = false;

                var startTime = UnixTime.Now();

                if (PostCount <= 0)
                {
                    try
                    {
                        code = await Proccess();
                    }
                    finally
                    {
                        PostCount = context.Interval;
                        RestTimeLabel.Text = PostCount.ToString();
                        ui.UpdateUI(BotInstance.UIParts.Other);
                    }
                }

                if (UnixTime.Now() - startTime < 2)
                {
                    await Task.Delay(1000);
                }
            }
            catch { }
            finally
            {
                timer1Proceccing = false;
                if (code == 0)
                {
                    timer1.Enabled = Working;
                }
                else
                {
                    await Stop();
                    ui.UpdateUI(BotInstance.UIParts.Other);
                    ui.manager.UpdateUI();
                }
            }
        }

        private async void timer2_Tick(object sender, EventArgs e)
        {
            if (toStop) return;

            SearchCount--;
            SearchCount = SearchCount < 0 ? 0 : SearchCount;
            label9.Text = SearchCount.ToString();

            if (SearchCount > 0) return;

            var code = 0;
            try
            {
                timer2Proceccing = true;
                timer2ProccessStartTime = UnixTime.Now();
                timer2.Enabled = false;

                await RefreshThread();

                // 成功したらリセット
                searchAttempt = 0;
            }
            catch (Exception er)
            {
                var _er = er as AggregateException == null ? er : er.InnerException;
                WriteLog(_er.Message);
                searchAttempt++;
                if (searchAttempt >= 10)
                {
                    code = 1;
                    try { await Network.SendLineMessage("[" + ui.InstanceName + "] 更新エラーにより動作停止\n" + _er.Message); } catch { }
                }
            }
            finally
            {
                timer2Proceccing = false;
                if (code == 0)
                    timer2.Enabled = Working;
                else
                    await Stop();
                SearchCount = 10;
                label9.Text = SearchCount.ToString();
                ui.UpdateUI();
                ui.manager.UpdateUI();
            }
        }

        private async Task RefreshThread()
        {
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
            PostCount = 2;
        }

        private async Task<int> Proccess()
        {
            try
            {
                ReportProgress("MAIN_STARTED");

                var current = SetNextCurrent();
                if (current == null || context.ThreadContext.IsIgnored(current))
                {
                    WriteResult("");
                    return 0;
                }

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
                            throw;
                        var __er = _er as PostFailureException;
                        WriteResult(__er.Result);
                        if (__er.Result.IndexOf("このスレッドには") == -1 && __er.Result.IndexOf("該当する") == -1)
                            throw;
                        context.ThreadContext.AddIgnored(current, true);
                    }
                }

                // 書き込み成功時の処理
                Attempt = 0;
                ReportProgress("POST_COMPLETED");
                if (hasLastChance == false)
                {
                    hasLastChance = true;
                    try
                    {
                        await Network.SendLineMessage("モバイル回線の復旧に成功しました。");
                    }
                    catch { }
                }
                return 0;
            }
            catch (Exception er)
            {
                // 書き込み失敗時の処理
                var _er = er as AggregateException == null ? er : er.InnerException;
                WriteLog(_er.Message + " " + _er.StackTrace);
                Attempt++;
                ReportProgress("MAIN_FAILED");
                if (Attempt >= 10)
                {
                    if (hasLastChance)
                    {
                        // リトライ回数が上限を超えたらUSBを再起動してリトライ回数をリセットして再度試す
                        Attempt = 0;
                        hasLastChance = false;
                        try { await Network.SendLineMessage("エラーのためコマンドを試行します"); } catch { }
                        ReportProgress("RESTART_USB_STARTED");
                        try { await Network.RestartUsb(); } catch { }
                        ReportProgress("RESTART_USB_COMPLETED");
                        return 0;
                    }
                    else
                    {
                        // それでもリトライ回数が上限を超えたら動作を停止する
                        ReportProgress("MAIN_FAIL_STOP_COMPLETED");
                        try
                        {
                            await Network.SendLineMessage(ui.InstanceName + ":" + _er.Message);
                            var message = ResultWebBrowser.Document.Body.InnerText;
                            if (_er as PostFailureException != null)
                                await Network.SendLineMessage(message);
                        }
                        catch (Exception er2)
                        {
                            var _er2 = er2 as AggregateException == null ? er2 : er2.InnerException;
                            WriteLog(_er2.Message);
                        }
                        return 1;
                    }
                }
                return 0;
            }
            finally
            {
                ReportProgress("MAIN_COMPLETED");
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
                    toolStripLabel1.Text = "";
                    label8.Visible = false;
                    label6.Text = Attempt.ToString() + "/10";
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
