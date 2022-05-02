using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ChBot
{
    public class BotThreadList : List<BotThread>
    {
        new public void Remove(BotThread thread)
        {
            if (thread != null)
            {
                foreach (BotThread item in this)
                {
                    if (item.Equals(thread))
                    {
                        base.Remove(item);
                        break;
                    }
                }
            }
        }

        public BotThreadList Clone()
        {
            BotThreadList list = new BotThreadList();
            foreach (BotThread thread in this)
            {
                list.Add(thread.Clone());
            }
            return list;
        }

        new public int IndexOf(BotThread thread)
        {
            for (var i = 0; i < Count; i++)
            {
                if (this[i].Equals(thread))
                    return i;
            }
            return -1;
        }

        new public bool Contains(BotThread thread)
        {
            foreach (BotThread item in this)
            {
                if (item != null)
                {
                    if (item.Equals(thread))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public enum SortMode
        {
            Time, Wrote
        }

        //番号でソート
        internal void Sort(SortMode mode = SortMode.Time)
        {
            if (mode == SortMode.Time)
            {
                base.Sort((BotThread a, BotThread b) => { return (int)(b.Key - a.Key); });
            }
            else if (mode == SortMode.Wrote)
            {
                base.Sort((BotThread a, BotThread b) => { return (int)(b.Wrote - a.Wrote); });
            }
        }

        //空であるか
        public bool Empty
        {
            get
            {
                return this.Count == 0;
            }
        }

        //最初の要素
        public BotThread First
        {
            get
            {
                if (!this.Empty)
                {
                    return this[0];
                }
                else
                {
                    return null;
                }
            }
        }

        //最後の要素
        public BotThread Last
        {
            get
            {
                if (!this.Empty)
                {
                    return this[this.Count - 1];
                }
                else
                {
                    return null;
                }
            }
        }

        public dynamic getObject()
        {
            return this.Select(thread => thread.getObject());
        }

        public void ResumeObject(JsonElement data)
        {
            this.Clear();
            foreach (var json in data.EnumerateArray())
            {
                var thread = new BotThread(0, "", "", "");
                thread.ResumeObject(json);
                this.Add(thread);
            }
        }
    }

    static class IEnumerableExtensions
    {
        public static BotThreadList ToBotThreadList(this IEnumerable<BotThread> source)
        {
            var list = new BotThreadList();
            foreach (var item in source)
                list.Add(item);
            return list;
        }
    }
}
