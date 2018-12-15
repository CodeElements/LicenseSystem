Imports System.IO
Imports CodeElements.LicenseSystemUI.WinForms.VB.CodeElements

Public Class ActivationForm
    Private Const CodeElementsLogoPng As String = "iVBORw0KGgoAAAANSUhEUgAAABAAAAATCAYAAACZZ43PAAAACXBIWXMAAAEJAAABCQG7HZ2mAAAA30lEQVQ4jaWT4Q2CQAyFvxr/ywhuoCMwAiMwCiMwAm7ACI7ACLgBG9SU3GHFg1ykScml3Ht97bWo6o8DNdADuvIJ6IAyYr7AwB0YEsCUW4LCg6uQIQccfYjgIgE2qZVLUITSnu5OHX/64OSBGz1qDGxnmT8iJR8bVXUk0yQw/m1iT5IB3lWV0+1mqx+nQ/rhOMHhJh5WcJ5liDQuNqhqn80QSlhvXrszhdcwuZ2fRJtzC94c9ysQRzV2xxbO/BJijzVz7iovStfyLEubAVwWLvmMImJqolxvBpzLUtUJ4A3wW/6QTPDRmAAAAABJRU5ErkJggg=="

    Public Sub New(licenseKeyFormat As String, applicationName As String, windowIcon As Icon)
        InitializeComponent()
        Icon = windowIcon
        Text = $"{applicationName} - Activate your product"
        licenseKeyDescriptionLabel.Text = $"Your license key is {licenseKeyFormat.Length} characters long and should look like this:"
        Dim placeholderChars = {"*"c, "#"c, "&"c, "0"c}
        licenseKeyFormatLabel.Text = placeholderChars.Aggregate(licenseKeyFormat, Function(s, c) s.Replace(c, "X"c))

        Using logoStream = New MemoryStream(Convert.FromBase64String(CodeElementsLogoPng), False)
            logoPictureBox.Image = Image.FromStream(logoStream)
        End Using
    End Sub

    Private Sub licenseKeyTextBox_TextChanged(sender As Object, e As EventArgs) Handles licenseKeyTextBox.TextChanged
        Dim foo As String
        continueButton.Enabled = LicenseSystem.TryParseLicenseKey(licenseKeyTextBox.Text, foo)
    End Sub

    Private Async Sub continueButton_Click(sender As Object, e As EventArgs) Handles continueButton.Click
        activationProgressBar.Style = ProgressBarStyle.Marquee
        activationProgressBar.Visible = True

        licenseKeyTextBox.Enabled = False
        continueButton.Enabled = False

        Dim result = Await LicenseSystem.ActivateComputer(licenseKeyTextBox.Text)

        activationProgressBar.Style = ProgressBarStyle.Blocks
        activationProgressBar.Value = 100

        Dim errorMessage As String
        Select Case result
            Case LicenseSystem.ComputerActivationResult.Valid
                DialogResult = DialogResult.OK
                Return
            Case LicenseSystem.ComputerActivationResult.ConnectionFailed
                errorMessage = "The connection failed. Please make sure that your computer is connected to the internet and that your firewall allows the connection and try again."
            Case LicenseSystem.ComputerActivationResult.ProjectDisabled
                errorMessage = "The project that hosts the licenses was disabled. Please contact the product owner."
            Case LicenseSystem.ComputerActivationResult.ProjectNotFound
                errorMessage = "The project that hosts the licenses was not found. Please contact the product owner."
            Case LicenseSystem.ComputerActivationResult.LicenseSystemNotFound
                errorMessage = "The license system was not found. Please contact the product owner."
            Case LicenseSystem.ComputerActivationResult.LicenseSystemDisabled
                errorMessage = "The license system was disabled. Please contact the product owner."
            Case LicenseSystem.ComputerActivationResult.LicenseSystemExpired
                errorMessage = "The license system expired. Please contact the product owner."
            Case LicenseSystem.ComputerActivationResult.LicenseNotFound
                errorMessage = "The license key is not valid. Please try again."
            Case LicenseSystem.ComputerActivationResult.LicenseDeactivated
                errorMessage = "The license was deactivated. Please contact the product owner if you think that is a mistake."
            Case LicenseSystem.ComputerActivationResult.LicenseExpired
                errorMessage = "The license expired. Please renew your subscription."
            Case LicenseSystem.ComputerActivationResult.IpLimitExhausted
                errorMessage = "The ip address limit of your license was exhausted. Please try again tomorrow."
            Case LicenseSystem.ComputerActivationResult.ActivationLimitExhausted
                errorMessage = "The activation limit of your license was exhausted. Please contact the product owner, he can clear your existing activations."
            Case Else
                Throw New ArgumentOutOfRangeException()
        End Select

        MessageBox.Show(Me, errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.[Error])
        activationProgressBar.Visible = False
        licenseKeyTextBox.Enabled = True
        continueButton.Enabled = True
    End Sub
End Class