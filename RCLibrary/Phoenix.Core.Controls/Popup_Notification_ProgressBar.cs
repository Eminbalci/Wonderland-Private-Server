using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Phoenix.Core.Controls;

public class Popup_Notification_ProgressBar : Form
{
	public string _msg;

	public int prog;

	private Task _tsk;

	public Delegate DoWork;

	private IContainer components = null;

	private ProgressBar progressBar1;

	private Label label1;

	private System.Windows.Forms.Timer timer1;

	public Popup_Notification_ProgressBar()
	{
		InitializeComponent();
	}

	private void Popup_Notification_ProgressBar_Load(object sender, EventArgs e)
	{
		timer1.Start();
		_tsk = Task.Factory.StartNew(delegate
		{
			Thread.Sleep(500);
			DoWork.DynamicInvoke(this);
		});
	}

	private void timer1_Tick(object sender, EventArgs e)
	{
		label1.Text = _msg;
		progressBar1.Value = prog;
	}

	private void Popup_Notification_ProgressBar_FormClosing(object sender, FormClosingEventArgs e)
	{
		timer1.Stop();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		this.progressBar1 = new System.Windows.Forms.ProgressBar();
		this.label1 = new System.Windows.Forms.Label();
		this.timer1 = new System.Windows.Forms.Timer(this.components);
		base.SuspendLayout();
		this.progressBar1.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.progressBar1.Location = new System.Drawing.Point(0, 23);
		this.progressBar1.Name = "progressBar1";
		this.progressBar1.Size = new System.Drawing.Size(275, 14);
		this.progressBar1.TabIndex = 0;
		this.label1.AutoSize = true;
		this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.label1.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
		this.label1.Location = new System.Drawing.Point(0, 0);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(35, 13);
		this.label1.TabIndex = 1;
		this.label1.Text = "label1";
		this.timer1.Enabled = true;
		this.timer1.Interval = 1;
		this.timer1.Tick += new System.EventHandler(timer1_Tick);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
		base.ClientSize = new System.Drawing.Size(275, 37);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.progressBar1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.Name = "Popup_Notification_ProgressBar";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Popup_Notification_ProgressBar";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(Popup_Notification_ProgressBar_FormClosing);
		base.Load += new System.EventHandler(Popup_Notification_ProgressBar_Load);
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
