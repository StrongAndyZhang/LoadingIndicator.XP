using LoadingIndicator.XP;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LoadingIndicator.Demo
{
    public partial class Form1 : Form
    {
        private LongOperation _longOperation;

        public Form1()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _longOperation=new LongOperation(this);
            _longOperation.Start(true);
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(3000);
            }).ContinueWith(_ => _longOperation.StopIfDisplayed());
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _longOperation=new LongOperation(panel1);
            _longOperation.Start(true);
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(3000);
            }).ContinueWith(_ => _longOperation.StopIfDisplayed());
        }
    }
}
