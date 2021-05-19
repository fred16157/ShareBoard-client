using Gma.System.MouseKeyHook;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareBoard
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            Combination copyCombo = Combination.FromString("Control+Alt+C");

            Dictionary<Combination, Action> assignment = new Dictionary<Combination, Action> { 
                { copyCombo, OnCopy } 
            };

            Hook.GlobalEvents().OnCombination(assignment);
            
        }

        void OnCopy()
        {
            if (!Clipboard.ContainsText()) return;
            string clipboardText = Clipboard.GetText();

            //딜레이가 없으면 E_UNEXPECTED 에러를 뱉는다
            //왜 딜레이가 있어야 실행되는지는 모르겠음
            Thread.Sleep(500);
            new ToastContentBuilder().AddText("클립보드에 복사됨").AddText(clipboardText).Show();
        }
    }
}
