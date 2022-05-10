using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Web;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Management.Automation;
using System.Text.Json;
using System.Web.Security;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Drawing;

namespace ChBot
{
    public enum UA { ChMate, X2chGear, JaneStyleWin }

    public static class Network
    {
        private static string ReadAppKey = "8yoeAcaLXiEY1FjEuJBKgkPxirkDqn";
        private static string ReadHMKey = "F7vFmWCGd5Gzs8hc03NpRvw4bPwMz3";
        private static string lineAuth = "";
        private static string imgUploadServer = "";
        private static string lineRecieveWebhookUrl = "";
        private static string ipGetUrl = "";
        public static List<string> DeviceIDList = new List<string>();
        private static List<IPEndPoint> postIPEndPointList = new List<IPEndPoint>();

        static Network()
        {
            ServicePointManager.Expect100Continue = false;
        }

        public static void LoadConfig()
        {
            var lines = File.ReadAllLines(@"config.txt");
            lineAuth = lines[0].Trim();
            imgUploadServer = lines[1].Trim();
            lineRecieveWebhookUrl = lines[2].Trim();
            ipGetUrl = lines[3].Trim();
            DeviceIDList.AddRange(lines[4].Trim().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            postIPEndPointList.AddRange(lines[5].Trim().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => new IPEndPoint(new IPAddress(s.Trim().Split('.').Select(ss => byte.Parse(ss)).ToArray()), 0)));
        }

        //APIのSIDを取得
        public static async Task<string> GetApiSid(string user = "", string password = "")
        {
            var CT = "1234567890";
            var message = ReadAppKey + CT;
            var messageData = Encoding.UTF8.GetBytes(message);
            var keyData = Encoding.UTF8.GetBytes(ReadHMKey);
            var hmac = new HMACSHA256(keyData);
            var bs = hmac.ComputeHash(messageData);
            hmac.Clear();
            var HB = BitConverter.ToString(bs).ToLower().Replace("-", "");

            var parameters = "ID=" + user + "&PW=" + password + "&KY=" + ReadAppKey + "&CT=" + CT + "&HB=" + HB;
            var data = Encoding.ASCII.GetBytes(parameters);

            var url = "https://api.5ch.net/v1/auth/";
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.KeepAlive = false;
            webReq.Method = "POST";
            webReq.ContentType = "application/x-www-form-urlencoded";
            webReq.Headers.Set("X-2ch-UA", "2chMate/0.8.10.153");
            webReq.CookieContainer = new CookieContainer();

            using (var reqStream = await webReq.GetRequestStreamAsync().Timeout(10000))
            {
                await reqStream.WriteAsync(data, 0, data.Length).Timeout(10000);
            }

            var body = "";
            using (var webRes = (HttpWebResponse)await webReq.GetResponseAsync().Timeout(10000))
            using (var stream = webRes.GetResponseStream())
            {
                using (var sr = new StreamReader(stream, Encoding.GetEncoding("Shift_JIS")))
                    body = await sr.ReadToEndAsync().Timeout(10000);
            }

            if (Regex.IsMatch(body, @"^ng"))
                throw new ApiLoginFailedException();

            var apiSid = body.Split(':')[1];

            if (user != "")
            {
                var thread = new BotThread(1650280278, "", "mi.5ch.net", "news4vip");
                var dat = await GetDat(thread, apiSid, enableRange: false);
                if (Regex.IsMatch(dat, @"^5ちゃんねる\s★<><>\d+/\d+/\d+\(.+?\)\s00:00:00.00\sID:\?{3,}<>\sこのスレッドは過去ログです。", RegexOptions.Multiline))
                {
                    throw new RoninLoginFailedException();
                }
            }

            return apiSid;
        }

        public static async Task<string> GetMonaKey(string userAgent, int deviceIndex)
        {
            var thread = new BotThread(1509713280, "", "mi.5ch.net", "news4vip");
            try
            {
                await Post(thread, "test", "", "", userAgent, "00000000-0000-0000-0000-000000000000", deviceIndex);
            }
            catch (SigFailureException er)
            {
                //await Task.Delay(4000);
                return er.NewMonaKey;
            }

            return "";
        }

        //スレ覧取得
        public static async Task<BotThreadList> GetThreadList(string url)
        {
            try
            {
                BotThreadList threads = new BotThreadList();

                Regex r1 = new Regex(@"https?://(.*?)/(.+)/$");
                string server = r1.Match(url).Groups[1].Value;
                string bbs = r1.Match(url).Groups[2].Value;

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://" + server + "/" + bbs + "/subject.txt");
                req.KeepAlive = false;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36 Edg/96.0.1054.62";

                string html;
                using (WebResponse res = await req.GetResponseAsync().Timeout(10000))
                using (Stream st = res.GetResponseStream())
                using (StreamReader srm = new StreamReader(st, Encoding.GetEncoding("Shift-JIS")))
                    html = await srm.ReadToEndAsync().Timeout(10000);

                string[] subjects = html.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < subjects.Length; i++)
                {
                    string subject = subjects[i];
                    var split1 = subject.Split(new[] { "<>", "," }, 2, StringSplitOptions.None);
                    var key = long.Parse(Regex.Match(split1[0], @"^(?<key>\d+)\.(dat|cgi)$").Groups["key"].Value);
                    int res = int.Parse(Regex.Match(split1[1], @"\((?<res>\d+)\)$").Groups["res"].Value);
                    var rep1 = Regex.Replace(split1[1], @"\t?\s\(\d+\)$", "");
                    var rep2 = Regex.Replace(rep1, @"&#169;2ch\.net$", "");
                    var title = Regex.Replace(rep2, @"\s\[[^\]]+\]$", "");
                    threads.Add(new BotThread(key, HttpUtility.HtmlDecode(title), server, bbs)
                    {
                        Res = res,
                        Rank = i + 1
                    });
                }
                return threads;
            }
            finally
            {

            }
        }

        public static bool isSjisCharactor(char c)
        {
            var encSjis = Encoding.GetEncoding("Shift_JIS");
            byte[] b = encSjis.GetBytes(c.ToString());
            string strSjis = encSjis.GetString(b);
            return strSjis == c.ToString();
        }

        public static string EscapeEmoji(string input)
        {
            return string.Join("", input.Select(c => isSjisCharactor(c) ? c.ToString() : "&#" + (int)c + ";"));
        }

        public static string Encode(string input)
        {
            var tmp = HttpUtility.UrlEncode(input);
            return Regex.Replace(tmp, @"%[0-9a-f]{2}", s => s.Value.ToUpper());
        }

        public static UA getProfileNumber(string userAgent)
        {
            if (userAgent.Contains("2chMate/"))
                return UA.ChMate;
            else if (userAgent.Contains("2chGear/"))
                return UA.X2chGear;
            else if (userAgent.Contains("JaneStyle/"))
                return UA.JaneStyleWin;
            else
                throw new Exception("Unimplemented useragent.");
        }

        //レス投稿
        public static async Task<string> Post(BotThread thread, string message, string name, string mail, string userAgent, string monakey, int deviceIndex)
        {
            var profile = getProfileNumber(userAgent);
            var bbs = thread.Bbs;
            var server = thread.Server;
            var key = thread.Key;
            var time = UnixTime.Now();
            var referer = "";
            var pDict = new Dictionary<string, string>();

            switch (profile)
            {
                case UA.ChMate:
                    referer = "https://" + server + "/test/read.cgi/" + bbs + "/" + time + "/";
                    pDict.Add("FROM", name);
                    pDict.Add("mail", mail);
                    pDict.Add("MESSAGE", message);
                    pDict.Add("bbs", bbs);
                    pDict.Add("key", key.ToString());
                    pDict.Add("submit", "書き込む");
                    pDict.Add("time", time.ToString());
                    break;
                case UA.X2chGear:
                    referer = "https://" + server + "/test/read.cgi/" + bbs + "/" + time + "/";
                    pDict.Add("MESSAGE", message);
                    pDict.Add("bbs", bbs);
                    pDict.Add("mail", mail);
                    pDict.Add("feature", "confirmed");
                    pDict.Add("submit", "書き込む");
                    pDict.Add("subject", "");
                    pDict.Add("FROM", name);
                    pDict.Add("time", time.ToString());
                    pDict.Add("key", key.ToString());
                    break;
                case UA.JaneStyleWin:
                    referer = "https://" + server + "/test/bbs.cgi";
                    pDict.Add("submit", "書き込む");
                    pDict.Add("FROM", name);
                    pDict.Add("mail", mail);
                    pDict.Add("MESSAGE", message);
                    pDict.Add("bbs", bbs);
                    pDict.Add("key", key.ToString());
                    pDict.Add("time", time.ToString());
                    break;
                default:
                    throw new Exception("Unimplemented useragent.");
            }

            var parameters = string.Join("&", pDict.Select(p => p.Key + "=" + Encode(p.Value)));
            var data = Encoding.ASCII.GetBytes(parameters);

            var mona = new List<string>() { "" };
            var html = await Send(server, data, referer, bbs, key, name, mail, message, "", time, userAgent, monakey, deviceIndex, mona: mona);

            var body = Regex.Match(html, @"<body[^>]*>(.+)</body>", RegexOptions.Singleline).Value;

            if (html.IndexOf("<title>書きこみました") == -1)
            {
                if (body.Contains("<form"))
                    throw new SigFailureException(body, mona[0]);
                else
                    throw new PostFailureException(body);
            }

            return body;
        }

        //スレ立て
        public static async Task<string> Build(string server, string bbs, string title, string message, string name, string mail, string userAgent, string monaKey, int deviceIndex)
        {
            var profile = getProfileNumber(userAgent);
            var time = UnixTime.Now();
            var referer = "";
            var pDict = new Dictionary<string, string>();

            switch (profile)
            {
                case UA.ChMate:
                    referer = "https://" + server + "/" + bbs + "/";
                    pDict.Add("subject", title);
                    pDict.Add("FROM", name);
                    pDict.Add("mail", mail);
                    pDict.Add("MESSAGE", message);
                    pDict.Add("bbs", bbs);
                    pDict.Add("submit", "新規スレッド作成");
                    pDict.Add("time", time.ToString());
                    break;
                case UA.X2chGear:
                    referer = "https://" + server + "/test/bbs.cgi";
                    pDict.Add("MESSAGE", message);
                    pDict.Add("bbs", bbs);
                    pDict.Add("mail", mail);
                    pDict.Add("submit", "新規スレッド作成");
                    pDict.Add("subject", title);
                    pDict.Add("time", time.ToString());
                    pDict.Add("FROM", name);
                    pDict.Add("key", "");
                    break;
                case UA.JaneStyleWin:
                    referer = "https://" + server + "/test/bbs.cgi";
                    pDict.Add("subject", title);
                    pDict.Add("submit", "書き込む");
                    pDict.Add("FROM", name);
                    pDict.Add("mail", mail);
                    pDict.Add("MESSAGE", message);
                    pDict.Add("bbs", bbs);
                    pDict.Add("time", time.ToString());
                    break;
                default:
                    throw new Exception("Unimplemented useragent.");
            }

            var parameters = string.Join("&", pDict.Select(p => p.Key + "=" + Encode(p.Value)));
            var data = Encoding.ASCII.GetBytes(parameters);

            var str = await Send(server, data, referer, bbs, -1, name, mail, message, title, time, userAgent, monaKey, deviceIndex);

            string html = "";
            Regex r = new Regex(@"<body[^>]*>(.+)</body>", RegexOptions.Singleline);
            MatchCollection mc = r.Matches(str);
            foreach (Match m in mc)
            {
                html = m.Groups[1].Value;
            }

            if (str.IndexOf("書きこみが終わりました。") == -1)
            {
                throw new PostFailureException(html);
            }

            return html;
        }

        //bbs.cgiに送信
        private static async Task<string> Send(string server, byte[] data, string referer, string bbs, long key, string name, string mail, string message, string title, long time, string userAgent, string monakey, int deviceIndex, List<string> mona = null)
        {
            var profile = getProfileNumber(userAgent);
            string nonce, HMKey, AppKey;
            switch (profile)
            {
                case UA.ChMate:
                    AppKey = "8yoeAcaLXiEY1FjEuJBKgkPxirkDqn";
                    HMKey = "F7vFmWCGd5Gzs8hc03NpRvw4bPwMz3";
                    nonce = time + "." + Generator.getRandomNumber(0, 9).ToString() + Generator.getRandomNumber(0, 9).ToString() + Generator.getRandomNumber(0, 9).ToString();
                    break;
                case UA.X2chGear:
                    AppKey = "hmCELmps15cHN8mL7viOH23nH24EwP";
                    HMKey = "fOOxqHdfCactc639EMjCF1mUAkG1Lx";
                    nonce = time.ToString();
                    break;
                case UA.JaneStyleWin:
                    AppKey = "a6kwZ1FHfwlxIKJWCq4XQQnUTqiA1P";
                    HMKey = "ZDzsNQ7PcOOGE2mXo145X6bt39WMz6";
                    nonce = time.ToString();
                    break;
                default:
                    throw new Exception("Unimplemented useragent.");
            }

            var mList = new List<string>();
            mList.Add(bbs);
            mList.Add(key >= 0 ? key.ToString() : "");
            mList.Add(time.ToString());
            mList.Add(name);
            mList.Add(mail);
            mList.Add(message);
            mList.Add(title);
            mList.Add(userAgent);
            mList.Add(monakey);
            mList.Add("");
            mList.Add(nonce);
            var mes = string.Join("<>", mList);
            var messageData = Encoding.UTF8.GetBytes(mes);
            var keyData = Encoding.UTF8.GetBytes(HMKey);

            var hmac = new HMACSHA256(keyData);
            var bs = hmac.ComputeHash(messageData);
            hmac.Clear();

            HttpWebRequest webReq;
            switch (profile)
            {
                case UA.ChMate:
                    webReq = (HttpWebRequest)WebRequest.Create("https://" + server + "/test/bbs.cgi?guid=ON");
                    webReq.UserAgent = userAgent;
                    webReq.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    webReq.Headers.Set("X-2ch-UA", "2chMate/0.8.10.153");
                    webReq.Headers.Set("X-APIKey", AppKey);
                    webReq.Headers.Set("X-MonaKey", monakey);
                    webReq.Headers.Set("X-PostNonce", nonce);
                    webReq.Headers.Set("X-PostSig", BitConverter.ToString(bs).ToLower().Replace("-", ""));
                    webReq.Headers.Set("Accept-Encoding", "gzip");
                    webReq.AutomaticDecompression = DecompressionMethods.GZip;
                    webReq.Referer = referer;
                    break;
                case UA.X2chGear:
                    webReq = (HttpWebRequest)WebRequest.Create("https://" + server + "/test/bbs.cgi");
                    webReq.UserAgent = userAgent;
                    webReq.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    webReq.Headers.Set("X-APIKey", AppKey);
                    webReq.Headers.Set("X-MonaKey", monakey);
                    webReq.Headers.Set("X-PostNonce", nonce);
                    webReq.Headers.Set("X-PostSig", BitConverter.ToString(bs).ToLower().Replace("-", ""));
                    webReq.Headers.Set("HTTP_X_MONAKEY_PERIOD", "30");
                    webReq.Headers.Set("Accept-Encoding", "gzip");
                    webReq.AutomaticDecompression = DecompressionMethods.GZip;
                    webReq.Referer = referer;
                    break;
                case UA.JaneStyleWin:
                    webReq = (HttpWebRequest)WebRequest.Create("https://" + server + "/test/bbs.cgi");
                    webReq.UserAgent = userAgent;
                    webReq.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    webReq.Headers.Set("X-APIKey", AppKey);
                    webReq.Headers.Set("X-MonaKey", monakey);
                    webReq.Headers.Set("X-PostNonce", nonce);
                    webReq.Headers.Set("X-PostSig", BitConverter.ToString(bs).ToLower().Replace("-", ""));
                    webReq.Headers.Set("HTTP_X_MONAKEY_PERIOD", "30");
                    webReq.Headers.Set("Accept-Encoding", "gzip, identity");
                    webReq.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    webReq.Referer = referer;
                    break;
                default:
                    throw new Exception("Unimplemented useragent.");
            }

            if (deviceIndex >= 0)
            {
                webReq.Proxy = null;
                webReq.ServicePoint.BindIPEndPointDelegate = delegate (
                    ServicePoint servicePoint,
                    IPEndPoint remoteEndPoint,
                    int retryCount)
                {
                    return postIPEndPointList[deviceIndex];
                };
            }

            webReq.Host = server;
            webReq.ContentLength = data.Length;
            webReq.Method = "POST";
            webReq.KeepAlive = false;

            using (var reqStream = await webReq.GetRequestStreamAsync().Timeout(10000))
            {
                await reqStream.WriteAsync(data, 0, data.Length).Timeout(10000);
            }

            using (var webRes = await webReq.GetResponseAsync().Timeout(10000))
            using (var stream = webRes.GetResponseStream())
            {
                try
                {
                    if (mona != null)
                        mona[0] = webRes.Headers.Get("X-MonaKey");
                }
                catch { }

                using (var sr = new StreamReader(stream, Encoding.GetEncoding("Shift_JIS")))
                    return await sr.ReadToEndAsync().Timeout(10000);
            }
        }

        public static string GetRandomUseragent(UA profile)
        {
            switch (profile)
            {
                case UA.ChMate:
                    return "Monazilla/1.00 2chMate/0.8.10.153 Dalvik/2.1.0 (Linux; U; Android 12; " + Generator.makeAlphaRandom(Generator.getRandomNumber(3, 8)) + (Generator.getRandomNumber(0, 1) == 0 ? Generator.getRandomNumber(0, 100).ToString() : "") + " Build/" + Generator.getRandomNumber(0, 200) + "." + Generator.getRandomNumber(0, 200) + "." + Generator.getRandomNumber(0, 200) + ")";
                case UA.X2chGear:
                    return "Monazilla/1.00 2chGear/1.1.3 (Linux; Android 9; " + Generator.makeAlphaRandom(Generator.getRandomNumber(3, 8)) + (Generator.getRandomNumber(0, 1) == 0 ? Generator.getRandomNumber(0, 100).ToString() : "") + " Build/" + Generator.getRandomNumber(0, 200) + "." + Generator.getRandomNumber(0, 200) + "." + Generator.getRandomNumber(0, 200) + ")";
                case UA.JaneStyleWin:
                    return "Monazilla/1.00 JaneStyle/4.23 Windows/10.0.22000";
                default:
                    throw new Exception("Unimplemented useragent.");
            }
        }

        //DAT取得
        public static async Task<string> GetDat(BotThread thread, string apiSid, bool enableRange = true)
        {
            try
            {
                if (apiSid == "")
                    throw new NotSetSidException();

                var message = "/v1/" + thread.Server.Split('.')[0] + "/" + thread.Bbs + "/" + thread.Key.ToString() + apiSid + ReadAppKey;
                var messageData = Encoding.UTF8.GetBytes(message);
                var keyData = Encoding.UTF8.GetBytes(ReadHMKey);
                var hmac = new HMACSHA256(keyData);
                var bs = hmac.ComputeHash(messageData);
                hmac.Clear();
                var hobo = BitConverter.ToString(bs).ToLower().Replace("-", "");

                var parameters = "sid=" + apiSid + "&hobo=" + hobo + "&appkey=" + ReadAppKey;
                var data = Encoding.ASCII.GetBytes(parameters);

                var url = "https://api.5ch.net/v1/" + thread.Server.Split('.')[0] + "/" + thread.Bbs + "/" + thread.Key.ToString();
                var webReq = (HttpWebRequest)WebRequest.Create(url);
                webReq.KeepAlive = false;
                webReq.Method = "POST";
                webReq.AutomaticDecompression = DecompressionMethods.GZip;
                webReq.ContentType = "application/x-www-form-urlencoded";
                webReq.Headers.Set("X-2h-UA", "2chMate/0.8.10.153");

                var datPath = @"C://dat_cache/" + thread.Key + ".dat";
                if (File.Exists(datPath) && enableRange)
                {
                    var fi = new FileInfo(datPath);
                    //ファイルのサイズを取得
                    long filesize = fi.Length;
                    webReq.AddRange(filesize - 1);
                }

                using (var reqStream = await webReq.GetRequestStreamAsync().Timeout(10000))
                {
                    await reqStream.WriteAsync(data, 0, data.Length).Timeout(10000);
                }

                var dat = "";
                using (var webRes = await webReq.GetResponseAsync().Timeout(10000))
                using (var stream = webRes.GetResponseStream())
                using (var sr = new StreamReader(stream, Encoding.GetEncoding("Shift_JIS")))
                {
                    dat = await sr.ReadToEndAsync().Timeout(10000);
                }

                if (Regex.IsMatch(dat, "^ng"))
                    throw new DatGetFailureException(dat);

                if (enableRange)
                {
                    if (File.Exists(datPath))
                    {
                        File.AppendAllText(datPath, dat.TrimStart('\n'), Encoding.GetEncoding("Shift_JIS"));

                        return File.ReadAllText(datPath, Encoding.GetEncoding("Shift_JIS"));
                    }
                    else
                    {
                        File.WriteAllText(datPath, dat, Encoding.GetEncoding("Shift_JIS"));
                    }
                }

                return dat;
            }
            catch (WebException er)
            {
                if (er.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)er.Response;
                    if (errres.StatusCode == HttpStatusCode.NotImplemented)
                        return await GetDat(thread, apiSid, enableRange: false);
                    else
                        throw;
                }
                else
                {
                    throw;
                }
            }
            finally
            {

            }
        }

        public static string[] DatToResList(string rawDat)
        {
            string dat = rawDat;
            dat = new Regex(@"\n$").Replace(dat, "");

            string[] reses = dat.Split('\n');

            if (!Regex.IsMatch(reses[0], @"^\d+<>"))
            {
                reses[0] = string.Join("", reses[0].Reverse());
                string s2 = new Regex(@"^.+?><").Replace(reses[0], "><");
                reses[0] = string.Join("", s2.Reverse());

                for (int i = 0; i < reses.Length; i++)
                {
                    reses[i] = string.Join("", reses[i].Reverse());
                    reses[i] = new Regex(@"^><\s(.+?)\s><").Match(reses[i]).Groups[1].Value;
                    reses[i] = string.Join("", reses[i].Reverse());
                    reses[i] = new Regex(@"\s?<br>\s?").Replace(reses[i], "\r\n");
                    reses[i] = new Regex(@"<.+?>").Replace(reses[i], "");
                    reses[i] = HttpUtility.HtmlDecode(reses[i]);
                }
            }
            else
            {
                reses = reses.Select(res => Regex.Match(res, @".*?<>.*?<>.*?<>.*?<>(.*?)<>").Groups[1].Value).ToArray();
            }

            return reses;
        }

        public static List<Dictionary<string, string>> DatToDetailResList(string rawDat)
        {
            string[] lines = rawDat.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var reses2 = new List<Dictionary<string, string>>();
            if (!Regex.IsMatch(lines[0], @"^\d+<>"))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = Regex.Match(lines[i], @"^(.*?)<>(.*?)<>(.*?)<>(.*?)<>");

                    var name = HttpUtility.HtmlDecode(match.Groups[1].Value);
                    var mail = HttpUtility.HtmlDecode(match.Groups[2].Value);
                    var option = match.Groups[3].Value;

                    var idMatch = Regex.Match(option, @"ID:(\S+)");
                    var id = "";
                    if (idMatch.Success)
                        id = idMatch.Groups[1].Value;

                    var message = match.Groups[4].Value.Trim(' ', '\t');
                    message = Regex.Replace(message, @"\s?<br>\s?", "\r\n");
                    message = Regex.Replace(message, @"<.+?>", "");
                    message = HttpUtility.HtmlDecode(message);

                    reses2.Add(new Dictionary<string, string>() {
                        { "No", (i + 1).ToString() },
                        { "Name", name },
                        { "Mail", mail },
                        { "Option", option },
                        { "ID", id },
                        { "Message", message }
                    });
                }
            }
            else
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = Regex.Match(lines[i], @"^(.*?)<>(.*?)<>(.*?)<>(.*?)<>(.*?)<>.*?<>(.*)$");

                    var name = HttpUtility.HtmlDecode(match.Groups[2].Value);
                    var mail = HttpUtility.HtmlDecode(match.Groups[3].Value);
                    var option = match.Groups[4].Value;
                    var id = HttpUtility.HtmlDecode(match.Groups[6].Value);

                    var message = match.Groups[5].Value.Trim(' ', '\t');
                    message = Regex.Replace(message, @"\s?<br>\s?", "\r\n");
                    message = Regex.Replace(message, @"<.+?>", "");
                    message = HttpUtility.HtmlDecode(message);

                    reses2.Add(new Dictionary<string, string>() {
                        { "No", (i + 1).ToString() },
                        { "Name", name },
                        { "Mail", mail },
                        { "Option", option },
                        { "ID", id },
                        { "Message", message }
                    });
                }
            }

            return reses2;
        }

        public static async Task<string[]> GetResList(BotThread thread, string apiSid)
        {
            //string dat = await GetDat(thread, apiSid);
            //return DatToResList(dat);
            string jsonstr = await GetDat(thread, apiSid);
            return DatToResList(jsonstr);
        }

        public static async Task ChangeIP(int deviceIndex)
        {
            await Task.Run(() =>
            {
                using (var invoker = new RunspaceInvoke())
                {
                    invoker.Invoke("adb -s " + DeviceIDList[deviceIndex] + " shell svc data disable; adb -s " + DeviceIDList[deviceIndex] + " shell svc data enable\r\n");
                    //invoker.Invoke("adb -s " + DeviceIDList[deviceIndex] + " shell settings put global airplane_mode_on 1; adb -s " + DeviceIDList[deviceIndex] + " shell am broadcast -a android.intent.action.AIRPLANE_MODE; adb -s " + DeviceIDList[deviceIndex] + " shell settings put global airplane_mode_on 0; adb -s " + DeviceIDList[deviceIndex] + " shell am broadcast -a android.intent.action.AIRPLANE_MODE\r\n");
                }
            });

            await Task.Delay(500);
        }

        public static async Task DisableWiFi(int deviceIndex)
        {
            await Task.Run(() =>
            {
                using (var invoker = new RunspaceInvoke())
                {
                    invoker.Invoke("adb -s " + DeviceIDList[deviceIndex] + " shell svc wifi disable\r\n");
                }
            });

            await Task.Delay(500);
        }

        public static async Task<string> GetIPAddress(int deviceIndex)
        {
            var req = (HttpWebRequest)WebRequest.Create(ipGetUrl);
            req.KeepAlive = false;

            if (deviceIndex >= 0)
            {
                req.Proxy = null;
                req.ServicePoint.BindIPEndPointDelegate = delegate (
                    ServicePoint servicePoint,
                    IPEndPoint remoteEndPoint,
                    int retryCount)
                {
                    return postIPEndPointList[deviceIndex];
                };
            }

            using (var res = await req.GetResponseAsync().Timeout(10000))
            using (var st = res.GetResponseStream())
            {
                using (var srm = new StreamReader(st))
                {
                    return (await srm.ReadToEndAsync().Timeout(10000)).Trim();
                }
            }
        }

        public static async Task<string> SendLineMessage(string message)
        {
            var jsonstr = JsonSerializer.Serialize(new
            {
                messages = new[]{new
                    {
                        type = "text",
                        text = message
                    }}
            }, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            });

            jsonstr = Regex.Replace(jsonstr, @"([^\\])\\u3000", "$1　");
            jsonstr = Regex.Replace(jsonstr, @"^\\u3000", "　");

            var postDataBytes = Encoding.UTF8.GetBytes(jsonstr);

            var req = (HttpWebRequest)WebRequest.Create("https://api.line.me/v2/bot/message/broadcast");
            req.Method = "POST";
            req.KeepAlive = false;
            req.ContentType = "application/json";
            req.ContentLength = postDataBytes.Length;
            req.Headers.Set("Authorization", "Bearer " + lineAuth.Trim());

            //データをPOST送信するためのStreamを取得
            using (var reqStream = await req.GetRequestStreamAsync().Timeout(10000))
            {
                await reqStream.WriteAsync(postDataBytes, 0, postDataBytes.Length).Timeout(10000);
            }

            using (var res = await req.GetResponseAsync().Timeout(10000))
            using (var resStream = res.GetResponseStream())
            using (var sr = new StreamReader(resStream))
            {
                return await sr.ReadToEndAsync().Timeout(10000);
            }
        }

        public static async Task<string> SendLineImage(string url)
        {
            var jsonstr = JsonSerializer.Serialize(new
            {
                messages = new[]{new
                    {
                        type = "image",
                        originalContentUrl = url,
                        previewImageUrl = url
                    }}
            }, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            });

            jsonstr = Regex.Replace(jsonstr, @"([^\\])\\u3000", "$1　");
            jsonstr = Regex.Replace(jsonstr, @"^\\u3000", "　");

            var postDataBytes = Encoding.UTF8.GetBytes(jsonstr);

            var req = (HttpWebRequest)WebRequest.Create("https://api.line.me/v2/bot/message/broadcast");
            req.Method = "POST";
            req.KeepAlive = false;
            req.ContentType = "application/json";
            req.ContentLength = postDataBytes.Length;
            req.Headers.Set("Authorization", "Bearer " + lineAuth.Trim());

            //データをPOST送信するためのStreamを取得
            using (var reqStream = await req.GetRequestStreamAsync().Timeout(10000))
            {
                await reqStream.WriteAsync(postDataBytes, 0, postDataBytes.Length).Timeout(10000);
            }

            using (var res = await req.GetResponseAsync().Timeout(10000))
            using (var resStream = res.GetResponseStream())
            using (var sr = new StreamReader(resStream))
            {
                return await sr.ReadToEndAsync().Timeout(10000);
            }
        }

        public static async Task<string> GetLineMessage(WebProxy proxy = null)
        {
            var req = (HttpWebRequest)WebRequest.Create(lineRecieveWebhookUrl);
            req.KeepAlive = false;

            if (proxy != null)
                req.Proxy = proxy;

            using (var res = await req.GetResponseAsync().Timeout(10000))
            using (var st = res.GetResponseStream())
            {
                using (var srm = new StreamReader(st))
                {
                    var resp = (await srm.ReadToEndAsync().Timeout(10000)).Trim();
                    if (resp == "")
                        return "";
                    else
                        return Regex.Replace(resp, @"\s\S+$", "", RegexOptions.Singleline);
                }
            }
        }

        public static async Task RestartUsb(int deviceIndex)
        {
            await Task.Run(() =>
            {
                using (var invoker = new RunspaceInvoke())
                {
                    invoker.Invoke("adb -s " + DeviceIDList[deviceIndex] + " usb\r\n");
                }
            });

            await Task.Delay(12000);
        }

        public static async Task<BotThreadList> GetTripWroteThreadList(string date, string trip, string bbs)
        {
            var threadList = new BotThreadList();
            var parameters = "date=" + date + "&Trip=" + trip + "&Bord=" + bbs;
            var data = Encoding.ASCII.GetBytes(parameters);

            var url = "http://hissi.org/trip_search.php";
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.KeepAlive = false;
            webReq.Method = "POST";
            webReq.ContentType = "application/x-www-form-urlencoded";

            using (var reqStream = await webReq.GetRequestStreamAsync().Timeout(10000))
            {
                await reqStream.WriteAsync(data, 0, data.Length).Timeout(10000);
            }

            var html1 = "";
            using (var webRes = await webReq.GetResponseAsync().Timeout(10000))
            using (var stream = webRes.GetResponseStream())
            {
                using (var sr = new StreamReader(stream, Encoding.GetEncoding("Shift_JIS")))
                    html1 = await sr.ReadToEndAsync().Timeout(10000);
            }

            foreach (Match m in Regex.Matches(html1, @"read\.php/" + bbs + @"/\d{8}/.+?\.html"))
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://hissi.org/" + m.Value + "?thread=all");
                req.KeepAlive = false;

                string html2;
                using (WebResponse res = await req.GetResponseAsync().Timeout(10000))
                {
                    using (Stream st = res.GetResponseStream())
                    {
                        using (StreamReader srm = new StreamReader(st, Encoding.GetEncoding("Shift-JIS")))
                            html2 = await srm.ReadToEndAsync().Timeout(10000);
                    }
                }

                foreach (Match m2 in Regex.Matches(html2, @"<a href=https://(.+?\.5ch\.net)/test/read\.cgi/(.+?)/(\d+)/.*? target=""_blank"">(.+?) </a><br>"))
                {
                    var title = m2.Groups[4].Value;
                    var key = long.Parse(m2.Groups[3].Value);
                    var server = m2.Groups[1].Value;
                    var thread = new BotThread(key, HttpUtility.HtmlDecode(title), server, bbs);

                    if (!threadList.Contains(thread))
                        threadList.Add(thread);
                }
            }

            return threadList;
        }

        public static async Task<string> UploadCapture()
        {
            var fileName = "capture"
                + Generator.getRandomNumber(100, 999) + "-"
                + Generator.getRandomNumber(100, 999) + "-"
                + Generator.getRandomNumber(100, 999) + ".png";
            var bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

            using (var fs1 = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(new Point(0, 0), new Point(0, 0), bmp.Size);
                fs1.SetLength(0);
                bmp.Save(fs1, System.Drawing.Imaging.ImageFormat.Png);
            }

            var url = imgUploadServer + "upimg.php";
            var enc = Encoding.GetEncoding("shift_jis");
            var boundary = Environment.TickCount.ToString();

            //WebRequestの作成
            var req = (HttpWebRequest)WebRequest.Create(url);
            //メソッドにPOSTを指定
            req.Method = "POST";
            //ContentTypeを設定
            req.ContentType = "multipart/form-data; boundary=" + boundary;

            //POST送信するデータを作成
            var postData = "--" + boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"comment\"\r\n\r\n" +
                    "これは、テストです。\r\n" +
                    "--" + boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"upimg\"; filename=\"" +
                        fileName + "\"\r\n" +
                    "Content-Type: application/octet-stream\r\n" +
                    "Content-Transfer-Encoding: binary\r\n\r\n";
            //バイト型配列に変換
            var startData = enc.GetBytes(postData);
            postData = "\r\n--" + boundary + "--\r\n";
            var endData = enc.GetBytes(postData);

            //送信するファイルを開く
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {

                //POST送信するデータの長さを指定
                req.ContentLength = startData.Length + endData.Length + fs.Length;

                //データをPOST送信するためのStreamを取得
                using (var reqStream = await req.GetRequestStreamAsync().Timeout(10000))
                {
                    //送信するデータを書き込む
                    await reqStream.WriteAsync(startData, 0, startData.Length).Timeout(10000);
                    //ファイルの内容を送信
                    var readData = new byte[0x1000];
                    var readSize = 0;
                    while (true)
                    {
                        readSize = await fs.ReadAsync(readData, 0, readData.Length);
                        if (readSize == 0)
                            break;
                        await reqStream.WriteAsync(readData, 0, readSize).Timeout(10000);
                    }
                    await reqStream.WriteAsync(endData, 0, endData.Length).Timeout(10000);
                }
            }

            await req.GetResponseAsync().Timeout(10000);
            return imgUploadServer + fileName;
        }
    }

    public static class TaskExtention
    {
        public static async Task Timeout(this Task task, int timeout)
        {
            var delay = Task.Delay(timeout);
            if (await Task.WhenAny(task, delay) == delay)
            {
                throw new TimeoutException();
            }
        }

        public static async Task<T> Timeout<T>(this Task<T> task, int timeout)
        {
            await ((Task)task).Timeout(timeout);
            return await task;
        }
    }

    public class UAMonaKeyPair
    {
        public string UA { get; set; }
        public string MonaKey { get; set; }

        public UAMonaKeyPair(string UA, string MonaKey)
        {
            this.UA = UA;
            this.MonaKey = MonaKey;
        }
    }
}
