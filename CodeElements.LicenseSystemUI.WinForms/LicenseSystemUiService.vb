Imports System.Reflection
Imports CodeElements.LicenseSystemUI.WinForms.VB.CodeElements

''' <summary>
'''     The UI service for WinForms
''' </summary>
Public NotInheritable Class LicenseSystemUiService

    ''' <summary>
    '''     The name of your application which will be displayed in the title bar of the activation window
    ''' </summary>
    Public Shared Property ApplicationName As String

    ''' <summary>
    '''     The icon of your application which will be used as the titlebar and taskbar icon.
    ''' </summary>
    Public Shared Property ApplicationIcon As Icon

    ''' <summary>
    '''     Run the license UI service which will initialize the LicenseSystem, check if the current computer is already
    '''     activated and if not, show a dialog that will guide the user through the activation
    ''' </summary>
    ''' <param name="projectId">The guid of the license system project</param>
    ''' <param name="licenseKeyFormat">The format of the license keys</param>
#If ALLOW_OFFLINE Then
    ''' <param name="publicKey">The public key of your license system to validate offline licenses</param>
    ''' <param name="version">The current version of your application</param>
    Public Shared Sub Run(ByVal projectId As Guid, ByVal licenseKeyFormat As String, ByVal Optional version As String = Nothing)
        LicenseSystem.Initialize(projectId, licenseKeyFormat, publicKey, version)
#Else
    '''<param name="version">The current version of your application</param>
    Public Shared Sub Run(ByVal projectId As Guid, ByVal licenseKeyFormat As String, ByVal Optional version As String = Nothing)
        LicenseSystem.Initialize(projectId, licenseKeyFormat, version)
#End If

        Dim result = LicenseSystem.CheckComputer().Result
        If result = LicenseSystem.ComputerCheckResult.Valid Then Return

        Dim form = New ActivationForm(licenseKeyFormat, If(ApplicationName, GetDefaultApplicationName()), If(ApplicationIcon, GetDefaultApplicationIcon()))

        If form.ShowDialog() <> DialogResult.OK Then Environment.Exit(-1)

        LicenseSystem.VerifyAccess().Wait()
    End Sub

    'Warning: Obfuscation may destroy these features
    Private Shared Function GetDefaultApplicationName() As String
        Return Assembly.GetEntryAssembly().GetName().Name
    End Function

    Private Shared Function GetDefaultApplicationIcon() As Icon
        Return Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location)
    End Function
End Class
