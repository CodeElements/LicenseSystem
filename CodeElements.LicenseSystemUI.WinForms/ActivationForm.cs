using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CodeElements.LicenseSystemUI.WinForms
{
    public partial class ActivationForm : Form
    {
        private const string CodeElementsLogoPng =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAATCAYAAACZZ43PAAAACXBIWXMAAAEJAAABCQG7HZ2mAAAA30lEQVQ4jaWT4Q2CQAyFvxr/ywhuoCMwAiMwCiMwAm7ACI7ACLgBG9SU3GHFg1ykScml3Ht97bWo6o8DNdADuvIJ6IAyYr7AwB0YEsCUW4LCg6uQIQccfYjgIgE2qZVLUITSnu5OHX/64OSBGz1qDGxnmT8iJR8bVXUk0yQw/m1iT5IB3lWV0+1mqx+nQ/rhOMHhJh5WcJ5liDQuNqhqn80QSlhvXrszhdcwuZ2fRJtzC94c9ysQRzV2xxbO/BJijzVz7iovStfyLEubAVwWLvmMImJqolxvBpzLUtUJ4A3wW/6QTPDRmAAAAABJRU5ErkJggg==";

        public ActivationForm(string licenseKeyFormat, string applicationName, Icon windowIcon)
        {
            InitializeComponent();

            Icon = windowIcon;
            Text = $"{applicationName} - Activate your product";

            licenseKeyDescriptionLabel.Text =
                $"Your license key is {licenseKeyFormat.Length} characters long and should look like this:";

            var placeholderChars = new[] { '*', '#', '&', '0' };
            licenseKeyFormatLabel.Text = placeholderChars.Aggregate(licenseKeyFormat, (s, c) => s.Replace(c, 'X'));

            using (var logoStream = new MemoryStream(Convert.FromBase64String(CodeElementsLogoPng), false))
                logoPictureBox.Image = Image.FromStream(logoStream);
        }

        private void licenseKeyTextBox_TextChanged(object sender, EventArgs e)
        {
            continueButton.Enabled = LicenseSystem.TryParseLicenseKey(licenseKeyTextBox.Text, out _);
        }

        private async void continueButton_Click(object sender, EventArgs e)
        {
            activationProgressBar.Style = ProgressBarStyle.Marquee;
            activationProgressBar.Visible = true;

            licenseKeyTextBox.Enabled = false;
            continueButton.Enabled = false;

            var result = await LicenseSystem.ActivateComputer(licenseKeyTextBox.Text);

            activationProgressBar.Style = ProgressBarStyle.Blocks;
            activationProgressBar.Value = 100;

            string errorMessage;
            switch (result)
            {
                case LicenseSystem.ComputerActivationResult.Valid:
                    DialogResult = DialogResult.OK;
                    return;
                case LicenseSystem.ComputerActivationResult.ConnectionFailed:
                    errorMessage =
                        "The connection failed. Please make sure that your computer is connected to the internet and that your firewall allows the connection and try again.";
                    break;
                case LicenseSystem.ComputerActivationResult.ProjectDisabled:
                    errorMessage = "The project that hosts the licenses was disabled. Please contact the product owner.";
                    break;
                case LicenseSystem.ComputerActivationResult.ProjectNotFound:
                    errorMessage = "The project that hosts the licenses was not found. Please contact the product owner.";
                    break;
                case LicenseSystem.ComputerActivationResult.LicenseSystemNotFound:
                    errorMessage = "The license system was not found. Please contact the product owner.";
                    break;
                case LicenseSystem.ComputerActivationResult.LicenseSystemDisabled:
                    errorMessage = "The license system was disabled. Please contact the product owner.";
                    break;
                case LicenseSystem.ComputerActivationResult.LicenseSystemExpired:
                    errorMessage = "The license system expired. Please contact the product owner.";
                    break;
                case LicenseSystem.ComputerActivationResult.LicenseNotFound:
                    errorMessage = "The license key is not valid. Please try again.";
                    break;
                case LicenseSystem.ComputerActivationResult.LicenseDeactivated:
                    errorMessage = "The license was deactivated. Please contact the product owner if you think that is a mistake.";
                    break;
                case LicenseSystem.ComputerActivationResult.LicenseExpired:
                    errorMessage = "The license expired. Please renew your subscription.";
                    break;
                case LicenseSystem.ComputerActivationResult.IpLimitExhausted:
                    errorMessage = "The ip address limit of your license was exhausted. Please try again tomorrow.";
                    break;
                case LicenseSystem.ComputerActivationResult.ActivationLimitExhausted:
                    errorMessage = "The activation limit of your license was exhausted. Please contact the product owner, he can clear your existing activations.";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            MessageBox.Show(this, errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            activationProgressBar.Visible = false;
            licenseKeyTextBox.Enabled = true;
            continueButton.Enabled = true;
        }
    }
}