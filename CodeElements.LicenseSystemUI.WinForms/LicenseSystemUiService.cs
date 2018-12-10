using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CodeElements.LicenseSystemUI.WinForms
{
    /// <summary>
    ///     The UI service for WinForms
    /// </summary>
    public static class LicenseSystemUiService
    {
        /// <summary>
        ///     The name of your application which will be displayed in the title bar of the activation window
        /// </summary>
        public static string ApplicationName { get; set; }

        /// <summary>
        ///     The icon of your application which will be used as the titlebar and taskbar icon.
        /// </summary>
        public static Icon ApplicationIcon { get; set; }

        /// <summary>
        ///     Run the license UI service which will initialize the LicenseSystem, check if the current computer is already
        ///     activated and if not, show a dialog that will guide the user through the activation
        /// </summary>
        /// <param name="projectId">The guid of the license system project</param>
        /// <param name="licenseKeyFormat">The format of the license keys</param>
        /// <param name="version">The current version of your application</param>
#if ALLOW_OFFLINE
        public static async Task Run(Guid projectId, string licenseKeyFormat, RSAParameters publicKey, string version =
 null)
        {
            LicenseSystem.Initialize(projectId, licenseKeyFormat, publicKey, version);
#else
        public static void Run(Guid projectId, string licenseKeyFormat, string version = null)
        {
            LicenseSystem.Initialize(projectId, licenseKeyFormat, version);
#endif
            var result = LicenseSystem.CheckComputer().Result;
            if (result == LicenseSystem.ComputerCheckResult.Valid)
                return;

            var form = new ActivationForm(licenseKeyFormat, ApplicationName ?? GetDefaultApplicationName(),
                ApplicationIcon ?? GetDefaultApplicationIcon());

            if (form.ShowDialog() != DialogResult.OK)
                Environment.Exit(-1);

            LicenseSystem.VerifyAccess().Wait();
        }

        //Warning: Obfuscation may destroy these features
        private static string GetDefaultApplicationName()
        {
            return Assembly.GetEntryAssembly().GetName().Name;
        }

        private static Icon GetDefaultApplicationIcon()
        {
            return Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location);
        }
    }
}