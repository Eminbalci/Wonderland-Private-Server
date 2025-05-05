namespace Wonderland_Private_Server
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.tabControl3 = new System.Windows.Forms.TabControl();
            this.tabPage7 = new System.Windows.Forms.TabPage();
            this.MainOutput = new System.Windows.Forms.RichTextBox();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.groupBox_Npc = new System.Windows.Forms.GroupBox();
            this.button_NpcLeave = new System.Windows.Forms.Button();
            this.radioButton_Ride = new System.Windows.Forms.RadioButton();
            this.radioButton_Battle = new System.Windows.Forms.RadioButton();
            this.textBox_FindNPC = new System.Windows.Forms.TextBox();
            this.listBox_NPC = new System.Windows.Forms.ListBox();
            this.groupBox_Items = new System.Windows.Forms.GroupBox();
            this.textBox_FindItems = new System.Windows.Forms.TextBox();
            this.listBox_Items = new System.Windows.Forms.ListBox();
            this.groupBox_Vehicles = new System.Windows.Forms.GroupBox();
            this.button_UnrideVehicle = new System.Windows.Forms.Button();
            this.textBox_FindVehicle = new System.Windows.Forms.TextBox();
            this.listBox_Vehicles = new System.Windows.Forms.ListBox();
            this.groupBox_Maps = new System.Windows.Forms.GroupBox();
            this.textBox_FindMap = new System.Windows.Forms.TextBox();
            this.listBox_Maps = new System.Windows.Forms.ListBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage6.SuspendLayout();
            this.tabControl3.SuspendLayout();
            this.tabPage7.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.groupBox_Npc.SuspendLayout();
            this.groupBox_Items.SuspendLayout();
            this.groupBox_Vehicles.SuspendLayout();
            this.groupBox_Maps.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.tabControl3);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage6.Size = new System.Drawing.Size(788, 487);
            this.tabPage6.TabIndex = 2;
            this.tabPage6.Text = "Main";
            this.tabPage6.UseVisualStyleBackColor = true;
            // 
            // tabControl3
            // 
            this.tabControl3.Controls.Add(this.tabPage7);
            this.tabControl3.Controls.Add(this.tabPage1);
            this.tabControl3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl3.Location = new System.Drawing.Point(3, 3);
            this.tabControl3.Name = "tabControl3";
            this.tabControl3.SelectedIndex = 0;
            this.tabControl3.Size = new System.Drawing.Size(782, 481);
            this.tabControl3.TabIndex = 0;
            // 
            // tabPage7
            // 
            this.tabPage7.Controls.Add(this.MainOutput);
            this.tabPage7.Location = new System.Drawing.Point(4, 22);
            this.tabPage7.Name = "tabPage7";
            this.tabPage7.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage7.Size = new System.Drawing.Size(774, 455);
            this.tabPage7.TabIndex = 0;
            this.tabPage7.Text = "Status";
            this.tabPage7.UseVisualStyleBackColor = true;
            // 
            // MainOutput
            // 
            this.MainOutput.BackColor = System.Drawing.SystemColors.InactiveCaptionText;
            this.MainOutput.ForeColor = System.Drawing.Color.White;
            this.MainOutput.Location = new System.Drawing.Point(6, 6);
            this.MainOutput.Name = "MainOutput";
            this.MainOutput.Size = new System.Drawing.Size(762, 443);
            this.MainOutput.TabIndex = 8;
            this.MainOutput.Text = "";
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.groupBox_Npc);
            this.tabPage1.Controls.Add(this.groupBox_Items);
            this.tabPage1.Controls.Add(this.groupBox_Vehicles);
            this.tabPage1.Controls.Add(this.groupBox_Maps);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(774, 455);
            this.tabPage1.TabIndex = 1;
            this.tabPage1.Text = "Cheat";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // groupBox_Npc
            // 
            this.groupBox_Npc.Controls.Add(this.button_NpcLeave);
            this.groupBox_Npc.Controls.Add(this.radioButton_Ride);
            this.groupBox_Npc.Controls.Add(this.radioButton_Battle);
            this.groupBox_Npc.Controls.Add(this.textBox_FindNPC);
            this.groupBox_Npc.Controls.Add(this.listBox_NPC);
            this.groupBox_Npc.Location = new System.Drawing.Point(584, 6);
            this.groupBox_Npc.Name = "groupBox_Npc";
            this.groupBox_Npc.Size = new System.Drawing.Size(184, 443);
            this.groupBox_Npc.TabIndex = 3;
            this.groupBox_Npc.TabStop = false;
            this.groupBox_Npc.Text = "NPC/Pet";
            // 
            // button_NpcLeave
            // 
            this.button_NpcLeave.Location = new System.Drawing.Point(131, 412);
            this.button_NpcLeave.Name = "button_NpcLeave";
            this.button_NpcLeave.Size = new System.Drawing.Size(47, 23);
            this.button_NpcLeave.TabIndex = 8;
            this.button_NpcLeave.Text = "Leave";
            this.button_NpcLeave.UseVisualStyleBackColor = true;
            this.button_NpcLeave.Click += new System.EventHandler(this.button_NpcLeave_Click);
            // 
            // radioButton_Ride
            // 
            this.radioButton_Ride.AutoSize = true;
            this.radioButton_Ride.Location = new System.Drawing.Point(65, 415);
            this.radioButton_Ride.Name = "radioButton_Ride";
            this.radioButton_Ride.Size = new System.Drawing.Size(47, 17);
            this.radioButton_Ride.TabIndex = 7;
            this.radioButton_Ride.Text = "Ride";
            this.radioButton_Ride.UseVisualStyleBackColor = true;
            // 
            // radioButton_Battle
            // 
            this.radioButton_Battle.AutoSize = true;
            this.radioButton_Battle.Checked = true;
            this.radioButton_Battle.Location = new System.Drawing.Point(7, 415);
            this.radioButton_Battle.Name = "radioButton_Battle";
            this.radioButton_Battle.Size = new System.Drawing.Size(52, 17);
            this.radioButton_Battle.TabIndex = 6;
            this.radioButton_Battle.TabStop = true;
            this.radioButton_Battle.Text = "Battle";
            this.radioButton_Battle.UseVisualStyleBackColor = true;
            this.radioButton_Battle.CheckedChanged += new System.EventHandler(this.radioButton_Battle_CheckedChanged);
            // 
            // textBox_FindNPC
            // 
            this.textBox_FindNPC.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindNPC.Name = "textBox_FindNPC";
            this.textBox_FindNPC.Size = new System.Drawing.Size(171, 20);
            this.textBox_FindNPC.TabIndex = 3;
            // 
            // listBox_NPC
            // 
            this.listBox_NPC.FormattingEnabled = true;
            this.listBox_NPC.Location = new System.Drawing.Point(7, 41);
            this.listBox_NPC.Name = "listBox_NPC";
            this.listBox_NPC.Size = new System.Drawing.Size(171, 368);
            this.listBox_NPC.TabIndex = 2;
            // 
            // groupBox_Items
            // 
            this.groupBox_Items.Controls.Add(this.textBox_FindItems);
            this.groupBox_Items.Controls.Add(this.listBox_Items);
            this.groupBox_Items.Location = new System.Drawing.Point(368, 6);
            this.groupBox_Items.Name = "groupBox_Items";
            this.groupBox_Items.Size = new System.Drawing.Size(210, 443);
            this.groupBox_Items.TabIndex = 2;
            this.groupBox_Items.TabStop = false;
            this.groupBox_Items.Text = "Items";
            // 
            // textBox_FindItems
            // 
            this.textBox_FindItems.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindItems.Name = "textBox_FindItems";
            this.textBox_FindItems.Size = new System.Drawing.Size(197, 20);
            this.textBox_FindItems.TabIndex = 3;
            // 
            // listBox_Items
            // 
            this.listBox_Items.FormattingEnabled = true;
            this.listBox_Items.Location = new System.Drawing.Point(7, 41);
            this.listBox_Items.Name = "listBox_Items";
            this.listBox_Items.Size = new System.Drawing.Size(197, 394);
            this.listBox_Items.TabIndex = 2;
            // 
            // groupBox_Vehicles
            // 
            this.groupBox_Vehicles.Controls.Add(this.button_UnrideVehicle);
            this.groupBox_Vehicles.Controls.Add(this.textBox_FindVehicle);
            this.groupBox_Vehicles.Controls.Add(this.listBox_Vehicles);
            this.groupBox_Vehicles.Location = new System.Drawing.Point(192, 6);
            this.groupBox_Vehicles.Name = "groupBox_Vehicles";
            this.groupBox_Vehicles.Size = new System.Drawing.Size(171, 443);
            this.groupBox_Vehicles.TabIndex = 1;
            this.groupBox_Vehicles.TabStop = false;
            this.groupBox_Vehicles.Text = "Vehicles";
            // 
            // button_UnrideVehicle
            // 
            this.button_UnrideVehicle.Location = new System.Drawing.Point(7, 414);
            this.button_UnrideVehicle.Name = "button_UnrideVehicle";
            this.button_UnrideVehicle.Size = new System.Drawing.Size(158, 23);
            this.button_UnrideVehicle.TabIndex = 9;
            this.button_UnrideVehicle.Text = "Remove Vehicle";
            this.button_UnrideVehicle.UseVisualStyleBackColor = true;
            this.button_UnrideVehicle.Click += new System.EventHandler(this.button_UnrideVehicle_Click);
            // 
            // textBox_FindVehicle
            // 
            this.textBox_FindVehicle.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindVehicle.Name = "textBox_FindVehicle";
            this.textBox_FindVehicle.Size = new System.Drawing.Size(158, 20);
            this.textBox_FindVehicle.TabIndex = 3;
            // 
            // listBox_Vehicles
            // 
            this.listBox_Vehicles.FormattingEnabled = true;
            this.listBox_Vehicles.Location = new System.Drawing.Point(7, 41);
            this.listBox_Vehicles.Name = "listBox_Vehicles";
            this.listBox_Vehicles.Size = new System.Drawing.Size(158, 368);
            this.listBox_Vehicles.TabIndex = 2;
            // 
            // groupBox_Maps
            // 
            this.groupBox_Maps.Controls.Add(this.textBox_FindMap);
            this.groupBox_Maps.Controls.Add(this.listBox_Maps);
            this.groupBox_Maps.Location = new System.Drawing.Point(6, 6);
            this.groupBox_Maps.Name = "groupBox_Maps";
            this.groupBox_Maps.Size = new System.Drawing.Size(180, 443);
            this.groupBox_Maps.TabIndex = 0;
            this.groupBox_Maps.TabStop = false;
            this.groupBox_Maps.Text = "Maps";
            // 
            // textBox_FindMap
            // 
            this.textBox_FindMap.Location = new System.Drawing.Point(7, 20);
            this.textBox_FindMap.Name = "textBox_FindMap";
            this.textBox_FindMap.Size = new System.Drawing.Size(167, 20);
            this.textBox_FindMap.TabIndex = 1;
            // 
            // listBox_Maps
            // 
            this.listBox_Maps.FormattingEnabled = true;
            this.listBox_Maps.Location = new System.Drawing.Point(7, 41);
            this.listBox_Maps.Name = "listBox_Maps";
            this.listBox_Maps.Size = new System.Drawing.Size(167, 394);
            this.listBox_Maps.TabIndex = 0;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(796, 513);
            this.tabControl1.TabIndex = 2;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(796, 513);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "Form1";
            this.Text = "WLO Private Server CheatEngine";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabPage6.ResumeLayout(false);
            this.tabControl3.ResumeLayout(false);
            this.tabPage7.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.groupBox_Npc.ResumeLayout(false);
            this.groupBox_Npc.PerformLayout();
            this.groupBox_Items.ResumeLayout(false);
            this.groupBox_Items.PerformLayout();
            this.groupBox_Vehicles.ResumeLayout(false);
            this.groupBox_Vehicles.PerformLayout();
            this.groupBox_Maps.ResumeLayout(false);
            this.groupBox_Maps.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TabPage tabPage6;
        private System.Windows.Forms.TabControl tabControl3;
        private System.Windows.Forms.TabPage tabPage7;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.RichTextBox MainOutput;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.GroupBox groupBox_Maps;
        private System.Windows.Forms.GroupBox groupBox_Vehicles;
        private System.Windows.Forms.GroupBox groupBox_Npc;
        private System.Windows.Forms.GroupBox groupBox_Items;
        private System.Windows.Forms.ListBox listBox_Maps;
        private System.Windows.Forms.TextBox textBox_FindMap;
        private System.Windows.Forms.TextBox textBox_FindNPC;
        private System.Windows.Forms.ListBox listBox_NPC;
        private System.Windows.Forms.TextBox textBox_FindItems;
        private System.Windows.Forms.ListBox listBox_Items;
        private System.Windows.Forms.TextBox textBox_FindVehicle;
        private System.Windows.Forms.ListBox listBox_Vehicles;
        private System.Windows.Forms.RadioButton radioButton_Ride;
        private System.Windows.Forms.RadioButton radioButton_Battle;
        private System.Windows.Forms.Button button_NpcLeave;
        private System.Windows.Forms.Button button_UnrideVehicle;
    }
}

