using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace ChBot
{
    public partial class InstanceManager : Form
    {
        public List<BotInstance> instanceList = new List<BotInstance>();
        public bool autoStart = false;

        public void Test()
        {
            MessageBox.Show("tete");
        }

        public InstanceManager()
        {
            InitializeComponent();
        }

        private async void InstanceManager_Load(object sender, EventArgs e)
        {
            autoStart = Properties.Settings.Default.IsRestart;
            Properties.Settings.Default.IsRestart = false;
            Properties.Settings.Default.Save();

            if (autoStart)
            {
                try
                {
                    await Network.SendLineMessage("再起動しました");
                }
                catch { }
            }

            try
            {
                Network.LoadConfig();
            }
            catch
            {
                if (!autoStart)
                {
                    MessageBox.Show("config.txtの読み込みに失敗");
                }
                else
                {
                    try
                    {
                        await Network.SendLineMessage("config.txtの読み込みに失敗");
                    }
                    catch { }
                }
            }

            LoadState();
            UpdateUI();
        }

        private void LoadState()
        {
            try
            {
                if (Properties.Settings.Default.InstanceStates == "")
                {
                    instanceList = new List<BotInstance>();
                }
                else
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(Properties.Settings.Default.InstanceStates);

                    foreach (var instanceData in data.EnumerateArray())
                    {
                        var instanceName = instanceData.GetProperty("InstanceName").GetString();
                        var visible = instanceData.GetProperty("Visible").GetBoolean();
                        var state = instanceData.GetProperty("State");

                        var newInstance = new BotInstance(instanceName, this);
                        instanceList.Add(newInstance);
                        newInstance.context.ResumeState(state);
                        if (visible)
                        {
                            newInstance.Show();
                            foreach (var client in newInstance.context.ClientList)
                                client.Show();
                        }
                        newInstance.WindowState = (FormWindowState)Enum.Parse(typeof(FormWindowState), instanceData.GetProperty("WindowState").GetString());
                        newInstance.UpdateUI();
                    }
                }
            }
            catch { }

            foreach (var instance in instanceList)
                instance.context.Login();

            UpdateUI();
        }

        public void UpdateUI()
        {
            listView1.Items.Clear();
            foreach (var instance in instanceList)
            {
                var item = new ListViewItem(new[] { instance.InstanceName, instance.context.Loginer.Logining ? "ログイン中" : instance.context.Working ? "稼働中" : "停止中" });
                item.BackColor = instance.context.Working ? Color.Red : Color.White;
                item.ForeColor = instance.context.Working ? Color.White : Color.Black;
                item.Tag = instance;
                listView1.Items.AddRange(new[] { item });
            }

            ApplySelectedIndexChanged();
        }

        private void InstanceManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveState();
            foreach (var instance in instanceList)
            {
                instance.toClose = true;
                instance.Close();
            }
        }

        public void SaveState()
        {
            var data = instanceList.Select(instance => new
            {
                instance.InstanceName,
                instance.Visible,
                instance.WindowState,
                State = instance.context.GetState()
            });

            var jsonstr = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });

            jsonstr = Regex.Replace(jsonstr, @"([^\\])\\u3000", "$1　");
            jsonstr = Regex.Replace(jsonstr, @"^\\u3000", "　");
            Properties.Settings.Default.InstanceStates = jsonstr;
            Properties.Settings.Default.Save();
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;

            var selectedInstance = (BotInstance)listView1.SelectedItems[0].Tag;
            selectedInstance.Show();
            selectedInstance.BringToFront();
            selectedInstance.WindowState = FormWindowState.Normal;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var usedNames = instanceList.Select(instance => instance.InstanceName);

            var i = 1;
            while (usedNames.Where(name => name == "instance" + i).Count() > 0)
                i++;

            var newInstance = new BotInstance("instance" + i, this);
            instanceList.Add(newInstance);
            newInstance.context.LoadSettings();
            newInstance.context.Login();
            newInstance.UpdateUI();
            UpdateUI();
            SaveState();
            newInstance.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;

            var selectedInstance = (BotInstance)listView1.SelectedItems[0].Tag;
            var newInstanceList = new List<BotInstance>();
            foreach (var instance in instanceList)
            {
                if (selectedInstance.InstanceName != instance.InstanceName)
                {
                    newInstanceList.Add(instance);
                }
                else
                {
                    selectedInstance.toClose = true;
                    selectedInstance.Close();
                }
            }

            instanceList = newInstanceList;

            UpdateUI();
            SaveState();
        }

        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label == null)
            {
                e.CancelEdit = true;
                return;
            }

            var newInstanceName = e.Label.Trim();
            var editingInstance = (BotInstance)listView1.Items[e.Item].Tag;
            var oldInstanceName = editingInstance.InstanceName;
            var otherInstances = instanceList.Where(item => item.InstanceName != oldInstanceName);
            if (otherInstances.Where(item => item.InstanceName == newInstanceName).Count() == 0)
            {
                editingInstance.InstanceName = newInstanceName;
                editingInstance.UpdateUI(BotInstance.UIParts.Other);
                UpdateUI();
                SaveState();
            }
            else
            {
                e.CancelEdit = true;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var cond1 = instanceList.Any(instance => instance.context.Loginer.Logining && UnixTime.Now() - instance.context.Loginer.loginStratTime > 30);

            var cond2 = instanceList.Any(instance =>
            {
                return instance.context.ClientList.Any(client =>
                {
                    return client.timer1Proceccing && UnixTime.Now() - client.timer1ProccessStartTime > 30;
                });
            });

            var cond3 = instanceList.Any(instance =>
            {
                return instance.context.searchTimerProceccing && UnixTime.Now() - instance.context.searchTimerProccessStartTime > 180;
            });

            if (cond1 || cond2 || cond3)
            {
#if DEBUG
                return;
#endif
                Close();
                Properties.Settings.Default.IsRestart = true;
                Properties.Settings.Default.Save();
                Application.Restart();
                File.AppendAllText(@"log.txt", "[" + DateTime.Now.ToString() + "] Auto restarted.\r\n");
            }
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;

            var selectedInstance = (BotInstance)listView1.SelectedItems[0].Tag;

            button3.Enabled = false;
            while (selectedInstance.context.Loginer.Logining)
                await Task.Delay(100);

            if (selectedInstance.context.Working)
            {
                await selectedInstance.context.StopAttack();
            }
            else
            {
                await selectedInstance.context.StartAttack();
            }

            button3.Enabled = true;
            selectedInstance.UpdateUI();
            UpdateUI();
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySelectedIndexChanged();
        }

        private void ApplySelectedIndexChanged()
        {
            BotInstance selectedInstance = null;
            if (listView1.SelectedItems.Count > 0)
            {
                selectedInstance = (BotInstance)listView1.SelectedItems[0].Tag;
            }

            if (selectedInstance != null)
            {

                for (var i = 0; i < listView1.Items.Count; i++)
                {
                    if (listView1.Items[i].Tag == selectedInstance)
                    {
                        listView1.SelectedIndices.Add(i);
                        break;
                    }
                }

                button3.Enabled = true;
                if (selectedInstance.context.Working)
                {
                    button3.Text = "停止";
                    button3.BackColor = Color.Red;
                    button3.ForeColor = Color.White;
                }
                else
                {
                    button3.Text = "開始";
                    button3.BackColor = SystemColors.Control;
                    button3.ForeColor = SystemColors.ControlText;
                }
            }
            else
            {
                button3.Enabled = false;
                button3.Text = "開始";
                button3.BackColor = SystemColors.Control;
                button3.ForeColor = SystemColors.ControlText;
            }
        }

        private async void timer2_Tick(object sender, EventArgs e)
        {
            try
            {
                timer2.Enabled = false;
                var msg = await Network.GetLineMessage();
                if (msg == "")
                    return;

                var m1 = Regex.Match(msg, @"del\s(https?://[^/]+?\.5ch\.net/test/read\.cgi/[^/]+?/\d+/)");
                var m2 = Regex.Match(msg, @"https?://[^/]+?\.5ch\.net/test/read\.cgi/[^/]+?/\d+/");

                if (m1.Success)
                {
                    var url = m1.Groups[1].Value;

                    foreach (var instance in instanceList)
                    {
                        instance.context.SearchConditions = instance.context.SearchConditions.Where(condition => condition.Url != url).ToList();
                        instance.searchConditionsForm.UpdateUI();
                    }
                }
                else if (m2.Success)
                {
                    var url = m2.Value;
                    var condition = new SearchCondition()
                    {
                        SearchMode = SearchCondition.SearchModes.Url,
                        Url = url
                    };

                    foreach (var instance in instanceList)
                    {
                        if (instance.context.SearchConditions.All(c => c.Url != url))
                        {
                            instance.context.SearchConditions.Add(condition);
                            instance.searchConditionsForm.UpdateUI();
                        }
                    }
                }

                if (m1.Success || m2.Success)
                {
                    await Task.Delay(500);

                    foreach (var instance in instanceList)
                    {
                        var text = "[" + instance.InstanceName + "]\n" + string.Join("\n", instance.context.SearchConditions.Select(c => c.SearchMode == SearchCondition.SearchModes.Url ? c.Url : c.Word));
                        await Network.SendLineMessage(text);
                    }

                    return;
                }

                switch (msg)
                {
                    case "GATHER_MONA":
                        await Network.SendLineMessage("RECIEVED.");
                        foreach (var instance in instanceList)
                            await instance.context.GatherMonaKey();
                        await Network.SendLineMessage("DONE.");
                        break;
                    case "START":
                        await Network.SendLineMessage("RECIEVED.");
                        foreach (var instance in instanceList)
                            await instance.context.StartAttack();
                        await Task.Delay(500);
                        var url = await Network.UploadCapture();
                        await Network.SendLineImage(url);
                        await Network.SendLineMessage("DONE.");
                        break;
                    case "STOP":
                        await Network.SendLineMessage("RECIEVED.");
                        foreach (var instance in instanceList)
                            await instance.context.StopAttack();
                        await Task.Delay(500);
                        var url2 = await Network.UploadCapture();
                        await Network.SendLineImage(url2);
                        await Network.SendLineMessage("DONE.");
                        break;
                    case "CAPTURE":
                        await Network.SendLineMessage("RECIEVED.");
                        var url3 = await Network.UploadCapture();
                        await Network.SendLineImage(url3);
                        await Network.SendLineMessage("DONE.");
                        break;
                    default:
                        await Network.SendLineMessage("UNRECOGNIZED.");
                        break;
                }
            }
            catch (Exception er)
            {
                try
                {
                    await Network.SendLineMessage("[InstanceManager]\n" + er.Message);
                }
                catch { }
            }
            finally
            {
                timer2.Enabled = true;
            }
        }
    }
}
