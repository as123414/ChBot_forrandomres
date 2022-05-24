using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class BotContext
    {
        //リストモード
        public enum ListModes { Search, History }

        public BotThreadContext ThreadContext { get; set; }

        public string Message { get; set; }
        public string Mail { get; set; }
        public string Name { get; set; }
        public int Interval { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string JanePath { get; set; }
        public ListModes ListMode { get; set; }
        public string ApiSid { get; set; }
        public string HomeIP { get; set; }
        public BotUAMonaKeyPair UAMonaKeyPair { get; set; }
        public bool AllowDuplicatePost { get; set; }

        public List<BotUAMonaKeyPair> UAMonaKeyPairs { get; set; }

        public List<BotClient> ClientList { get; set; }

        public List<SearchCondition> SearchConditions { get; set; }
        public bool Working { get => ClientList.Any(client => client.Working) || SearchWorking; }
        public BotThreadList everMatchList = new BotThreadList();

        public BotInstance ui;
        public BotLoginer Loginer;
        public List<BotClient> autoStartProxies;
        public bool autoStartSearch;

        public Timer searchTimer;
        public bool SearchWorking { get; private set; } = false;
        public bool searchTimerProceccing = false;
        public long searchTimerProccessStartTime = 0;
        public int SearchCount = 0;
        public int searchAttempt = 0;
        public BotThreadList notifiedThreads = new BotThreadList();
        public bool toStop = false;
        public Dictionary<BotThread, double> PowerList { get; set; }
        public bool waitingReboot = false;
        public bool pausing = false;
        public long pauseStartTime = 0;

        public BotContext(BotInstance ui)
        {
            this.ui = ui;
            ThreadContext = new BotThreadContext();
            autoStartProxies = new List<BotClient>();
            autoStartSearch = false;
            JanePath = "";
            ListMode = ListModes.Search;
            Loginer = new BotLoginer(ui, this);
            UAMonaKeyPair = null;
            UAMonaKeyPairs = new List<BotUAMonaKeyPair>();
            PowerList = new Dictionary<BotThread, double>();
            searchTimer = new Timer() { Interval = 1000 };
            searchTimer.Tick += SearchTimer_Tick;
            ResetSettings();
            ClientList = new List<BotClient>() { new BotClient(ui, this, 0) };
        }

        private async void SearchTimer_Tick(object sender, EventArgs e)
        {
            if (toStop || waitingReboot || (pausing && UnixTime.Now() - pauseStartTime < 600))
                return;

            SearchCount--;
            SearchCount = SearchCount < 0 ? 0 : SearchCount;
            ui.label9.Text = SearchCount + "/" + Interval;

            if (SearchCount > 0) return;

            var code = 0;
            try
            {
                searchTimerProceccing = true;
                searchTimerProccessStartTime = UnixTime.Now();
                searchTimer.Enabled = false;

                if (pausing)
                {
                    try { await Network.SendLineMessage(ui.InstanceName + ":動作再開しました"); } catch { }
                    pausing = false;
                }

                await RefreshThread();

                // 成功したらリセット
                searchAttempt = 0;
            }
            catch (Exception er)
            {
                var _er = er as AggregateException == null ? er : er.InnerException;
                WriteLog(_er.Message);
                searchAttempt++;
                if (searchAttempt >= 3)
                {
                    if (UnixTime.Now() - Properties.Settings.Default.LastRetry > 120)
                    {
                        try { await Network.SendLineMessage("更新エラーのため再起動します"); } catch { }
                        ui.manager.restartFlag = true;
                        waitingReboot = true;
                        code = 0;
                    }
                    else
                    {
                        code = 1;
                        try { await Network.SendLineMessage("[" + ui.InstanceName + "] 更新エラーにより動作停止\n" + _er.Message); } catch { }
                    }
                }
            }
            finally
            {
                searchTimerProceccing = false;
                if (code == 1)
                {
                    pausing = true;
                    pauseStartTime = UnixTime.Now();
                    //await StopAttack();
                }
                searchTimer.Enabled = SearchWorking;
                SearchCount = 10;
                ui.label9.Text = SearchCount.ToString();
                ui.UpdateUI();
                ui.manager.UpdateUI();
            }
        }

        private async Task RefreshThread()
        {
            await FullSearchThread(new Action<int, int>((i, cnt) =>
            {
                ui.label9.Text = (i + 1) + "/" + cnt;
            }));
            await SetPower();
            var fixedPowerList = PowerList.Where(pair => pair.Value > 0.05).ToDictionary(pair => pair.Key, pair => pair.Value);
            var newThreads = fixedPowerList.Select(pair => pair.Key).Where(thread => notifiedThreads.All(t => t.Key != thread.Key)).ToList();
            notifiedThreads.AddRange(newThreads);
            if (notifiedThreads.Count > 100)
            {
                notifiedThreads = notifiedThreads.GetRange(notifiedThreads.Count - 100, 100).ToBotThreadList();
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
            PowerList = new Dictionary<BotThread, double>();
            var enabledList = ThreadContext.GetEnabled().Where(thread => !ThreadContext.IsIgnoredContains(thread)).ToBotThreadList();
            foreach (var thread in enabledList)
            {
                if (thread.MatchedSearchCondition != null)
                {
                    var power = await thread.MatchedSearchCondition.GetPower(this, thread);
                    PowerList.Add(thread, power);
                }
            }
        }

        private void WriteLog(string text)
        {
            ui.textBox1.AppendText(text + "\r\n");
        }

        //デフォルトプロファイルの復元
        private void ResetSettings()
        {
            Message = "てす";
            Mail = "";
            Name = "";
            Interval = 20;
            User = "";
            HomeIP = "";
            Password = "";
            SearchConditions = new List<SearchCondition>() { new SearchCondition() };
            AllowDuplicatePost = false;
        }

        public void Login()
        {
            Loginer.Login();
        }

        public async Task StartAttack()
        {
            foreach (var client in ClientList)
                await client.Start(true);

            await StartSearch();
        }

        public async Task StopAttack()
        {
            foreach (var client in ClientList)
                await client.Stop();

            await StopSearch();
        }

        public async Task StartSearch()
        {
            if (SearchWorking)
                return;

            searchAttempt = 0;
            SearchCount = 0;
            SearchWorking = true;
            searchTimer.Enabled = true;
        }

        public async Task StopSearch()
        {
            if (!SearchWorking)
                return;

            toStop = true;
            while (searchTimerProceccing)
                await Task.Delay(100);
            toStop = false;
            SearchWorking = false;
            searchTimer.Enabled = false;
        }

        public async Task GatherMonaKey()
        {
            UAMonaKeyPairs = new List<BotUAMonaKeyPair>();
            for (var i = 0; i < 100; i++)
            {
                if (i % 5 == 0)
                    await Network.ChangeIP(0);

                var ua = Network.GetRandomUseragent(BotUA.ChMate);
                var mona = await Network.GetMonaKey(ua, 0);
                /*var thread = new BotThread(1509713280, "", "mi.5ch.net", "news4vip");
                try
                {
                    await Network.Post(thread, "test", "", "", ua, mona);
                }
                catch (PostFailureException) { }*/
                var pair = new BotUAMonaKeyPair(ua, mona, false);
                UAMonaKeyPairs.Add(pair);
                Console.WriteLine("[" + (i + 1) + "/100] " + ua + "," + mona);
            }
        }

        public async Task FillMonaKey()
        {
            UAMonaKeyPairs = UAMonaKeyPairs.Where(p => p.Used).ToList();
            var addNum = 100 - UAMonaKeyPairs.Count;
            var newUAMonaKeyPairs = new List<BotUAMonaKeyPair>();
            for (var i = 0; i < addNum; i++)
            {
                if (i % 5 == 0)
                    await Network.ChangeIP(0);

                var ua = Network.GetRandomUseragent(BotUA.ChMate);
                var mona = await Network.GetMonaKey(ua, 0);
                /*var thread = new BotThread(1509713280, "", "mi.5ch.net", "news4vip");
                try
                {
                    await Network.Post(thread, "test", "", "", ua, mona);
                }
                catch (PostFailureException) { }*/
                var pair = new BotUAMonaKeyPair(ua, mona, false);
                newUAMonaKeyPairs.Insert(0, pair);
                Console.WriteLine("[" + (i + 1) + "/" + addNum + "] " + ua + "," + mona);
            }
            UAMonaKeyPairs.InsertRange(0, newUAMonaKeyPairs);
        }

        public void SetUAKey()
        {
            if (UAMonaKeyPairs.Count == 0)
                throw new Exception("モナキーが0個です");
            lock (UAMonaKeyPairs)
            {
                var pair = UAMonaKeyPairs[0];
                UAMonaKeyPairs.Remove(pair);
                UAMonaKeyPairs.Add(pair);
                UAMonaKeyPair = pair;
            }
        }

        //メッセージ欄にランダム文字列をセット
        public async Task SetNewMessage(BotThread thread)
        {
            if (thread.MatchedSearchCondition == null)
                Message = "";
            else
                Message = await thread.MatchedSearchCondition.GetMessage(this, thread);
        }

        public void SetNewName(BotThread thread)
        {
            if (thread.MatchedSearchCondition == null)
                Name = "";
            else
                Name = thread.MatchedSearchCondition.GetName(this, thread);
        }

        public async Task<BotThreadList> GetAllThreadList()
        {
            var boards = new List<string>();
            foreach (var searchCondition in SearchConditions)
            {
                if (!boards.Contains(searchCondition.Board))
                    boards.Add(searchCondition.Board);
            }

            var allThreads = new BotThreadList();
            foreach (var board in boards)
                allThreads.AddRange(await Network.GetThreadList(board));

            return allThreads;
        }

        //スレッド検索
        public async Task SearchThread()
        {
            var allThreads = await GetAllThreadList();
            var searchResult = new BotThreadList();
            foreach (var thread in allThreads)
            {
                if (everMatchList.Contains(thread))
                {
                    var existThread = everMatchList.FirstOrDefault(t => t.Equals(thread));
                    existThread.ResListCache = null;
                    searchResult.Add(existThread);
                    continue;
                }

                foreach (var condition in SearchConditions)
                {
                    if (!condition.Enabled)
                        continue;

                    if (condition.IsMatchLiteCondition(thread))
                    {
                        thread.MatchedSearchCondition = condition;
                        searchResult.Add(thread);
                        break;
                    }
                }
            }
            searchResult.Sort();
            ThreadContext.ClearEnabled();
            ThreadContext.AddEnabled(searchResult);
            ThreadContext.SearchResult = searchResult;
            ThreadContext.DeleteUnneccesaryIgnored();
        }

        public async Task MiddleSearchThread()
        {
            var allThreads = await GetAllThreadList();
            var searchResult = new BotThreadList();
            foreach (var thread in allThreads)
            {
                if (everMatchList.Contains(thread))
                {
                    var existThread = everMatchList.FirstOrDefault(t => t.Equals(thread));
                    existThread.ResListCache = null;
                    searchResult.Add(existThread);
                    continue;
                }

                foreach (var condition in SearchConditions)
                {
                    if (!condition.Enabled)
                        continue;

                    if (await condition.IsMatchMiddleCondition(thread))
                    {
                        thread.MatchedSearchCondition = condition;
                        searchResult.Add(thread);
                        break;
                    }
                }
            }
            searchResult.Sort();
            ThreadContext.ClearEnabled();
            ThreadContext.AddEnabled(searchResult);
            ThreadContext.SearchResult = searchResult;
            ThreadContext.DeleteUnneccesaryIgnored();
        }

        public async Task FullSearchThread(Action<int, int> report = null)
        {
            var allThreads = await GetAllThreadList();
            var searchResult = new BotThreadList();
            var threads = new BotThreadList();
            foreach (var thread in allThreads)
            {
                if (everMatchList.Contains(thread))
                {
                    var existThread = everMatchList.FirstOrDefault(t => t.Equals(thread));
                    existThread.ResListCache = null;
                    searchResult.Add(existThread);
                    continue;
                }

                foreach (var condition in SearchConditions)
                {
                    if (!condition.Enabled)
                        continue;

                    if (await condition.IsMatchMiddleCondition(thread))
                    {
                        threads.Add(thread);
                        break;
                    }
                }
            }

            for (var i = 0; i < threads.Count; i++)
            {
                var thread = threads[i];
                foreach (var condition in SearchConditions)
                {
                    if (!condition.Enabled)
                        continue;

                    if (await condition.IsMatchFullCondition(thread, ApiSid))
                    {
                        thread.MatchedSearchCondition = condition;
                        searchResult.Add(thread);
                        if (condition.EverMatch)
                            everMatchList.Add(thread);
                        break;
                    }
                }

                if (report != null)
                    report(i + 1, threads.Count);
            }
            searchResult.Sort();
            ThreadContext.ClearEnabled();
            ThreadContext.AddEnabled(searchResult);
            ThreadContext.SearchResult = searchResult;
            ThreadContext.DeleteUnneccesaryIgnored();
        }

        //プロファイル保存
        public void SaveSettings()
        {
            var settings = new
            {
                Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                Message,
                Mail,
                Name,
                Interval,
                User,
                Password,
                HomeIP,
                SearchConditions,
                AllowDuplicatePost
            };

            var jsonstr = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });

            jsonstr = Regex.Replace(jsonstr, @"([^\\])\\u3000", "$1　");
            jsonstr = Regex.Replace(jsonstr, @"^\\u3000", "　");
            File.WriteAllText("profile.json", jsonstr);
        }

        //プロファイル読み込み
        public void LoadSettings()
        {
            ResetSettings();

            if (!File.Exists("profile.json"))
                return;

            try
            {
                var jsonstr = File.ReadAllText("profile.json");
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonstr);

                Message = settings["Message"].GetString();
                Mail = settings["Mail"].GetString();
                Name = settings["Name"].GetString();
                Interval = settings["Interval"].GetInt32();
                User = settings["User"].GetString();
                HomeIP = settings["HomeIP"].GetString();
                Password = settings["Password"].GetString();
                SearchConditions = settings["SearchConditions"].EnumerateArray().Select(conditionJson =>
                {
                    var condition = new SearchCondition();
                    condition.Resume(conditionJson);
                    return condition;
                }).ToList();
                AllowDuplicatePost = settings["AllowDuplicatePost"].GetBoolean();
            }
            catch { }
        }

        //匿名クラスで状態データを取得
        public dynamic GetState()
        {
            return new
            {
                Message,
                Mail,
                Name,
                Interval,
                User,
                Password,
                HomeIP,
                SearchConditions,
                AllowDuplicatePost,

                ListMode,
                JanePath,
                ThreadContext.Direction,
                SearchResult = ThreadContext.SearchResult.Clone().getObject(),
                Enabled = ThreadContext.GetEnabled().getObject(),
                Ignored = ThreadContext.GetIgnored().getObject(),
                History = ThreadContext.GetHistory().getObject(),
                Working,
                ProxyWindowState = ClientList.Select(client => new
                {
                    Working = client.Working,
                    Visible = client.Visible,
                    WindowState = client.WindowState,
                    LocationX = client.Location.X,
                    LocationY = client.Location.Y,
                    deviceIndex = client.DeviceIndex
                }),
                UAMonaKeyPairs = UAMonaKeyPairs.Select(pair => pair.UA + "," + pair.MonaKey + "," + (pair.Used ? "1" : "0")),
                SearchWorking
            };
        }

        //JSON形式の状態データから復元
        public void ResumeState(JsonElement state)
        {
            try
            {
                Message = state.GetProperty("Message").GetString();
                Mail = state.GetProperty("Mail").GetString();
                Name = state.GetProperty("Name").GetString();
                Interval = state.GetProperty("Interval").GetInt32();
                User = state.GetProperty("User").GetString();
                Password = state.GetProperty("Password").GetString();
                ListMode = (ListModes)Enum.Parse(typeof(ListModes), state.GetProperty("ListMode").GetString());
                ThreadContext.Direction = (BotThreadContext.Directions)Enum.Parse(typeof(BotThreadContext.Directions), state.GetProperty("Direction").GetString());

                var searchResult = new BotThreadList();
                searchResult.ResumeObject(state.GetProperty("SearchResult"));
                ThreadContext.SearchResult = searchResult;

                ThreadContext.ClearEnabled();
                var enabled = new BotThreadList();
                enabled.ResumeObject(state.GetProperty("Enabled"));
                ThreadContext.AddEnabled(enabled);

                ThreadContext.ClearIgnored();
                var ignored = new BotThreadList();
                ignored.ResumeObject(state.GetProperty("Ignored"));
                ThreadContext.AddIgnored(ignored);

                ThreadContext.ClearHistory();
                var history = new BotThreadList();
                history.ResumeObject(state.GetProperty("History"));
                ThreadContext.AddHistory(history);

                JanePath = state.GetProperty("JanePath").GetString();
                HomeIP = state.GetProperty("HomeIP").GetString();

                SearchConditions = state.GetProperty("SearchConditions").EnumerateArray().Select(conditionJson =>
                {
                    var condition = new SearchCondition();
                    condition.Resume(conditionJson);
                    return condition;
                }).ToList();

                UAMonaKeyPairs = state.GetProperty("UAMonaKeyPairs").EnumerateArray().Select(recodeJson =>
                {
                    var recode = recodeJson.GetString();
                    var ua = recode.Split(',')[0];
                    var mona = recode.Split(',')[1];
                    var used = recode.Split(',')[2];
                    return new BotUAMonaKeyPair(ua, mona, used == "1");
                }).ToList();

                var counter = 0;
                foreach (var item in state.GetProperty("ProxyWindowState").EnumerateArray())
                {
                    var deviceIndex = item.GetProperty("deviceIndex").GetInt32();
                    if (++counter > ClientList.Count)
                        ClientList.Add(new BotClient(ui, this, deviceIndex));
                    var client = ClientList[counter - 1];
                    client.Visible = item.GetProperty("Visible").GetBoolean();
                    client.WindowState = (FormWindowState)Enum.Parse(typeof(FormWindowState), item.GetProperty("WindowState").GetString());
                    var location = new Point(item.GetProperty("LocationX").GetInt32(), item.GetProperty("LocationY").GetInt32());
                    if (location.X != -32000 && location.Y != -32000)
                        client.Location = location;
                    if (ui.manager.autoStart & item.GetProperty("Working").GetBoolean())
                        autoStartProxies.Add(client);
                }

                if (ui.manager.autoStart && state.GetProperty("SearchWorking").GetBoolean())
                    autoStartSearch = true;

                AllowDuplicatePost = state.GetProperty("AllowDuplicatePost").GetBoolean();
            }
            catch { }
        }
    }
}
