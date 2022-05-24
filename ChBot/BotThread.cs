using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Web;

namespace ChBot
{
    public class BotThread
    {
        public long Key;
        public string Title;
        public int Res;
        public string Server;
        public string Bbs;
        public int Rank;
        public long Wrote;
        public bool Priority;
        public List<Dictionary<string, string>> ResListCache;
        public SearchCondition MatchedSearchCondition;

        //コンストラクタ
        public BotThread(long time, string title, string server, string bbs)
        {
            Key = time;
            Title = title;
            Server = server;
            Bbs = bbs;
            Res = 1;
            Rank = 1;
            Wrote = 0;
            Priority = false;
            ResListCache = null;
            MatchedSearchCondition = null;
        }

        //スレッドURLを解析
        public static Dictionary<string, object> ParseUrl(string url)
        {
            try
            {
                System.Text.RegularExpressions.Regex r1 = new System.Text.RegularExpressions.Regex(@"https?://(.*?)/", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string server = r1.Match(url).Groups[1].Value;
                System.Text.RegularExpressions.Regex r2 = new System.Text.RegularExpressions.Regex(@"/(\d+?)/$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                int time = int.Parse(r2.Match(url).Groups[1].Value);
                System.Text.RegularExpressions.Regex r3 = new System.Text.RegularExpressions.Regex(@"/([^/]+?)/\d+/$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string bbs = r3.Match(url).Groups[1].Value;
                Dictionary<string, object> data = new Dictionary<string, object>();
                data.Add("Server", server);
                data.Add("Time", time);
                data.Add("Bbs", bbs);
                return data;
            }
            catch
            {
                throw new UrlFormNotCorrectException();
            }
        }

        public BotThread Clone()
        {
            return new BotThread(Key, Title, Server, Bbs)
            {
                Res = Res,
                Rank = Rank,
                Wrote = Wrote,
                Priority = Priority,
                ResListCache = ResListCache,
                MatchedSearchCondition = MatchedSearchCondition
            };
        }

        //スレッドのURL
        public string Url
        {
            get
            {
                return "https://" + Server + "/test/read.cgi/" + Bbs + "/" + Key.ToString() + "/";
            }
        }

        public bool Equals(BotThread thread)
        {
            if (thread != null)
            {
                return this.Url == thread.Url;
            }
            else
            {
                return false;
            }
        }

        public dynamic getObject()
        {
            return new
            {
                Key,
                Title,
                Res,
                Server,
                Bbs,
                Rank,
                Wrote,
                Priority
            };
        }

        public void ResumeObject(JsonElement data)
        {
            Key = data.GetProperty("Key").GetInt64();
            Title = data.GetProperty("Title").GetString();
            Res = data.GetProperty("Res").GetInt32();
            Server = data.GetProperty("Server").GetString();
            Bbs = data.GetProperty("Bbs").GetString();
            Rank = data.GetProperty("Rank").GetInt32();
            Wrote = data.GetProperty("Wrote").GetInt64();
            Priority = data.GetProperty("Priority").GetBoolean();
        }
    }
}
