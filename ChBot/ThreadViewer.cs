using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChBot
{
    public partial class ThreadViewer : Form
    {
        BotContext context;
        bool disableEvent = false;
        int latestResNo = 0;
        BotThread viewingThread = null;
        List<Dictionary<string, string>> resList = new List<Dictionary<string, string>>();
        ToolStripDropDown toolStripDropDown;

        public ThreadViewer(BotContext context)
        {
            this.context = context;
            InitializeComponent();
            webBrowser1.WebBrowserShortcutsEnabled = false;

            toolStripDropDown = new ToolStripDropDown()
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                DropShadowEnabled = false,
                AutoSize = true,
                Size = Size.Empty
            };
        }

        public async Task Open(BotThread thread)
        {
            viewingThread = thread;

            Text = viewingThread.Title;
            Show();

            toolStripLabel1.Text = "読み込み中…";
            disableEvent = true;
            try
            {
                //resList = Network.DatJsonToDetailResList(await Network.GetDatJson(viewingThread));
                resList = Network.DatToDetailResList(await Network.GetDat(viewingThread, context.ApiSid));
                await showResList(resList);
            }
            finally
            {
                toolStripLabel1.Text = "";
                disableEvent = false;
            }
        }

        private async Task showResList(List<Dictionary<string, string>> resList)
        {
            var prevScrollTop = 0;
            if (webBrowser1.Document != null)
            {
                prevScrollTop = webBrowser1.Document.Body.ScrollTop;
            }
            else
            {
                webBrowser1.Navigate("about:blank");

                while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                    await Task.Delay(100);

                webBrowser1.Document.Body.Style = "font-size:15px;background:#efefef;font-family:'MeiryoKe_PGothic';padding-bottom:20px;";
            }

            var container = webBrowser1.Document.CreateElement("div");

            foreach (var res in resList)
            {
                var header = webBrowser1.Document.CreateElement("div");
                header.Style = "margin-bottom:5px;";
                container.AppendChild(header);

                var noElem = webBrowser1.Document.CreateElement("span");
                noElem.InnerHtml = res["No"];
                noElem.Style = "color:blue;text-decoration:underline;font-weight:" + (int.Parse(res["No"]) > latestResNo ? "bold" : "") + ";";
                header.AppendChild(noElem);

                var nameElem = webBrowser1.Document.CreateElement("span");
                nameElem.InnerHtml = "<b>" + res["Name"] + "</b>";
                nameElem.Style = "margin-left:5px;color:green;";
                header.AppendChild(nameElem);

                var mailElem = webBrowser1.Document.CreateElement("span");
                mailElem.InnerHtml = "[" + res["Mail"] + "]";
                mailElem.Style = "margin-left:5px;";
                header.AppendChild(mailElem);

                var splitedOption = res["Option"].Split(new[] { "ID:" + res["ID"] }, StringSplitOptions.None);
                var option1Elem = webBrowser1.Document.CreateElement("span");
                option1Elem.InnerHtml = splitedOption[0];
                option1Elem.Style = "margin-left:5px;";
                header.AppendChild(option1Elem);

                if (splitedOption.Length == 2)
                {
                    var idExtracted = resList.Where(r => r["ID"] == res["ID"]).ToList();

                    var idHeadElem = webBrowser1.Document.CreateElement("span");
                    idHeadElem.InnerHtml = "ID:";
                    idHeadElem.Style = "text-decoration:underline;" + (idExtracted.Count >= 2 ? "color:blue;" : "");
                    idHeadElem.MouseOver += (sender, e) =>
                    {
                        if (toolStripDropDown.Visible)
                        {
                            toolStripDropDown.Tag = false;
                        }
                        else
                        {
                            var x = idHeadElem.OffsetRectangle.Left - webBrowser1.Document.Body.ScrollLeft;
                            var y = idHeadElem.OffsetRectangle.Top - webBrowser1.Document.Body.ScrollTop;
                            ShowPopup(x, y, res["ID"]);
                        }
                    };
                    idHeadElem.MouseLeave += async (sender, e) => await ClosePopup();
                    header.AppendChild(idHeadElem);

                    var idElem = webBrowser1.Document.CreateElement("span");
                    idElem.InnerHtml = res["ID"];
                    header.AppendChild(idElem);

                    if (idExtracted.Count >= 2)
                    {
                        var idFootElem = webBrowser1.Document.CreateElement("span");
                        idFootElem.InnerHtml = "[" + (idExtracted.IndexOf(res) + 1) + "/" + idExtracted.Count + "]";
                        idFootElem.Style = "margin-left:5px;";
                        header.AppendChild(idFootElem);
                    }

                    var option2Elem = webBrowser1.Document.CreateElement("span");
                    option2Elem.InnerHtml = splitedOption[1];
                    option2Elem.Style = "margin-left:5px;";
                    header.AppendChild(option2Elem);
                }

                var contentContainer = webBrowser1.Document.CreateElement("div");
                contentContainer.Style = "margin-left:20px;margin-bottom:25px;";
                container.AppendChild(contentContainer);

                var message = Regex.Replace(res["Message"], @"https?://[\w/:%#\$&\?\(\)~\.=\+\-]+", m => "<span style=\"color:blue;text-decoration:underline;\">" + m.Value + "</span>");
                message = Regex.Replace(message, @">>\d+([\-,]\d+)*", m => "<span style=\"color:blue;text-decoration:underline;\">" + m.Value + "</span>");
                message = message.Replace("\r\n", "<br />");

                var messageElem = webBrowser1.Document.CreateElement("div");
                messageElem.InnerHtml = message;
                contentContainer.AppendChild(messageElem);

                var imageContainer = webBrowser1.Document.CreateElement("div");
                imageContainer.Style = "margin-top:15px;";
                contentContainer.AppendChild(imageContainer);

                var imageMatches = Regex.Matches(res["Message"], @"https?://[\w/:%#\$&\?\(\)~\.=\+\-]+\.(jpg|png|gif)");
                foreach (Match match in imageMatches)
                {
                    var imageElem = webBrowser1.Document.CreateElement("img");
                    imageElem.SetAttribute("src", match.Value);
                    imageElem.Style = "vertical-align:top;border:1px solid blue;margin-right:10px;height:80px;width:auto;";
                    imageElem.Click += (sender, e) =>
                    {
                        var recodes = imageElem.Style.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        var dict = recodes.ToDictionary(r => r.Split(':')[0].Trim().ToLower(), r => r.Split(':')[1].Trim());
                        dict["height"] = dict["height"] == "80px" ? "300px" : "80px";
                        imageElem.Style = string.Join(";", dict.Select(p => p.Key + ":" + p.Value));
                    };
                    imageContainer.AppendChild(imageElem);
                }
            }

            webBrowser1.Document.Body.InnerHtml = "";
            webBrowser1.Document.Body.AppendChild(container);
            latestResNo = resList.Count;
            await Task.Delay(1);
            webBrowser1.Document.Body.ScrollTop = prevScrollTop;
        }

        private async void webBrowser1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.F4)
            {
                e.IsInputKey = true;
            }
            else
            {
                if (disableEvent)
                    return;

                if (webBrowser1.ReadyState == WebBrowserReadyState.Complete && e.KeyCode == Keys.F5)
                    await Open(viewingThread);
            }
        }

        private void ShowPopup(int x, int y, string id)
        {
            toolStripDropDown.Tag = false;
            var extracted = resList.Where(r => r["ID"] == id).ToList();

            var panel1 = new FlowLayoutPanel()
            {
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(2),
                AutoSize = true,
                Size = Size.Empty,
                FlowDirection = FlowDirection.TopDown
            };

            for (var i = 0; i < extracted.Count; i++)
            {
                var res = extracted[i];

                panel1.Controls.Add(new Label()
                {
                    Text = res["No"] + " " + res["Name"] + " [" + res["Mail"] + "] " + res["Option"],
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 3)
                });

                panel1.Controls.Add(new Label()
                {
                    Text = res["Message"],
                    AutoSize = true,
                    Margin = new Padding(10, 0, 0, i < extracted.Count - 1 ? 12 : 0)
                });
            }

            var toolStripControlHost = new ToolStripControlHost(panel1)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = true,
                Font = new Font("MeiryoKe_PGothic", DefaultFont.Size),
                BackColor = SystemColors.Info,
                Size = Size.Empty
            };

            panel1.MouseHover += (sender, e) => toolStripDropDown.Tag = true;
            panel1.MouseLeave += async (sender, e) =>
            {
                toolStripDropDown.Tag = false;
                await ClosePopup();
            };

            toolStripDropDown.Items.Clear();
            toolStripDropDown.Items.Add(toolStripControlHost);
            toolStripDropDown.Show(webBrowser1, new Point(x - 2, y + 1), ToolStripDropDownDirection.AboveRight);
        }

        private async Task ClosePopup()
        {
            await Task.Delay(1);
            if ((bool)toolStripDropDown.Tag == false)
                toolStripDropDown.Close();
        }
    }
}
