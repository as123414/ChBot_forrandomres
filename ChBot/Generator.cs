using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Microsoft.JScript.Vsa;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Web.Security;
using System.Management.Automation;
using System.Web;

namespace ChBot
{
    public static class Generator
    {
        public static long LastThreadKey = 0;
        public static int LastResNo = 0;
        public static int LastPicked = -1;
        public static List<int> UsedResIndex = new List<int>();
        public static int ThreadPostCount = 0;
        public static Dictionary<string, List<long>> dict = new Dictionary<string, List<long>>();
        public static int zenCount = 0;
        public static long zenNowKeyAfter = 0;

        public static void Reset()
        {
            LastResNo = 0;
            LastThreadKey = 0;
            LastPicked = -1;
            UsedResIndex.Clear();
            ThreadPostCount = 0;
            zenCount = 0;
            zenNowKeyAfter = 0;
        }

        //乱数生成
        public static int getRandomNumber(int min, int max)
        {
            byte[] randoms = new byte[100];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(randoms);
            return min + Math.Abs(BitConverter.ToInt32(randoms, 0) % (max - min + 1));
        }

        public enum Kinds
        {
            Manual, AsciiKanji, UnixTime, AsciiArt, RandomRes, Calculator, TagetID, RandomImage, Capture, ThreadTitleRes, Zenryoku
        }

        //レス文字列生成
        public static async Task<string> getResString(Kinds kind, BotContext context)
        {
            switch (kind)
            {
                case Kinds.AsciiKanji:
                    return makeKanjiRandom();
                //return getLongString();
                case Kinds.UnixTime:
                    return UnixTime.Now().ToString();
                case Kinds.AsciiArt:
                default:
                    throw new NotImplementedException("指定された生成モードの動作は定義されていません。");
            }
        }

        public static string getLongString()
        {
            string randomString;
            int func = getRandomNumber(1, 2);
            switch (func)
            {
                case 1:
                    randomString = makeKanjiRandom();
                    break;
                case 2:
                    randomString = makeAckiiRandom();
                    break;
                default:
                    randomString = makeAckiiRandom();
                    break;
            }
            return randomString;
        }

        //漢字1文字生成
        public static string getSJISRandomStr(int length)
        {
            int[,] ranges = new int[,]{
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x82A0,0x82ED},
            {0x889F,0x88FC},
            {0x8940,0x897E},
            {0x8980,0x89FC},
            {0x8A40,0x8A7E},
            {0x8A80,0x8AFC},
            {0x8B40,0x8B7E},
            {0x8B80,0x8BFC},
            {0x8C40,0x8C7E},
            {0x8C80,0x8CFC},
            {0x8D40,0x8D7E},
            {0x8D80,0x8DFC},
            {0x8E40,0x8E7E},
            {0x8E80,0x8EFC},
            {0x8F40,0x8F7E},
            {0x8F80,0x8FFC},
            {0x9040,0x907E},
            {0x9080,0x90FC},
            {0x9140,0x917E},
            {0x9180,0x91FC},
            {0x9240,0x927E},
            {0x9280,0x92FC},
            {0x9240,0x927E},
            {0x9280,0x92FC},
            {0x9340,0x937E},
            {0x9380,0x93FC},
            {0x9440,0x947E},
            {0x9480,0x94FC}
            };
            var str = new List<byte>(length * 2);
            for (var i = 0; i < length; i++)
            {
                int row = getRandomNumber(0, ranges.GetLength(0) - 1);
                int code = getRandomNumber(ranges[row, 0], ranges[row, 1]);
                byte top = (byte)(code >> 8);
                byte bottom = (byte)(code & 0xFF);
                str.Add(top);
                str.Add(bottom);
            }
            return Regex.Replace(Encoding.GetEncoding(932).GetString(str.ToArray()), @"[ぁぃぅぇぉゃゅょゎっぱ-ぽぢ]", "");
        }

        //漢字文生成
        public static string makeKanjiRandom()
        {
            string randomString = "";
            int line = getRandomNumber(1, 10);

            for (int i = 0; i < line; i++)
            {
                randomString += getSJISRandomStr(getRandomNumber(4, 25)) + "\r\n";
            }
            return randomString.Trim();
        }

        //英数字文生成
        private static string makeAckiiRandom()
        {
            string randomString = "";
            int line = getRandomNumber(3, 50);
            for (int i = 0; i < line; i++)
            {
                string buf = Membership.GeneratePassword(getRandomNumber(20, 50), 0) + "\r\n";
                randomString += buf;
            }
            randomString = randomString.Replace("<", "a");
            randomString = randomString.Replace("\"", "a"); // ※「”」です。
            randomString = randomString.Replace("|", "a");
            randomString = randomString.Replace(">", "a");
            randomString = randomString.Replace("@", "a");
            return randomString;
        }

        public static string makeAlphaRandom(int length)
        {
            var aplha = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var result = "";
            for (int i = 0; i < length; i++)
            {
                result += aplha[getRandomNumber(0, aplha.Length - 1)];
            }
            return result;
        }
    }
}
