using CodeArtEng.SQLite;
using System;
using System.Windows.Forms;

namespace SQLiteTestApp
{
    public partial class MainForm : Form
    {
        private readonly SQLiteTestDB DB = new SQLiteTestDB("TestDB.db");
        public MainForm()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            DB.ReadTableA();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }
}
