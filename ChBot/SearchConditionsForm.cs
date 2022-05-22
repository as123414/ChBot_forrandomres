using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChBot
{
    public partial class SearchConditionsForm : Form
    {
        public bool disableEvents = false;
        public BotContext context;
        public SearchCondition selectedSearchCondition = new SearchCondition();

        public SearchConditionsForm(BotContext context)
        {
            disableEvents = true;
            this.context = context;
            InitializeComponent();
        }

        private void SearchConditionsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void SearchConditionsForm_Shown(object sender, EventArgs e)
        {
            disableEvents = false;
            UpdateUI();
        }

        //検索モード変更
        private void ApplySearchModeRadioButtton()
        {
            var prevDisableEvents = disableEvents;
            disableEvents = true;
            if (selectedSearchCondition.SearchMode == SearchCondition.SearchModes.Url)
            {
                UrlFormPanel.Enabled = true;
                WordFormPanel.Enabled = false;
                UrlModeRadioButton.Checked = true;
                WordModeRadioButton.Checked = false;
            }
            else if (selectedSearchCondition.SearchMode == SearchCondition.SearchModes.Word)
            {
                UrlFormPanel.Enabled = false;
                WordFormPanel.Enabled = true;
                UrlModeRadioButton.Checked = false;
                WordModeRadioButton.Checked = true;
            }
            else
            {
                UrlFormPanel.Enabled = false;
                WordFormPanel.Enabled = true;
            }
            disableEvents = prevDisableEvents;
        }

        public void ListUpSearchConditions()
        {
            var prevDisableEvents = disableEvents;
            disableEvents = true;

            var refresh = false;
            var shownSearchConditions = new List<SearchCondition>();
            foreach (ListViewItem item in listView1.Items)
                shownSearchConditions.Add((SearchCondition)item.Tag);
            if (shownSearchConditions.Count != context.SearchConditions.Count)
            {
                refresh = true;
            }
            else
            {
                for (var i = 0; i < shownSearchConditions.Count; i++)
                {
                    if (shownSearchConditions[i] != context.SearchConditions[i])
                    {
                        refresh = true;
                        break;
                    }
                }
            }

            if (refresh)
            {
                var selectedIndex = 0;
                if (listView1.SelectedItems.Count > 0)
                    selectedIndex = listView1.SelectedItems[0].Index;

                listView1.Items.Clear();
                foreach (var condition in context.SearchConditions)
                {
                    var item = new ListViewItem(new[] {
                        condition.SearchMode == SearchCondition.SearchModes.Url ? "URL" :
                        condition.SearchMode == SearchCondition.SearchModes.Word ? "Word" : "",
                        condition.SearchMode == SearchCondition.SearchModes.Url ? condition.Url : condition.Word,
                        condition.MinRes + "-" + condition.MaxRes,
                        condition.MinTime + "-" + condition.MaxTime,
                        condition.KeyMod + "n+" + condition.KeyRem,
                        condition.BodySearchText,
                        condition.NameSearchText
                    });

                    item.Checked = condition.Enabled;
                    item.BackColor = condition.Enabled ? Color.White : Color.Silver;
                    item.Tag = condition;
                    listView1.Items.AddRange(new[] { item });
                }

                var newSelectedIndex = Math.Min(listView1.Items.Count - 1, selectedIndex);
                listView1.Items[newSelectedIndex].Selected = true;
                selectedSearchCondition = (SearchCondition)listView1.Items[newSelectedIndex].Tag;
            }
            else
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    var condition = (SearchCondition)item.Tag;

                    item.SubItems[0].Text = condition.SearchMode == SearchCondition.SearchModes.Url ? "URL" :
                    condition.SearchMode == SearchCondition.SearchModes.Word ? "Word" : "";
                    item.SubItems[1].Text = condition.SearchMode == SearchCondition.SearchModes.Url ? condition.Url : condition.Word;
                    item.SubItems[2].Text = condition.MinRes + "-" + condition.MaxRes;
                    item.SubItems[3].Text = condition.MinTime + "-" + condition.MaxTime;
                    item.SubItems[4].Text = condition.KeyMod + "n+" + condition.KeyRem;
                    item.SubItems[5].Text = condition.BodySearchText;
                    item.SubItems[6].Text = condition.NameSearchText;

                    item.Checked = condition.Enabled;
                    item.BackColor = condition.Enabled ? Color.White : Color.Silver;
                }
            }
            disableEvents = prevDisableEvents;
        }

        public void UpdateUI()
        {
            ListUpSearchConditions();
            displaySelectedSearchCondition();
        }

        //状態を表示
        public void displaySelectedSearchCondition()
        {
            var prevDisableEvents = disableEvents;
            disableEvents = true;

            UrlTextBox.Text = selectedSearchCondition.Url;
            WordTextBox.Text = selectedSearchCondition.Word;
            MaxResNumericUpDown.Value = selectedSearchCondition.MaxRes;
            MinResNumericUpDown.Value = selectedSearchCondition.MinRes;
            MaxTimeNumericUpDown.Value = selectedSearchCondition.MaxTime;
            MinTimeNumericUpDown.Value = selectedSearchCondition.MinTime;
            KeyModNumericUpDown.Value = selectedSearchCondition.KeyMod;
            KeyRemNumericUpDown.Value = selectedSearchCondition.KeyRem;
            BodySearchTextTextBox.Text = selectedSearchCondition.BodySearchText;
            NameSearchTextBox.Text = selectedSearchCondition.NameSearchText;
            IDMatchTextBox.Text = selectedSearchCondition.IDMatchText;
            MaxTargetNumericUpDown.Value = selectedSearchCondition.MaxTarget;
            OptionSearchTextTextBox.Text = selectedSearchCondition.OptionSearchText;
            MaxNoNumericUpDown.Value = selectedSearchCondition.MaxNo;
            MinNoNumericUpDown.Value = selectedSearchCondition.MinNo;
            TripTextBox.Text = selectedSearchCondition.Trip;
            EverMatchCheckBox.Checked = selectedSearchCondition.EverMatch;
            NeedMatchCountNumericUpDown.Value = selectedSearchCondition.NeedMatchCount;
            textBox1.Text = selectedSearchCondition.Board;

            ApplySearchModeRadioButtton();

            disableEvents = prevDisableEvents;
        }

        //貼り付け
        private void PasteButton_Click(object sender, EventArgs e)
        {
            selectedSearchCondition.Url = Clipboard.GetText();
            UpdateUI();
        }

        private void UrlModeRadioButton_CheckedChanged_1(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.SearchMode = SearchCondition.SearchModes.Url;
            ApplySearchModeRadioButtton();
            ListUpSearchConditions();
        }

        private void WordModeRadioButton_CheckedChanged_1(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.SearchMode = SearchCondition.SearchModes.Word;
            ApplySearchModeRadioButtton();
            ListUpSearchConditions();
        }

        private void BodySearchTextTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.BodySearchText = BodySearchTextTextBox.Text;
            ListUpSearchConditions();
        }

        private void KeyRemNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.KeyRem = (int)KeyRemNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void KeyModNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.KeyMod = (int)KeyModNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void UrlTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.Url = UrlTextBox.Text;
            ListUpSearchConditions();
        }

        private void WordTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.Word = WordTextBox.Text;
            ListUpSearchConditions();
        }

        private void MinResNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MinRes = (int)MinResNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void MaxResNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MaxRes = (int)MaxResNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void MinTimeNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MinTime = (long)MinTimeNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void MaxTimeNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MaxTime = (long)MaxTimeNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void NameSearchTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.NameSearchText = NameSearchTextBox.Text;
            ListUpSearchConditions();
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            if (listView1.SelectedItems.Count > 0)
            {
                selectedSearchCondition = (SearchCondition)listView1.SelectedItems[0].Tag;
                displaySelectedSearchCondition();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var condtion = new SearchCondition();
            context.SearchConditions.Add(condtion);
            UpdateUI();
            listView1.Items[listView1.Items.Count - 1].Selected = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (context.SearchConditions.Count == 1)
            {
                MessageBox.Show("検索条件を0個にすることはできません。");
            }
            else
            {
                context.SearchConditions = context.SearchConditions.Where(context => context != selectedSearchCondition).ToList();
                UpdateUI();
            }
        }

        private void IDMatchTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.IDMatchText = IDMatchTextBox.Text;
            ListUpSearchConditions();
        }

        private void SearchConditionsForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Escape)
                Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void MaxTargetNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MaxTarget = (int)MaxTargetNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void OptionSearchTextTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.OptionSearchText = OptionSearchTextTextBox.Text;
            ListUpSearchConditions();
        }

        private void MinNoNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MinNo = (int)MinNoNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void MaxNoNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.MaxNo = (int)MaxNoNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void EverMatchCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.EverMatch = EverMatchCheckBox.Checked;
            ListUpSearchConditions();
        }

        private void TripTextBox_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.Trip = TripTextBox.Text;
            ListUpSearchConditions();
        }

        private void NeedMatchCountNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.NeedMatchCount = (int)NeedMatchCountNumericUpDown.Value;
            ListUpSearchConditions();
        }

        private void listView1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (disableEvents)
                return;

            ((SearchCondition)listView1.Items[e.Index].Tag).Enabled = e.NewValue == CheckState.Checked;
            ListUpSearchConditions();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (disableEvents)
                return;

            selectedSearchCondition.Board = textBox1.Text;
            ListUpSearchConditions();
        }
    }
}