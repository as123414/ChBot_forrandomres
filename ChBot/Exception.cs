using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChBot
{
    public class DatGetFailureException : Exception
    {
        public string Result = "";

        public DatGetFailureException(string result)
        {
            this.Result = result;
        }

        override public string Message
        {
            get
            {
                return "DAT取得に失敗しました。 ";
            }
        }
    }

    public class PostFailureException : Exception
    {
        public string Result = "";

        public PostFailureException(string result)
        {
            this.Result = result;
        }

        override public string Message
        {
            get
            {
                return "書き込みに失敗しました。 ";
            }
        }
    }

    public class SigFailureException : PostFailureException
    {
        public string NewMonaKey = "";

        public SigFailureException(string result, string newMonaKey) : base(result)
        {
            this.NewMonaKey = newMonaKey;
        }

        override public string Message
        {
            get
            {
                return "署名が適合しませんでした。 ";
            }
        }
    }

    public class UrlFormNotCorrectException : Exception
    {
        override public string Message
        {
            get
            {
                return "URLの形式が正しくありません。 ";
            }
        }
    }

    public class NotSetSidException : Exception
    {
        override public string Message
        {
            get
            {
                return "API SIDがありません。";
            }
        }
    }

    public class ApiLoginFailedException : Exception
    {
        override public string Message
        {
            get
            {
                return "APIログイン失敗";
            }
        }
    }

    public class RoninLoginFailedException : Exception
    {
        override public string Message
        {
            get
            {
                return "浪人ログイン失敗";
            }
        }
    }
}
