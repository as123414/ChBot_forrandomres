using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChBot
{
    public class SearchCondition
    {
        //検索モード
        public enum SearchModes { Url, Word, Trip }

        public SearchModes SearchMode { get; set; }
        public string Url { get; set; }
        public string Word { get; set; }
        public int MinRes { get; set; }
        public int MaxRes { get; set; }
        public long MinTime { get; set; }
        public long MaxTime { get; set; }
        public int KeyMod { get; set; }
        public int KeyRem { get; set; }
        public string BodySearchText { get; set; }
        public string NameSearchText { get; set; }
        public string IDMatchText { get; set; }
        public int MaxTarget { get; set; }
        public string OptionSearchText { get; set; }
        public int MinNo { get; set; }
        public int MaxNo { get; set; }
        public bool EverMatch { get; set; }
        public string Trip { get; set; }
        public int NeedMatchCount { get; set; }

        public BotThreadList everMatchList;
        public long lastHissiLoadTime;
        public BotThreadList hissiThreads;

        public SearchCondition()
        {
            SearchMode = SearchModes.Word;
            Url = "";
            Word = "";
            MinRes = 1;
            MaxRes = 1000;
            MinTime = 0;
            MaxTime = 10000000000;
            KeyMod = 1;
            KeyRem = 0;
            BodySearchText = "";
            NameSearchText = "";
            IDMatchText = "";
            MaxTarget = 1000;
            OptionSearchText = "";
            MinNo = 1;
            MaxNo = 10000;
            EverMatch = false;
            everMatchList = new BotThreadList();
            Trip = "";
            hissiThreads = new BotThreadList();
            lastHissiLoadTime = 0;
            NeedMatchCount = 1;
        }

        public bool IsMatchLiteCondition(BotThread thread)
        {
            if (SearchMode == SearchModes.Word)
            {
                return (IsMatchTitleWord(thread)
                    && thread.Res >= MinRes
                    && thread.Res <= MaxRes
                    && UnixTime.Now() - thread.Key >= MinTime
                    && UnixTime.Now() - thread.Key <= MaxTime
                    && thread.Key % KeyMod == KeyRem
                    && thread.Rank >= MinNo
                    && thread.Rank <= MaxNo) || everMatchList.Contains(thread);
            }
            else if (SearchMode == SearchModes.Url)
            {
                return thread.Url == Url || everMatchList.Contains(thread);
            }
            else
            {
                throw new Exception("Invalid search mode.");
            }
        }

        public bool IsMatchTitleWord(BotThread thread)
        {
            var isMathWord = false;
            thread.Priority = false;
            var words = Word.Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                if (word[0] != '-' && word[0] != '!' && Regex.IsMatch(thread.Title, word))
                    isMathWord = true;

                if (word[0] == '!' && word.Length >= 2 && Regex.IsMatch(thread.Title, word.Substring(1)))
                {
                    thread.Priority = true;
                    isMathWord = true;
                }
            }

            if (words.Where(word => word[0] != '-').Count() == 0)
                isMathWord = true;

            foreach (string word in words)
            {
                if (word.Length >= 2 && word[0] == '-' && Regex.IsMatch(thread.Title, word.Substring(1)))
                {
                    isMathWord = false;
                }
            }

            return isMathWord;
        }

        public async Task<bool> IsMatchFullCondition(BotThread thread, string ApiSid)
        {
            var matched = everMatchList.Contains(thread);
            if (!matched)
            {
                if (!IsMatchLiteCondition(thread))
                {
                    matched = false;
                }
                else
                {
                    if (Trip != "" && UnixTime.Now() - lastHissiLoadTime > 60)
                    {
                        hissiThreads = await Network.GetTripWroteThreadList(DateTime.Now.ToString("yyyyMMdd"), Trip, thread.Bbs);
                        lastHissiLoadTime = UnixTime.Now();
                    }

                    if (Trip != "" && !hissiThreads.Contains(thread))
                    {
                        matched = false;
                    }
                    else
                    {
                        var resList = Network.DatToDetailResList(await Network.GetDat(thread, ApiSid));
                        thread.ResListCache = resList;

                        matched = resList.Where(res =>
                            int.Parse(res["No"]) <= MaxTarget
                            && (BodySearchText == "" || Regex.IsMatch(res["Message"], BodySearchText, RegexOptions.Singleline))
                            && (NameSearchText == "" || Regex.IsMatch(res["Name"], NameSearchText))
                            && (IDMatchText == "" || Regex.IsMatch(res["ID"], IDMatchText))
                            && (OptionSearchText == "" || Regex.IsMatch(res["Option"], OptionSearchText))
                        ).Count() >= NeedMatchCount;
                    }
                }
            }

            if (matched)
            {
                if (EverMatch && !everMatchList.Contains(thread))
                    everMatchList.Add(thread);

                return true;
            }
            else
            {
                return false;
            }
        }

        public dynamic GetObject()
        {
            return new
            {
                SearchMode,
                Url,
                Word,
                MinRes,
                MaxRes,
                MinTime,
                MaxTime,
                KeyMod,
                KeyRem,
                BodySearchText,
                NameSearchText,
                IDMatchText,
                MaxTarget,
                OptionSearchText,
                MinNo,
                MaxNo,
                EverMatch,
                Trip,
                NeedMatchCount
            };
        }

        public void Resume(JsonElement settings)
        {
            try
            {
                SearchMode = (SearchModes)Enum.Parse(typeof(SearchModes), settings.GetProperty("SearchMode").GetString());
                Url = settings.GetProperty("Url").GetString();
                Word = settings.GetProperty("Word").GetString();
                MinRes = settings.GetProperty("MinRes").GetInt32();
                MaxRes = settings.GetProperty("MaxRes").GetInt32();
                MinTime = settings.GetProperty("MinTime").GetInt64();
                MaxTime = settings.GetProperty("MaxTime").GetInt64();
                KeyMod = settings.GetProperty("KeyMod").GetInt32();
                KeyRem = settings.GetProperty("KeyRem").GetInt32();
                BodySearchText = settings.GetProperty("BodySearchText").GetString();
                NameSearchText = settings.GetProperty("NameSearchText").GetString();
                IDMatchText = settings.GetProperty("IDMatchText").GetString();
                MaxTarget = settings.GetProperty("MaxTarget").GetInt32();
                OptionSearchText = settings.GetProperty("OptionSearchText").GetString();
                MinNo = settings.GetProperty("MinNo").GetInt32();
                MaxNo = settings.GetProperty("MaxNo").GetInt32();
                EverMatch = settings.GetProperty("EverMatch").GetBoolean();
                Trip = settings.GetProperty("Trip").GetString();
                NeedMatchCount = settings.GetProperty("NeedMatchCount").GetInt32();
            }
            catch { }
        }
    }
}
