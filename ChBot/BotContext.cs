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

        public string Board { get; set; }
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
        public string UserAgent { get; set; }
        public string MonaKey { get; set; }

        public List<UAMonaKeyPair> UAMonaKeyPairs { get; set; }

        public BotClient client { get; set; }

        public List<SearchCondition> SearchConditions { get; set; }

        public bool Working { get => client.Working; }

        public BotInstance ui;
        public BotLoginer Loginer;
        public List<BotClient> autoStartProxies;

        public BotContext(BotInstance ui)
        {
            this.ui = ui;
            ThreadContext = new BotThreadContext();
            autoStartProxies = new List<BotClient>();
            JanePath = "";
            ListMode = ListModes.Search;
            Loginer = new BotLoginer(ui, this);
            MonaKey = "";
            UserAgent = "";
            UAMonaKeyPairs = new List<UAMonaKeyPair>();
            ResetSettings();
            client = new BotClient(ui, this);
        }

        //デフォルトプロファイルの復元
        private void ResetSettings()
        {
            Board = "https://mi.5ch.net/news4vip/";
            Message = "てす";
            Mail = "";
            Name = "";
            Interval = 20;
            User = "";
            HomeIP = "";
            Password = "";
            SearchConditions = new List<SearchCondition>() { new SearchCondition() };
        }

        public void Login()
        {
            Loginer.Login();
        }

        public async Task StartAttack()
        {
            client.Start();
        }

        public async Task StopAttack()
        {
            await client.Stop();
        }

        public async Task GatherMonaKey()
        {
            UAMonaKeyPairs = new List<UAMonaKeyPair>();
            for (var i = 0; i < 50; i++)
            {
                if (i % 5 == 0)
                    await Network.ChangeIP();

                var ua = Network.GetRandomUseragent(UA.ChMate);
                var mona = await Network.GetMonaKey(ua);
                /*var thread = new BotThread(1509713280, "", "mi.5ch.net", "news4vip");
                try
                {
                    await Network.Post(thread, "test", "", "", ua, mona);
                }
                catch (PostFailureException) { }*/
                var pair = new UAMonaKeyPair(ua, mona);
                UAMonaKeyPairs.Add(pair);
                Console.WriteLine(ua + "," + mona);
            }
        }

        //メッセージ欄にランダム文字列をセット
        public async Task SetNewMessage()
        {
            Message = await Generator.getResString(Generator.Kinds.AsciiKanji, this);
        }

        //スレッド検索
        public async Task SearchThread()
        {
            var allThreads = await Network.GetThreadList(Board);
            var searchResult = allThreads.Where(thread => SearchConditions.Any(condition => condition.IsMatchLiteCondition(thread))).ToBotThreadList();
            searchResult.Sort();
            var current = ThreadContext.GetCurrent();
            ThreadContext.ClearEnabled();
            ThreadContext.AddEnabled(searchResult);
            ThreadContext.SearchResult = searchResult;
            ThreadContext.DeleteUnneccesaryIgnored();
            if (current != null)
            {
                ThreadContext.SetCurrent(current);
            }
            else
            {
                ThreadContext.SetCurrent(ThreadContext.EnabledAt(0), true);
            }
        }

        public async Task FullSearchThread(Action<int, int> report = null)
        {
            var allThreads = await Network.GetThreadList(Board);
            var searchResult = new BotThreadList();
            for (var i = 0; i < allThreads.Count; i++)
            {
                var thread = allThreads[i];
                var isMatch = false;
                foreach (var condition in SearchConditions)
                {
                    if (await condition.IsMatchFullCondition(thread, ApiSid))
                    {
                        isMatch = true;
                        break;
                    }
                }
                if (isMatch)
                    searchResult.Add(thread);

                if (report != null)
                {
                    report(i, allThreads.Count);
                }
            }
            searchResult.Sort();
            var current = ThreadContext.GetCurrent();
            ThreadContext.ClearEnabled();
            ThreadContext.AddEnabled(searchResult);
            ThreadContext.SearchResult = searchResult;
            ThreadContext.DeleteUnneccesaryIgnored();
            if (current != null)
            {
                ThreadContext.SetCurrent(current);
            }
            else
            {
                ThreadContext.SetCurrent(ThreadContext.EnabledAt(0), true);
            }
        }

        //プロファイル保存
        public void SaveSettings()
        {
            var settings = new
            {
                Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                Board,
                Message,
                Mail,
                Name,
                Interval,
                User,
                Password,
                HomeIP,
                SearchConditions
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

                Board = settings["Board"].GetString();
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
            }
            catch { }
        }

        //匿名クラスで状態データを取得
        public dynamic GetState()
        {
            return new
            {
                Board,
                Message,
                Mail,
                Name,
                Interval,
                User,
                Password,
                HomeIP,
                SearchConditions,

                ListMode,
                JanePath,
                ThreadContext.Direction,
                SearchResult = ThreadContext.SearchResult.Clone().getObject(),
                Enabled = ThreadContext.GetEnabled().getObject(),
                Ignored = ThreadContext.GetIgnored().getObject(),
                Current = ThreadContext.GetCurrent()?.getObject(),
                History = ThreadContext.GetHistory().getObject(),
                Working,
                ProxyWindowState = new
                {
                    Visible = client.Visible,
                    WindowState = client.WindowState,
                    LocationX = client.Location.X,
                    LocationY = client.Location.Y,
                },
                UAMonaKeyPairs = UAMonaKeyPairs.Select(pair => pair.UA + "," + pair.MonaKey)
            };
        }

        //JSON形式の状態データから復元
        public void ResumeState(JsonElement state)
        {
            try
            {
                Board = state.GetProperty("Board").GetString();
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

                var current = new BotThread(0, "", "", "");
                if (state.GetProperty("Current").ValueKind == JsonValueKind.Null)
                    current = null;
                else
                    current.ResumeObject(state.GetProperty("Current"));
                ThreadContext.SetCurrent(current);

                JanePath = state.GetProperty("JanePath").GetString();
                HomeIP = state.GetProperty("HomeIP").GetString();

                if (ui.manager.autoStart & state.GetProperty("Working").GetBoolean())
                {
                    autoStartProxies.Add(client);
                }

                var item = state.GetProperty("ProxyWindowState");
                client.Visible = item.GetProperty("Visible").GetBoolean();
                client.WindowState = (FormWindowState)Enum.Parse(typeof(FormWindowState), item.GetProperty("WindowState").GetString());
                var location = new Point(item.GetProperty("LocationX").GetInt32(), item.GetProperty("LocationY").GetInt32());
                if (location.X != -32000 && location.Y != -32000)
                    client.Location = location;

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
                   return new UAMonaKeyPair(ua, mona);
               }).ToList();
            }
            catch { }
        }
    }
}
