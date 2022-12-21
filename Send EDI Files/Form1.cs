using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Dapper;

namespace Send_EDI_Files
{
    public partial class Form1 : Form
    {
        private Label label1;
        private TextBox txtBoxResults;
        private Button btnSend;
        private SplitContainer splitContainer1;
        private TextBox txtBoxOrderNumbers;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            txtBoxOrderNumbers = new TextBox();
            label1 = new Label();
            txtBoxResults = new TextBox();
            btnSend = new Button();
            splitContainer1 = new SplitContainer();
            SendMode_cb = new ComboBox();
            ((ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // txtBoxOrderNumbers
            // 
            txtBoxOrderNumbers.BorderStyle = BorderStyle.None;
            txtBoxOrderNumbers.Dock = DockStyle.Fill;
            txtBoxOrderNumbers.Font = new Font("Microsoft Sans Serif", 10.125F, FontStyle.Regular, GraphicsUnit.Point,
                (byte)0);
            txtBoxOrderNumbers.Location = new Point(0, 32);
            txtBoxOrderNumbers.Multiline = true;
            txtBoxOrderNumbers.Name = "txtBoxOrderNumbers";
            txtBoxOrderNumbers.Size = new Size(305, 537);
            txtBoxOrderNumbers.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = DockStyle.Top;
            label1.Font = new Font("Microsoft Sans Serif", 13.875F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            label1.ForeColor = SystemColors.ButtonFace;
            label1.Location = new Point(0, 0);
            label1.Name = "label1";
            label1.Size = new Size(394, 32);
            label1.TabIndex = 1;
            label1.Text = "Enter or Paste Order Numbers";
            // 
            // txtBoxResults
            // 
            txtBoxResults.BackColor = SystemColors.MenuHighlight;
            txtBoxResults.Dock = DockStyle.Fill;
            txtBoxResults.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            txtBoxResults.ForeColor = SystemColors.Info;
            txtBoxResults.Location = new Point(20, 0);
            txtBoxResults.Multiline = true;
            txtBoxResults.Name = "txtBoxResults";
            txtBoxResults.Size = new Size(589, 640);
            txtBoxResults.TabIndex = 2;
            // 
            // btnSend
            // 
            btnSend.BackColor = SystemColors.ButtonFace;
            btnSend.Dock = DockStyle.Bottom;
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.Font = new Font("Microsoft Sans Serif", 13.875F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            btnSend.Location = new Point(0, 569);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(305, 47);
            btnSend.TabIndex = 3;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = false;
            btnSend.Click += new EventHandler(btnSend_Click);
            // 
            // splitContainer1
            // 
            splitContainer1.Anchor = (AnchorStyles)(AnchorStyles.Top | AnchorStyles.Bottom
                                                                     | AnchorStyles.Left
                                                                     | AnchorStyles.Right);
            splitContainer1.Location = new Point(29, 38);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(txtBoxOrderNumbers);
            splitContainer1.Panel1.Controls.Add(label1);
            splitContainer1.Panel1.Controls.Add(btnSend);
            splitContainer1.Panel1.Controls.Add(SendMode_cb);
            splitContainer1.Panel1MinSize = 200;
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(txtBoxResults);
            splitContainer1.Panel2.Padding = new Padding(20, 0, 0, 0);
            splitContainer1.Panel2MinSize = 400;
            splitContainer1.Size = new Size(918, 640);
            splitContainer1.SplitterDistance = 305;
            splitContainer1.TabIndex = 4;
            // 
            // SendMode_cb
            // 
            SendMode_cb.Dock = DockStyle.Bottom;
            SendMode_cb.FormattingEnabled = true;
            SendMode_cb.Items.AddRange(new object[]
            {
                "Send Now",
                "Send With Next Batch"
            });
            SendMode_cb.Location = new Point(0, 616);
            SendMode_cb.Name = "SendMode_cb";
            SendMode_cb.Size = new Size(305, 24);
            SendMode_cb.TabIndex = 4;
            // 
            // Form1
            // 
            BackColor = SystemColors.MenuHighlight;
            ClientSize = new Size(974, 709);
            Controls.Add(splitContainer1);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Name = "Form1";
            Load += new EventHandler(Form1_Load);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SendMode_cb.SelectedIndex = 1;
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            txtBoxResults.Text = string.Empty;
            if (SendMode_cb.SelectedIndex == 0) //Send now
            {
                SendNow();
                return;
            }

            SendWithNextBatch();
        }

        private void SendWithNextBatch()
        {
            string connectionString = Properties.Settings.Default.T12DB01ConnectionString;
            var connection = new SqlConnection(connectionString);

            var orderNumbers = OrdersFromList(txtBoxOrderNumbers.Text);
            foreach (string orderNumber in orderNumbers)
            {
                if (string.IsNullOrWhiteSpace(orderNumber)) continue;
                string command = $"delete from EDI945Complete where ordernumber = '{orderNumber}'";
                try
                {
                    connection.Query<string>(command);
                    txtBoxResults.Text += command + @" - Success" + Environment.NewLine;
                }
                catch
                {
                    txtBoxResults.Text += command + @" - Failed" + Environment.NewLine;
                }
            }
        }

        private List<string> OrdersFromList(string text)
        {
            string orders = text.Replace("'", "").Replace("\"", "").Replace("\r", " ")
                .Replace("\n", ","); //Remove ticks and question marks
            return orders.Split(',').ToList();
        }


        private void SendNow()
        {
            if (string.IsNullOrWhiteSpace(txtBoxOrderNumbers.Text)) return;

            var start = new ProcessStartInfo();
            start.FileName = Properties.Settings.Default.Generator945Executable; // Specify exe name not cmd exe.
            start.Arguments = txtBoxOrderNumbers.Text;
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            try
            {
                using (var process = Process.Start(start))
                {
                    using (var reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd();
                        txtBoxResults.Text = result;
                    }
                }
            }
            catch (Exception ex)
            {
                txtBoxResults.Text =
                    $"{start.FileName} not found.  Please check the path and executable in User Settings." +
                    Environment.NewLine;
                txtBoxResults.Text += ex.Message;
            }
        }
    }
}