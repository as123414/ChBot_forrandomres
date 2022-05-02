using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChBot
{
    public class BotThreadContext
    {
        private BotThread current;
        private BotThreadList enabled;
        private BotThreadList ignored;
        private BotThreadList history;

        public BotThreadList SearchResult { get; set; }
        public Directions Direction { get; set; }

        public enum Directions
        {
            Down, Up
        }

        //カレント移動モード
        public enum CurrentMoveModes
        {
            NoAutoMove, Latest, MoveNext, RankTop, Random
        }

        //コンストラクタ
        public BotThreadContext()
        {
            current = null;
            enabled = new BotThreadList();
            ignored = new BotThreadList();
            history = new BotThreadList();
            SearchResult = new BotThreadList();
            Direction = Directions.Down;
        }

        public BotThread GetCurrent()
        {
            return current;
        }

        public BotThreadList GetEnabled()
        {
            return enabled.Clone();
        }

        public BotThreadList GetIgnored()
        {
            return ignored.Clone();
        }

        public BotThreadList GetHistory()
        {
            return history.Clone();
        }

        public void SetCurrent(BotThread thread, bool escape = true)
        {
            if (thread == null)
                current = null;
            else
            {
                if (!ignored.Contains(thread))
                    current = thread;
                if (escape)
                    EscapeCurrent();
            }
        }

        public void AddEnabled(BotThread thread)
        {
            BotThreadList list = new BotThreadList();
            list.Add(thread);
            AddEnabled(list);
        }

        public void AddEnabled(BotThreadList list)
        {
            foreach (BotThread thread in list)
            {
                if (thread != null && !enabled.Contains(thread))
                {
                    enabled.Add(thread);
                }
            }
            enabled.Sort();
        }

        public void AddIgnored(BotThread thread, bool disableEscape = false)
        {
            BotThreadList list = new BotThreadList();
            list.Add(thread);
            AddIgnored(list, disableEscape);
        }

        public void AddIgnored(BotThreadList list, bool disableEscape = false)
        {
            foreach (BotThread thread in list)
            {
                if (thread != null && !ignored.Contains(thread))
                    ignored.Add(thread);
            }
            ignored.Sort();
            if (!disableEscape)
                EscapeCurrent();
        }

        public void AddHistory(BotThread thread)
        {
            var list = new BotThreadList();
            list.Add(thread);
            AddHistory(list);
        }

        public void AddHistory(BotThreadList list)
        {
            foreach (var thread in list)
            {
                if (thread != null && !history.Contains(thread))
                {
                    history.Add(thread);
                }
            }
            history.Sort(BotThreadList.SortMode.Wrote);

            while (history.Count > 100)
                history.Remove(history.Last());
        }

        public void RemoveEnabled(BotThread thread)
        {
            var list = new BotThreadList();
            list.Add(thread);
            RemoveEnabled(list);
        }

        public void RemoveEnabled(BotThreadList list)
        {
            foreach (BotThread thread in list)
            {
                enabled.Remove(thread);
            }
            enabled.Sort();
            EscapeCurrent();
        }

        public void RemoveIgnored(BotThread thread)
        {
            BotThreadList list = new BotThreadList();
            list.Add(thread);
            RemoveIgnored(list);
        }

        public void RemoveIgnored(BotThreadList list)
        {
            foreach (BotThread thread in list)
            {
                ignored.Remove(thread);
            }
            ignored.Sort();
        }

        public void RemoveHistory(BotThread thread)
        {
            BotThreadList list = new BotThreadList();
            list.Add(thread);
            RemoveHistory(list);
        }

        public void RemoveHistory(BotThreadList list)
        {
            foreach (BotThread thread in list)
            {
                history.Remove(thread);
            }
            history.Sort();
            DeleteUnneccesaryIgnored();
        }

        public void DeleteUnneccesaryIgnored()
        {
            ignored = ignored.Where(thread => SearchResult.Contains(thread) || history.Contains(thread)).ToBotThreadList();
        }

        public void ClearEnabled()
        {
            enabled.Clear();
            EscapeCurrent();
        }

        public void ClearIgnored()
        {
            ignored.Clear();
        }

        public void ClearHistory()
        {
            history.Clear();
            DeleteUnneccesaryIgnored();
        }

        public bool IsCurrent(BotThread thread)
        {
            if (current != null)
            {
                return current.Equals(thread);
            }
            else
            {
                return false;
            }
        }

        public bool IsEnabled(BotThread thread)
        {
            return enabled.Contains(thread);
        }

        public bool IsIgnored(BotThread thread)
        {
            return ignored.Contains(thread);
        }

        public bool IsHistory(BotThread thread)
        {
            return history.Contains(thread);
        }

        public bool IsEnabledEmpty()
        {
            return enabled.Empty;
        }

        public bool IsIgnoredEmpty()
        {
            return ignored.Empty;
        }

        public bool IsHistoryEmpty()
        {
            return history.Empty;
        }

        public bool IsCurrentEquals(BotThread thread)
        {
            return thread.Equals(current);
        }

        public bool IsEnabledContains(BotThread thread)
        {
            return enabled.Contains(thread);
        }

        public bool IsIgnoredContains(BotThread thread)
        {
            return ignored.Contains(thread);
        }

        public bool IsHistoryContains(BotThread thread)
        {
            return history.Contains(thread);
        }

        public int EnabledIndexOf(BotThread thread)
        {
            return enabled.IndexOf(thread);
        }

        public int IgnoredIndexOf(BotThread thread)
        {
            return ignored.IndexOf(thread);
        }

        public int HistoryIndexOf(BotThread thread)
        {
            return history.IndexOf(thread);
        }

        public BotThread EnabledAt(int index)
        {
            if (!enabled.Empty)
            {
                return enabled[index];
            }
            else
            {
                return null;
            }
        }

        public BotThread IgnoredAt(int index)
        {
            if (!ignored.Empty)
            {
                return ignored[index];
            }
            else
            {
                return null;
            }
        }

        public BotThread HistoryAt(int index)
        {
            if (!history.Empty)
            {
                return history[index];
            }
            else
            {
                return null;
            }
        }

        public int EnabledCount()
        {
            return enabled.Count();
        }

        public int IgnoredCount()
        {
            return ignored.Count();
        }

        public int HistoryCount()
        {
            return history.Count();
        }

        //カレントスレッドを退避
        public void EscapeCurrent()
        {
            var enabledList = enabled.Where(thread => !ignored.Contains(thread)).ToList();
            var priorityList = enabledList.Where(thread => thread.Priority).ToList();
            var availableLlist = priorityList.Count > 0 ? priorityList : enabledList;

            if (availableLlist.Count > 0)
            {
                if (current == null)
                {
                    current = null;
                }
                else
                {
                    if (!availableLlist.Contains(current))
                    {
                        if (SearchResult.Contains(current))
                        {
                            int index = SearchResult.IndexOf(current);
                            if (Direction == Directions.Down)
                            {
                                for (int i = 0; i < SearchResult.Count; i++)
                                {
                                    if (availableLlist.Contains(SearchResult[(index + i) % SearchResult.Count]))
                                    {
                                        current = SearchResult[(index + i) % SearchResult.Count];
                                        break;
                                    }
                                }
                            }
                            else if (Direction == Directions.Up)
                            {
                                for (int i = 0; i < SearchResult.Count; i++)
                                {
                                    if (availableLlist.Contains(SearchResult[(index + (SearchResult.Count - i)) % SearchResult.Count]))
                                    {
                                        current = SearchResult[(index + (SearchResult.Count - i)) % SearchResult.Count];
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            current = availableLlist.First();
                        }
                    }
                }
            }
            else
            {
                current = null;
            }
        }

        public string OjbectToStringData()
        {
            throw new NotImplementedException();
        }

        public void StringDataToObject(string stringData)
        {

        }

    }
}
