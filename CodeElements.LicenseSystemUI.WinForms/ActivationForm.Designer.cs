namespace CodeElements.LicenseSystemUI.WinForms
{
    partial class ActivationForm
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
            this.licenseKeyTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.continueButton = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.logoPictureBox = new System.Windows.Forms.PictureBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.licenseKeyDescriptionLabel = new System.Windows.Forms.Label();
            this.licenseKeyFormatLabel = new System.Windows.Forms.Label();
            this.activationProgressBar = new System.Windows.Forms.ProgressBar();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.logoPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // licenseKeyTextBox
            // 
            this.licenseKeyTextBox.Location = new System.Drawing.Point(47, 131);
            this.licenseKeyTextBox.Name = "licenseKeyTextBox";
            this.licenseKeyTextBox.Size = new System.Drawing.Size(414, 20);
            this.licenseKeyTextBox.TabIndex = 0;
            this.licenseKeyTextBox.TextChanged += new System.EventHandler(this.licenseKeyTextBox_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI Semilight", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(40, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(268, 37);
            this.label1.TabIndex = 1;
            this.label1.Text = "Enter your license key";
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(44, 57);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(506, 33);
            this.label2.TabIndex = 2;
            this.label2.Text = "To use this software, a license key is required. Please enter your license key in" +
    " the field below and we will contact our servers to verify that key.";
            // 
            // continueButton
            // 
            this.continueButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.continueButton.Enabled = false;
            this.continueButton.Location = new System.Drawing.Point(473, 14);
            this.continueButton.Name = "continueButton";
            this.continueButton.Size = new System.Drawing.Size(112, 23);
            this.continueButton.TabIndex = 3;
            this.continueButton.Text = "Continue";
            this.continueButton.UseVisualStyleBackColor = true;
            this.continueButton.Click += new System.EventHandler(this.continueButton_Click);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(253)))), ((int)(((byte)(253)))), ((int)(((byte)(253)))));
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.logoPictureBox);
            this.panel1.Controls.Add(this.panel2);
            this.panel1.Controls.Add(this.continueButton);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 197);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(597, 50);
            this.panel1.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(39, 19);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(143, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Powered By CodeElements";
            // 
            // logoPictureBox
            // 
            this.logoPictureBox.Location = new System.Drawing.Point(21, 16);
            this.logoPictureBox.Name = "logoPictureBox";
            this.logoPictureBox.Size = new System.Drawing.Size(16, 19);
            this.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.logoPictureBox.TabIndex = 6;
            this.logoPictureBox.TabStop = false;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(597, 1);
            this.panel2.TabIndex = 5;
            // 
            // licenseKeyDescriptionLabel
            // 
            this.licenseKeyDescriptionLabel.AutoSize = true;
            this.licenseKeyDescriptionLabel.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.licenseKeyDescriptionLabel.Location = new System.Drawing.Point(44, 90);
            this.licenseKeyDescriptionLabel.Name = "licenseKeyDescriptionLabel";
            this.licenseKeyDescriptionLabel.Size = new System.Drawing.Size(323, 13);
            this.licenseKeyDescriptionLabel.TabIndex = 5;
            this.licenseKeyDescriptionLabel.Text = "Your license key is X characters long and should look like this:";
            // 
            // licenseKeyFormatLabel
            // 
            this.licenseKeyFormatLabel.AutoSize = true;
            this.licenseKeyFormatLabel.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.licenseKeyFormatLabel.Location = new System.Drawing.Point(44, 105);
            this.licenseKeyFormatLabel.Name = "licenseKeyFormatLabel";
            this.licenseKeyFormatLabel.Size = new System.Drawing.Size(113, 13);
            this.licenseKeyFormatLabel.TabIndex = 6;
            this.licenseKeyFormatLabel.Text = "XXXX-XXXXX-XXXXX";
            // 
            // activationProgressBar
            // 
            this.activationProgressBar.Location = new System.Drawing.Point(47, 155);
            this.activationProgressBar.MarqueeAnimationSpeed = 20;
            this.activationProgressBar.Name = "activationProgressBar";
            this.activationProgressBar.Size = new System.Drawing.Size(414, 10);
            this.activationProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.activationProgressBar.TabIndex = 8;
            this.activationProgressBar.Visible = false;
            // 
            // ActivationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(597, 247);
            this.Controls.Add(this.activationProgressBar);
            this.Controls.Add(this.licenseKeyFormatLabel);
            this.Controls.Add(this.licenseKeyDescriptionLabel);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.licenseKeyTextBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ActivationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Activate Product";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.logoPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox licenseKeyTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button continueButton;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label licenseKeyDescriptionLabel;
        private System.Windows.Forms.Label licenseKeyFormatLabel;
        private System.Windows.Forms.PictureBox logoPictureBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ProgressBar activationProgressBar;
    }
}