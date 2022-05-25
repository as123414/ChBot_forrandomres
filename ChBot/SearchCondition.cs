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

        public enum PowerSetModes { AgeAnchor, AgeAnchorMatsubi, AnchorTrip }
        public enum NameGenerateModes { Manual, Kote }

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
        public bool Enabled { get; set; }
        public string Board { get; set; }
        public Generator.Kinds MessageGenerateKind { get; set; }
        public PowerSetModes PowerSetMode { get; set; }
        public NameGenerateModes NameGenerateMode { get; set; }
        public char AvoidMatsubi { get; set; }
        public string ManualName { get; set; }

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
            Trip = "";
            hissiThreads = new BotThreadList();
            lastHissiLoadTime = 0;
            NeedMatchCount = 1;
            Enabled = true;
            Board = "https://mi.5ch.net/news4vip/";
            MessageGenerateKind = Generator.Kinds.AsciiKanji;
            PowerSetMode = PowerSetModes.AgeAnchor;
            NameGenerateMode = NameGenerateModes.Manual;
            AvoidMatsubi = 'a';
            ManualName = "";
        }

        public bool IsMatchLiteCondition(BotThread thread)
        {
            if (!Enabled)
                throw new Exception("Disabled search condition.");

            if (SearchMode == SearchModes.Word)
            {
                return (IsMatchTitleWord(thread)
                    && "https://" + thread.Server + "/" + thread.Bbs + "/" == Board
                    && thread.Res >= MinRes
                    && thread.Res <= MaxRes
                    && UnixTime.Now() - thread.Key >= MinTime
                    && UnixTime.Now() - thread.Key <= MaxTime
                    && thread.Key % KeyMod == KeyRem
                    && thread.Rank >= MinNo
                    && thread.Rank <= MaxNo);
            }
            else if (SearchMode == SearchModes.Url)
            {
                return thread.Url == Url;
            }
            else
            {
                throw new Exception("Invalid search mode.");
            }
        }

        public bool IsMatchTitleWord(BotThread thread)
        {
            if (!Enabled)
                throw new Exception("Disabled search condition.");

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

        public async Task<bool> IsMatchMiddleCondition(BotThread thread)
        {
            if (!Enabled)
                throw new Exception("Disabled search condition.");

            if (!IsMatchLiteCondition(thread))
            {
                return false;
            }
            else
            {
                if (Trip != "" && UnixTime.Now() - lastHissiLoadTime > 60)
                {
                    hissiThreads = await Network.GetTripWroteThreadList(DateTime.Now.ToString("yyyyMMdd"), Trip, thread.Bbs);
                    lastHissiLoadTime = UnixTime.Now();
                }

                return Trip == "" || hissiThreads.Contains(thread);
            }
        }

        public async Task<bool> IsMatchFullCondition(BotThread thread, string ApiSid)
        {
            if (!Enabled)
                throw new Exception("Disabled search condition.");

            if (!await IsMatchMiddleCondition(thread))
            {
                return false;
            }
            else
            {
                var resList = Network.DatToDetailResList(await Network.GetDat(thread, ApiSid));
                thread.ResListCache = resList;

                return resList.Where(res =>
                     int.Parse(res["No"]) <= MaxTarget
                     && (BodySearchText == "" || Regex.IsMatch(res["Message"], BodySearchText, RegexOptions.Singleline))
                     && (NameSearchText == "" || Regex.IsMatch(res["Name"], NameSearchText))
                     && (IDMatchText == "" || Regex.IsMatch(res["ID"], IDMatchText))
                     && (OptionSearchText == "" || Regex.IsMatch(res["Option"], OptionSearchText))
                 ).Count() >= NeedMatchCount;
            }
        }

        public async Task<double> GetPower(BotContext context, BotThread thread)
        {
            if (thread.ResListCache == null)
                thread.ResListCache = Network.DatToDetailResList(await Network.GetDat(thread, context.ApiSid));
            if (PowerSetMode == PowerSetModes.AgeAnchor)
            {
                var anchorResList = thread.ResListCache.Where(res => Regex.IsMatch(res["Message"], @"[>＞]", RegexOptions.Singleline));
                var power = anchorResList.Aggregate(0.0, (p, res) => 1.0 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                var ageResList = thread.ResListCache.Where(res => res["Mail"] != "sage");
                power += ageResList.Aggregate(0.0, (p, res) => 0.1 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                return power;
            }
            else if (PowerSetMode == PowerSetModes.AgeAnchorMatsubi)
            {
                var anchorResList = thread.ResListCache.Where(res => Regex.IsMatch(res["Message"], @"[>＞]", RegexOptions.Singleline));
                var power = anchorResList.Aggregate(0.0, (p, res) => 1.0 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                var ageResList = thread.ResListCache.Where(res => res["Mail"] != "sage");
                power += ageResList.Aggregate(0.0, (p, res) => 0.1 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                var sageMatsubiResList = thread.ResListCache.Where(res => res["Mail"] == "sage" && Regex.IsMatch(res["ID"], @"^.{8}[^" + AvoidMatsubi + "]"));
                power += sageMatsubiResList.Aggregate(0.0, (p, res) => 0.1 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                return power;
            }
            else if (PowerSetMode == PowerSetModes.AnchorTrip)
            {
                var anchorResList = thread.ResListCache.Where(res => Regex.IsMatch(res["Message"], @"[>＞]", RegexOptions.Singleline));
                var power = anchorResList.Aggregate(0.0, (p, res) => 1.0 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                var othersResList = thread.ResListCache.Where(res => context.tripKeyDict.Values.All(v => res["Name"].IndexOf("◆" + v) == -1));
                power += othersResList.Aggregate(0.0, (p, res) => 0.1 / (thread.ResListCache.Count + 1 - int.Parse(res["No"])) + p);
                return power;
            }
            else
            {
                return 0;
            }
        }

        public async Task<string> GetMessage(BotContext context, BotThread thread)
        {
            return await Generator.getResString(MessageGenerateKind, context);
        }

        public string GetName(BotContext context, BotThread thread)
        {
            if (NameGenerateMode == NameGenerateModes.Manual)
            {
                return ManualName;
            }
            else if (NameGenerateMode == NameGenerateModes.Kote)
            {
                var charList = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポあいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポ亜哀挨愛曖悪握圧扱宛嵐安案暗以衣位囲医依委威為畏胃尉異移萎偉椅彙意違維慰遺緯域育一壱逸茨芋引印因咽姻員院淫陰飲隠韻右宇羽雨唄鬱畝浦運雲永泳英映栄営詠影鋭衛易疫益液駅悦越謁閲円延沿炎怨宴媛援園煙猿遠鉛塩演縁艶汚王凹央応往押旺欧殴桜翁奥横岡屋億憶臆虞乙俺卸音恩温穏下化火加可仮何花佳価果河苛科架夏家荷華菓貨渦過嫁暇禍靴寡歌箇稼課蚊牙瓦我画芽賀雅餓介回灰会快戒改怪拐悔海界皆械絵開階塊楷解潰壊懐諧貝外劾害崖涯街慨蓋該概骸垣柿各角拡革格核殻郭覚較隔閣確獲嚇穫学岳楽額顎掛潟括活喝渇割葛滑褐轄且株釜鎌刈干刊甘汗缶完肝官冠巻看陥乾勘患貫寒喚堪換敢棺款間閑勧寛幹感漢慣管関歓監緩憾還館環簡観韓艦鑑丸含岸岩玩眼頑顔願企伎危机気岐希忌汽奇祈季紀軌既記起飢鬼帰基寄規亀喜幾揮期棋貴棄毀旗器畿輝機騎技宜偽欺義疑儀戯擬犠議菊吉喫詰却客脚逆虐九久及弓丘旧休吸朽臼求究泣急級糾宮救球給嗅窮牛去巨居拒拠挙虚許距魚御漁凶共叫狂京享供協況峡挟狭恐恭胸脅強教郷境橋矯鏡競響驚仰暁業凝曲局極玉巾斤均近金菌勤琴筋僅禁緊錦謹襟吟銀区句苦駆具惧愚空偶遇隅串屈掘窟熊繰君訓勲薫軍郡群兄刑形系径茎係型契計恵啓掲渓経蛍敬景軽傾携継詣慶憬稽憩警鶏芸迎鯨隙劇撃激桁欠穴血決結傑潔月犬件見券肩建研県倹兼剣拳軒健険圏堅検嫌献絹遣権憲賢謙鍵繭顕験懸元幻玄言弦限原現舷減源厳己戸古呼固股虎孤弧故枯個庫湖雇誇鼓錮顧五互午呉後娯悟碁語誤護口工公勾孔功巧広甲交光向后好江考行坑孝抗攻更効幸拘肯侯厚恒洪皇紅荒郊香候校耕航貢降高康控梗黄喉慌港硬絞項溝鉱構綱酵稿興衡鋼講購乞号合拷剛傲豪克告谷刻国黒穀酷獄骨駒込頃今困昆恨根婚混痕紺魂墾懇左佐沙査砂唆差詐鎖座挫才再災妻采砕宰栽彩採済祭斎細菜最裁債催塞歳載際埼在材剤財罪崎作削昨柵索策酢搾錯咲冊札刷刹拶殺察撮擦雑皿三山参桟蚕惨産傘散算酸賛残斬暫士子支止氏仕史司四市矢旨死糸至伺志私使刺始姉枝祉肢姿思指施師恣紙脂視紫詞歯嗣試詩資飼誌雌摯賜諮示字寺次耳自似児事侍治持時滋慈辞磁餌璽鹿式識軸七叱失室疾執湿嫉漆質実芝写社車舎者射捨赦斜煮遮謝邪蛇尺借酌釈爵若弱寂手主守朱取狩首殊珠酒腫種趣寿受呪授需儒樹収囚州舟秀周宗拾秋臭修袖終羞習週就衆集愁酬醜蹴襲十汁充住柔重従渋銃獣縦叔祝宿淑粛縮塾熟出述術俊春瞬旬巡盾准殉純循順準潤遵処初所書庶暑署緒諸女如助序叙徐除小升少召匠床抄肖尚招承昇松沼昭宵将消症祥称笑唱商渉章紹訟勝掌晶焼焦硝粧詔証象傷奨照詳彰障憧衝賞償礁鐘上丈冗条状乗城浄剰常情場畳蒸縄壌嬢錠譲醸色拭食植殖飾触嘱織職辱尻心申伸臣芯身辛侵信津神唇娠振浸真針深紳進森診寝慎新審震薪親人刃仁尽迅甚陣尋腎須図水吹垂炊帥粋衰推酔遂睡穂随髄枢崇数据杉裾寸瀬是井世正生成西声制姓征性青斉政星牲省凄逝清盛婿晴勢聖誠精製誓静請整醒税夕斥石赤昔析席脊隻惜戚責跡積績籍切折拙窃接設雪摂節説舌絶千川仙占先宣専泉浅洗染扇栓旋船戦煎羨腺詮践箋銭潜線遷選薦繊鮮全前善然禅漸膳繕狙阻祖租素措粗組疎訴塑遡礎双壮早争走奏相荘草送倉捜挿桑巣掃曹曽爽窓創喪痩葬装僧想層総遭槽踪操燥霜騒藻造像増憎蔵贈臓即束足促則息捉速側測俗族属賊続卒率存村孫尊損遜他多汰打妥唾堕惰駄太対体耐待怠胎退帯泰堆袋逮替貸隊滞態戴大代台第題滝宅択沢卓拓託濯諾濁但達脱奪棚誰丹旦担単炭胆探淡短嘆端綻誕鍛団男段断弾暖談壇地池知値恥致遅痴稚置緻竹畜逐蓄築秩窒茶着嫡中仲虫沖宙忠抽注昼柱衷酎鋳駐著貯丁弔庁兆町長挑帳張彫眺釣頂鳥朝貼超腸跳徴嘲潮澄調聴懲直勅捗沈珍朕陳賃鎮追椎墜通痛塚漬坪爪鶴低呈廷弟定底抵邸亭貞帝訂庭逓停偵堤提程艇締諦泥的笛摘滴適敵溺迭哲鉄徹撤天典店点展添転填田伝殿電斗吐妬徒途都渡塗賭土奴努度怒刀冬灯当投豆東到逃倒凍唐島桃討透党悼盗陶塔搭棟湯痘登答等筒統稲踏糖頭謄藤闘騰同洞胴動堂童道働銅導瞳峠匿特得督徳篤毒独読栃凸突届屯豚頓貪鈍曇丼那奈内梨謎鍋南軟難二尼弐匂肉虹日入乳尿任妊忍認寧熱年念捻粘燃悩納能脳農濃把波派破覇馬婆罵拝杯背肺俳配排敗廃輩売倍梅培陪媒買賠白伯拍泊迫剥舶博薄麦漠縛爆箱箸畑肌八鉢発髪伐抜罰閥反半氾犯帆汎伴判坂阪板版班畔般販斑飯搬煩頒範繁藩晩番蛮盤比皮妃否批彼披肥非卑飛疲秘被悲扉費碑罷避尾眉美備微鼻膝肘匹必泌筆姫百氷表俵票評漂標苗秒病描猫品浜貧賓頻敏瓶不夫父付布扶府怖阜附訃負赴浮婦符富普腐敷膚賦譜侮武部舞封風伏服副幅復福腹複覆払沸仏物粉紛雰噴墳憤奮分文聞丙平兵併並柄陛閉塀幣弊蔽餅米壁璧癖別蔑片辺返変偏遍編弁便勉歩保哺捕補舗母募墓慕暮簿方包芳邦奉宝抱放法泡胞俸倣峰砲崩訪報蜂豊飽褒縫亡乏忙坊妨忘防房肪某冒剖紡望傍帽棒貿貌暴膨謀頬北木朴牧睦僕墨撲没勃堀本奔翻凡盆麻摩磨魔毎妹枚昧埋幕膜枕又末抹万満慢漫未味魅岬密蜜脈妙民眠矛務無夢霧娘名命明迷冥盟銘鳴滅免面綿麺茂模毛妄盲耗猛網目黙門紋問冶夜野弥厄役約訳薬躍闇由油喩愉諭輸癒唯友有勇幽悠郵湧猶裕遊雄誘憂融優与予余誉預幼用羊妖洋要容庸揚揺葉陽溶腰様瘍踊窯養擁謡曜抑沃浴欲翌翼拉裸羅来雷頼絡落酪辣乱卵覧濫藍欄吏利里理痢裏履璃離陸立律慄略柳流留竜粒隆硫侶旅虜慮了両良料涼猟陵量僚領寮療瞭糧力緑林厘倫輪隣臨瑠涙累塁類令礼冷励戻例鈴零霊隷齢麗暦歴列劣烈裂恋連廉練錬呂炉賂路露老労弄郎朗浪廊楼漏籠六録麓論和話賄脇惑枠湾腕";

                var len = Generator.getRandomNumber(3, 8);
                var name = "";
                for (var i = 0; i < len; i++)
                    name += charList[Generator.getRandomNumber(0, charList.Length - 1)];

                //var nameList = File.ReadAllLines(@"kote.txt");

                //return nameList[Generator.getRandomNumber(0, nameList.Length - 1)] + "#" + DateTime.Now.ToFileTime();
                //return name + "#" + DateTime.Now.ToFileTime();
                return name + "#" + context.tripKeyDict.Keys.ElementAt(Generator.getRandomNumber(0, context.tripKeyDict.Count - 1));
            }
            else
            {
                return "";
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
                NeedMatchCount,
                Enabled,
                Board,
                MessageGenerateKind,
                PowerSetMode,
                NameGenerateMode,
                AvoidMatsubi = AvoidMatsubi.ToString(),
                ManualName
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
                Enabled = settings.GetProperty("Enabled").GetBoolean();
                Board = settings.GetProperty("Board").GetString();
                MessageGenerateKind = (Generator.Kinds)Enum.Parse(typeof(Generator.Kinds), settings.GetProperty("MessageGenerateKind").GetString());
                PowerSetMode = (PowerSetModes)Enum.Parse(typeof(PowerSetModes), settings.GetProperty("PowerSetMode").GetString());
                NameGenerateMode = (NameGenerateModes)Enum.Parse(typeof(NameGenerateModes), settings.GetProperty("NameGenerateMode").GetString());
                AvoidMatsubi = settings.GetProperty("AvoidMatsubi").GetString()[0];
                ManualName = settings.GetProperty("ManualName").GetString();
            }
            catch { }
        }
    }
}
