<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ActivationForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.activationProgressBar = New System.Windows.Forms.ProgressBar()
        Me.licenseKeyFormatLabel = New System.Windows.Forms.Label()
        Me.licenseKeyDescriptionLabel = New System.Windows.Forms.Label()
        Me.panel1 = New System.Windows.Forms.Panel()
        Me.label3 = New System.Windows.Forms.Label()
        Me.logoPictureBox = New System.Windows.Forms.PictureBox()
        Me.panel2 = New System.Windows.Forms.Panel()
        Me.continueButton = New System.Windows.Forms.Button()
        Me.label2 = New System.Windows.Forms.Label()
        Me.label1 = New System.Windows.Forms.Label()
        Me.licenseKeyTextBox = New System.Windows.Forms.TextBox()
        Me.panel1.SuspendLayout
        CType(Me.logoPictureBox,System.ComponentModel.ISupportInitialize).BeginInit
        Me.SuspendLayout
        '
        'activationProgressBar
        '
        Me.activationProgressBar.Location = New System.Drawing.Point(47, 150)
        Me.activationProgressBar.MarqueeAnimationSpeed = 20
        Me.activationProgressBar.Name = "activationProgressBar"
        Me.activationProgressBar.Size = New System.Drawing.Size(414, 10)
        Me.activationProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee
        Me.activationProgressBar.TabIndex = 15
        Me.activationProgressBar.Visible = false
        '
        'licenseKeyFormatLabel
        '
        Me.licenseKeyFormatLabel.AutoSize = true
        Me.licenseKeyFormatLabel.Font = New System.Drawing.Font("Segoe UI", 8.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0,Byte))
        Me.licenseKeyFormatLabel.Location = New System.Drawing.Point(44, 100)
        Me.licenseKeyFormatLabel.Name = "licenseKeyFormatLabel"
        Me.licenseKeyFormatLabel.Size = New System.Drawing.Size(113, 13)
        Me.licenseKeyFormatLabel.TabIndex = 14
        Me.licenseKeyFormatLabel.Text = "XXXX-XXXXX-XXXXX"
        '
        'licenseKeyDescriptionLabel
        '
        Me.licenseKeyDescriptionLabel.AutoSize = true
        Me.licenseKeyDescriptionLabel.Font = New System.Drawing.Font("Segoe UI", 8.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0,Byte))
        Me.licenseKeyDescriptionLabel.Location = New System.Drawing.Point(44, 85)
        Me.licenseKeyDescriptionLabel.Name = "licenseKeyDescriptionLabel"
        Me.licenseKeyDescriptionLabel.Size = New System.Drawing.Size(323, 13)
        Me.licenseKeyDescriptionLabel.TabIndex = 13
        Me.licenseKeyDescriptionLabel.Text = "Your license key is X characters long and should look like this:"
        '
        'panel1
        '
        Me.panel1.BackColor = System.Drawing.Color.FromArgb(CType(CType(253,Byte),Integer), CType(CType(253,Byte),Integer), CType(CType(253,Byte),Integer))
        Me.panel1.Controls.Add(Me.label3)
        Me.panel1.Controls.Add(Me.logoPictureBox)
        Me.panel1.Controls.Add(Me.panel2)
        Me.panel1.Controls.Add(Me.continueButton)
        Me.panel1.Dock = System.Windows.Forms.DockStyle.Bottom
        Me.panel1.Location = New System.Drawing.Point(0, 197)
        Me.panel1.Name = "panel1"
        Me.panel1.Size = New System.Drawing.Size(597, 50)
        Me.panel1.TabIndex = 12
        '
        'label3
        '
        Me.label3.AutoSize = true
        Me.label3.Font = New System.Drawing.Font("Segoe UI", 8.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0,Byte))
        Me.label3.Location = New System.Drawing.Point(39, 19)
        Me.label3.Name = "label3"
        Me.label3.Size = New System.Drawing.Size(143, 13)
        Me.label3.TabIndex = 7
        Me.label3.Text = "Powered By CodeElements"
        '
        'logoPictureBox
        '
        Me.logoPictureBox.Location = New System.Drawing.Point(21, 16)
        Me.logoPictureBox.Name = "logoPictureBox"
        Me.logoPictureBox.Size = New System.Drawing.Size(16, 19)
        Me.logoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize
        Me.logoPictureBox.TabIndex = 6
        Me.logoPictureBox.TabStop = false
        '
        'panel2
        '
        Me.panel2.BackColor = System.Drawing.Color.FromArgb(CType(CType(224,Byte),Integer), CType(CType(224,Byte),Integer), CType(CType(224,Byte),Integer))
        Me.panel2.Dock = System.Windows.Forms.DockStyle.Top
        Me.panel2.Location = New System.Drawing.Point(0, 0)
        Me.panel2.Name = "panel2"
        Me.panel2.Size = New System.Drawing.Size(597, 1)
        Me.panel2.TabIndex = 5
        '
        'continueButton
        '
        Me.continueButton.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right),System.Windows.Forms.AnchorStyles)
        Me.continueButton.Enabled = false
        Me.continueButton.Location = New System.Drawing.Point(473, 14)
        Me.continueButton.Name = "continueButton"
        Me.continueButton.Size = New System.Drawing.Size(112, 23)
        Me.continueButton.TabIndex = 3
        Me.continueButton.Text = "Continue"
        Me.continueButton.UseVisualStyleBackColor = true
        '
        'label2
        '
        Me.label2.Font = New System.Drawing.Font("Segoe UI", 8.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0,Byte))
        Me.label2.Location = New System.Drawing.Point(44, 52)
        Me.label2.Name = "label2"
        Me.label2.Size = New System.Drawing.Size(506, 33)
        Me.label2.TabIndex = 11
        Me.label2.Text = "To use this software, a license key is required. Please enter your license key in"& _ 
    " the field below and we will contact our servers to verify that key."
        '
        'label1
        '
        Me.label1.AutoSize = true
        Me.label1.Font = New System.Drawing.Font("Segoe UI Semilight", 20.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0,Byte))
        Me.label1.Location = New System.Drawing.Point(40, 6)
        Me.label1.Name = "label1"
        Me.label1.Size = New System.Drawing.Size(268, 37)
        Me.label1.TabIndex = 10
        Me.label1.Text = "Enter your license key"
        '
        'licenseKeyTextBox
        '
        Me.licenseKeyTextBox.Location = New System.Drawing.Point(47, 126)
        Me.licenseKeyTextBox.Name = "licenseKeyTextBox"
        Me.licenseKeyTextBox.Size = New System.Drawing.Size(414, 20)
        Me.licenseKeyTextBox.TabIndex = 9
        '
        'ActivationForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6!, 13!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(597, 247)
        Me.Controls.Add(Me.activationProgressBar)
        Me.Controls.Add(Me.licenseKeyFormatLabel)
        Me.Controls.Add(Me.licenseKeyDescriptionLabel)
        Me.Controls.Add(Me.panel1)
        Me.Controls.Add(Me.label2)
        Me.Controls.Add(Me.label1)
        Me.Controls.Add(Me.licenseKeyTextBox)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.MaximizeBox = false
        Me.MinimizeBox = false
        Me.Name = "ActivationForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "ActivationForm"
        Me.panel1.ResumeLayout(false)
        Me.panel1.PerformLayout
        CType(Me.logoPictureBox,System.ComponentModel.ISupportInitialize).EndInit
        Me.ResumeLayout(false)
        Me.PerformLayout

End Sub

    Private WithEvents activationProgressBar As ProgressBar
    Private WithEvents licenseKeyFormatLabel As Label
    Private WithEvents licenseKeyDescriptionLabel As Label
    Private WithEvents panel1 As Panel
    Private WithEvents label3 As Label
    Private WithEvents logoPictureBox As PictureBox
    Private WithEvents panel2 As Panel
    Private WithEvents continueButton As Button
    Private WithEvents label2 As Label
    Private WithEvents label1 As Label
    Private WithEvents licenseKeyTextBox As TextBox
End Class
