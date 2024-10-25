using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Phoenix.Core.Controls;

public class InputBox : Form
{
	private string _msg;

	private string _question;

	private int _answerlength;

	private IContainer components = null;

	private TextBox textBox1;

	private Button button1;

	public InputBox()
	{
		InitializeComponent();
	}

	private void InputBox_Load(object sender, EventArgs e)
	{
		base.Width = _question.Length * 11;
		Text = _question;
	}

	public string GetInput(string question, int maxlength = 0)
	{
		_question = question;
		_answerlength = maxlength;
		if (ShowDialog() == DialogResult.OK)
		{
			return _msg;
		}
		return "";
	}

	private void textBox1_TextChanged(object sender, EventArgs e)
	{
		if (_answerlength != 0 && textBox1.Text.Length > 0)
		{
			MessageBox.Show("Answer can only be " + _answerlength + " characters long");
			textBox1.Text = "";
		}
		_msg = textBox1.Text;
	}

	private void button1_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
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
		this.textBox1 = new System.Windows.Forms.TextBox();
		this.button1 = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.textBox1.Dock = System.Windows.Forms.DockStyle.Top;
		this.textBox1.Location = new System.Drawing.Point(0, 0);
		this.textBox1.Name = "textBox1";
		this.textBox1.Size = new System.Drawing.Size(269, 20);
		this.textBox1.TabIndex = 0;
		this.textBox1.TextChanged += new System.EventHandler(textBox1_TextChanged);
		this.button1.Dock = System.Windows.Forms.DockStyle.Bottom;
		this.button1.Location = new System.Drawing.Point(0, 21);
		this.button1.Name = "button1";
		this.button1.Size = new System.Drawing.Size(269, 23);
		this.button1.TabIndex = 1;
		this.button1.Text = "Ok";
		this.button1.UseVisualStyleBackColor = true;
		this.button1.Click += new System.EventHandler(button1_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(269, 44);
		base.Controls.Add(this.button1);
		base.Controls.Add(this.textBox1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
		base.Name = "InputBox";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "InputBox";
		base.Load += new System.EventHandler(InputBox_Load);
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
