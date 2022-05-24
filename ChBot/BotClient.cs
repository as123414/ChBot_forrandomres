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
        public bool hasLastChance = true;
        public bool toStop = false;
        public int DeviceIndex { get; private set; }
        public BotThread current = null;
        public bool waitingReboot = false;
        public bool pausing = false;
        public long pauseStartTime = 0;

        public BotClient(BotInstance ui, BotContext context, int deviceIndex)
        {
            InitializeComponent();

#if DEBUG
            button2.Visible = true;
#endif

            Attempt = 0;
            PostCount = context.Interval;
            Working = false;
            this.ui = ui;
            this.context = context;
            label6.Text = Attempt.ToString() + "/3";
            RestTimeLabel.Text = PostCount.ToString();
            DeviceIndex = deviceIndex;
        }

        public async Task Start(bool disableStartSearch)
        {
            if (Working)
                return;

            Attempt = 0;
            hasLastChance = true;
            PostCount = context.Interval;
            Working = true;
            timer1.Enabled = true;
            button1.Text = "停止";
            button1.BackColor = Color.Red;
            button1.ForeColor = Color.White;

            if (!disableStartSearch && !context.SearchWorking)
                await context.StartSearch();
        }

        public async Task Stop()
        {
            if (!Working)
                return;

            toStop = true;
            while (timer1Proceccing)
                await Task.Delay(100);
            toStop = false;

            timer1.Enabled = false;
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
                await Start(false);
            }

            ui.UpdateUI();
            ui.manager.UpdateUI();
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (toStop || waitingReboot || (pausing && UnixTime.Now() - pauseStartTime < 600) || context.pausing)
                return;

            PostCount -= 1;
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
                        if (pausing)
                        {
                            try { await Network.SendLineMessage("[" + ui.InstanceName + "]" + Network.DeviceIDList[DeviceIndex] + ":動作再開しました"); } catch { }
                            pausing = false;
                        }

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
                    await Task.Delay(1000 - timer1.Interval);
                }
            }
            catch { }
            finally
            {
                timer1Proceccing = false;
                if (code == 1)
                {
                    pausing = true;
                    pauseStartTime = UnixTime.Now();
                    /*await Stop();
                        ui.UpdateUI(BotInstance.UIParts.Other);
                        ui.manager.UpdateUI();*/
                }
                timer1.Enabled = Working;
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

                current = SetNextCurrent();
                if (current == null || context.ThreadContext.IsIgnored(current))
                {
                    WriteResult("");
                    return 0;
                }

                ReportProgress("CHANGE_IP_STARTED");
                await Network.ChangeIP(DeviceIndex);
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
                    await context.SetNewMessage(current);
                    ReportProgress("GENERATE_MESSAGE_COMPLETED");

                    context.SetNewName(current);

                    if (context.Message == string.Empty)
                    {
                        WriteResult("");
                        continue;
                    }

                    ReportProgress("POST" + (i + 1) + "_STARTED");
                    AddHistory(current);
                    context.SetUAKey();

                    try
                    {
                        var result = await Network.Post(current, context.Message, context.Name, context.Mail, context.UAMonaKeyPair, DeviceIndex);
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
                        context.ThreadContext.AddIgnored(current);
                    }
                }

                // 書き込み成功時の処理
                current = null;
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
                current = null;
                var _er = er as AggregateException == null ? er : er.InnerException;
                WriteLog(_er.Message);
                Attempt++;
                ReportProgress("MAIN_FAILED");
                if (Attempt >= 3)
                {
                    if (hasLastChance)
                    {
                        // リトライ回数が上限を超えたらUSBを再起動してリトライ回数をリセットして再度試す
                        Attempt = 0;
                        hasLastChance = false;
                        try { await Network.SendLineMessage("エラーのためコマンドを試行します\n" + _er.Message); } catch { }
                        ReportProgress("RESTART_USB_STARTED");
                        try
                        {
                            await Network.RestartUsb(DeviceIndex);
                            await Network.DisableWiFi(DeviceIndex);
                            await Network.EnableUsbTethering(DeviceIndex);
                        }
                        catch { }
                        ReportProgress("RESTART_USB_COMPLETED");
                        return 0;
                    }
                    else if (UnixTime.Now() - Properties.Settings.Default.LastRetry > 120)
                    {
                        try { await Network.SendLineMessage("エラーのため再起動します\n" + _er.Message); } catch { }
                        ui.manager.restartFlag = true;
                        waitingReboot = true;
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

        private void AddHistory(BotThread current)
        {
            current.Wrote = UnixTime.Now();
            context.ThreadContext.GetHistory().Sort(BotThreadList.SortMode.Wrote);
            context.ThreadContext.AddHistory(current);
        }

        private BotThread SetNextCurrent()
        {
            var fixedPowerList = context.PowerList
                .Where(pair => context.AllowDuplicatePost || context.ClientList.All(client => client.current == null || !client.current.Equals(pair.Key)))
                .Where(pair => pair.Value > 0.05)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

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
                return pickedKey;
            }
            else
            {
                return null;
            }
        }

        private async Task CheckIP()
        {
            var ip = await Network.GetIPAddress(DeviceIndex);

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
                    label6.Text = Attempt.ToString() + "/3";
                    break;
                case "MAIN_FAIL_STOP_COMPLETED":
                    ui.manager.UpdateUI();
                    break;
                case "MAIN_COMPLETED":
                    label8.Visible = false;
                    toolStripLabel1.Text = "";
                    label6.Text = Attempt.ToString() + "/3";
                    ui.UpdateUI();
                    break;
                default:
                    break;
            }
        }
    }
}
