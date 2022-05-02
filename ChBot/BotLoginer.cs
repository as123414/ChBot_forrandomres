using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ChBot
{
    public partial class BotLoginer : Form
    {
        public bool Logining { get; set; }
        BotInstance ui;
        BotContext context;
        public bool toClose = false;
        public long loginStratTime = 0;

        public BotLoginer(BotInstance ui, BotContext context)
        {
            InitializeComponent();
            this.ui = ui;
            this.context = context;
            Logining = false;
        }

        public void Login()
        {
            if (ui.Visible)
                Show();

            loginStratTime = UnixTime.Now();
            Logining = true;

            Text = ui.InstanceName + ": " + "ログイン";
            label1.Text = "取得中・・・";
            label1.ForeColor = SystemColors.ControlText;

            ui.UpdateUI(BotInstance.UIParts.Other);
            ui.manager.UpdateUI();

            LoginWorker.RunWorkerAsync();
        }

        private void BotLoginer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!toClose)
                e.Cancel = true;
        }

        private void LoginWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            context.ApiSid = "";

            try
            {
                context.ApiSid = Network.GetApiSid(context.User, context.Password).Result;
            }
            catch (Exception er)
            {
                context.ApiSid = "";
                Console.WriteLine("エラー" + er.Message);
                return;
            }

            LoginWorker.ReportProgress(100);
        }

        private void LoginWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 100)
            {
                label1.Text = "完了";
                label1.ForeColor = Color.Blue;
            }
        }

        private async void LoginWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Logining = false;

            if (context.ApiSid == "")
            {
                label1.Text = "失敗";
                label1.ForeColor = Color.Red;
            }

            await Task.Delay(500);
            ui.UpdateUI(BotInstance.UIParts.Other);
            ui.manager.UpdateUI();
            Hide();

            foreach (var proxy in context.autoStartProxies)
            {
                proxy.Start();
            }

            context.autoStartProxies.Clear();

            ui.UpdateUI(BotInstance.UIParts.Other);
            ui.manager.UpdateUI();
        }
    }
}