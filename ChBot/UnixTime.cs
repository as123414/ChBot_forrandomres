using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChBot
{
    public static class UnixTime
    {
        private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        //現在時刻からUnixTimeを計算
        public static long Now()
        {
            return FromDateTime(DateTime.UtcNow);
        }

        //UnixTimeからDateTimeに変換
        public static DateTime FromUnixTime(long unixTime)
        {
            return UNIX_EPOCH.AddSeconds(unixTime).ToLocalTime();
        }

        //指定時間をUnixTimeに変換する
        public static long FromDateTime(DateTime dateTime)
        {
            double nowTicks = (dateTime.ToUniversalTime() - UNIX_EPOCH).TotalSeconds;
            return (long)nowTicks;
        }

    }
}
