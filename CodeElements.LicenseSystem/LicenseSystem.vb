Imports System.Globalization
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Threading

Namespace CodeElements
    ''' <summary>
    '''     The CodeElements license system that provides the abilities to activate/validate computers and access the online
    '''     service (variables & methods)
    ''' </summary>
    Public Module LicenseSystem
        Private Const LicenseSystemVersion As String = "1.0"
        Private _isInitialized As Boolean
        Private _isLicenseVerified As Boolean
        Private _hardwareIdString As String
        Private _currentToken As JwToken
#If NET20 Then
        Private ReadOnly Client As WebClient = New WebClient()
#Else
        Private Client As HttpClient
#End If

#If NET20 Then
        Private ReadOnly Object ConnectionSemaphore = new Object()
#Else
        Private ReadOnly ConnectionSemaphore As SemaphoreSlim = New SemaphoreSlim(1, 1)
#End If

        Private _licenseType As LicenseTypes
        Private _expirationDate As DateTime?
        Private _customerName As String
        Private _customerEmail As String
        Private ReadOnly LicenseSystemBaseUri As Uri = New Uri("https://service.codeelements.net:2313/")
        Private ReadOnly MethodExecutionBaseUri As Uri = New Uri("https://exec.codeelements.net:2313/")
        Private _verifyLicenseUri As Uri
        Private _activateLicenseUri As Uri
        Private _getVariableUri As String
        Private _executeMethodUri As String
        Private ReadOnly CertificateValidator As CodeElementsCertificateValidator = New CodeElementsCertificateValidator()

        Private _projectId As Guid
        Private _licenseKeyFormat As String

#If ALLOW_OFFLINE Then
        Private Shared _publicKey As RSAParameters
        Private Const LicenseFilename As String = "license.elements"
#End If

        Private Sub Init()
#If NETSTANDARD Then
            Dim handler = New HttpClientHandler()
            handler.Server
#Else
            ServicePointManager.ServerCertificateValidationCallback = AddressOf ServerCertificateValidationCallback
#If Not NET20 Then
            Client = New HttpClient()
#End If
#End If

        End Sub

        Public Property LicenseType As LicenseTypes
            Get
                CheckLicenseVerified()
                Return _licenseType
            End Get
            Private Set
                _licenseType = Value
            End Set
        End Property

        Public Property ExpirationDate As DateTime?
            Get
                CheckLicenseVerified()
                Return _expirationDate
            End Get
            Private Set
                _expirationDate = Value
            End Set
        End Property

        Public ReadOnly Property HardwareId As Byte() = GenerateHardwareId()

        Public ReadOnly Property HardwareIdString As String
            Get
                Return If(_hardwareIdString, (CSharpImpl.__Assign(_hardwareIdString, BitConverter.ToString(HardwareId).Replace("-", Nothing).ToLowerInvariant())))
            End Get
        End Property

        Public Property CustomerName As String
            Get
                CheckLicenseVerified()
                Return _customerName
            End Get
            Private Set
                _customerName = Value
            End Set
        End Property

        Public Property CustomerEmail As String
            Get
                CheckLicenseVerified()
                Return _customerEmail
            End Get
            Private Set
                _customerEmail = Value
            End Set
        End Property

#If MANUAL_INITIALIZATION Then
        Public 
#Else
        Public Sub Initialize(Optional version As String = Nothing)
#End If
            If _isInitialized Then
                Throw New InvalidOperationException("Initialize must only be called once.")
            End If

            _verifyLicenseUri = New Uri(LicenseSystemBaseUri, $"v1/projects/{_projectId}/l/licenses/activations/verify?hwid={HardwareIdString}")
            _activateLicenseUri = New Uri(LicenseSystemBaseUri, $"v1/projects/{_projectId}/l/licenses/activations?hwid={HardwareIdString}&includeCustomer=true")
            _getVariableUri = New Uri(LicenseSystemBaseUri, $"v1/projects/{_projectId}/l/variables").AbsoluteUri & "/{0}"
            _executeMethodUri = New Uri(MethodExecutionBaseUri, $"v1/{_projectId}").AbsoluteUri & "/{0}"

            Dim os = GetOperatingSystem()
            Dim lang = CultureInfo.CurrentUICulture
            Dim userAgent = New StringBuilder().AppendFormat("CodeElementsLicenseSystem/{0} ", LicenseSystemVersion).AppendFormat("({0} {1}; {2}) ", os.OperatingSystem, os.Version.ToString(3), lang).AppendFormat("app/{0} ", If(version, "0.0.0")).ToString()

#If NET20 Then
            Client.Headers.Add(HttpRequestHeader.UserAgent, userAgent)
#Else
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent)
#End If

            _isInitialized = True
        End Sub

        Private ReadOnly PlaceholderChars As Dictionary(Of Char, Char()) = New Dictionary(Of Char, Char()) From {
            {"*"c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray()},
            {"#"c, "1234567890".ToCharArray()},
            {"&"c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray()},
            {"0"c, "0123456789ABCDEF".ToCharArray()}
            }

        ''' <summary>
        '''     Try to convert the given license key to the actual data using the format set in this class
        ''' </summary>
        ''' <param name="licenseKey">The license key that should be parsed</param>
        ''' <param name="licenseData">The relevant license data</param>
        ''' <returns>Return true if the conversion succeeded, else return false</returns>
        Public Function TryParseLicenseKey(licenseKey As String, ByRef licenseData As String) As Boolean
#If NET20 Then
            If licenseKey Is Nothing OrElse String.IsNullOrEmpty(licenseKey.Trim()) Then
#Else
            If String.IsNullOrWhiteSpace(licenseKey) Then
#End If
                licenseData = Nothing
                Return False
            End If

            licenseData = licenseKey.ToUpperInvariant() 'unify
            Dim licenseDataIndex = 0
            Dim chars As Char() = Nothing

            For Each c In _licenseKeyFormat
                If licenseDataIndex >= licenseData.Length Then Return False
                Dim formatChar = c

                If PlaceholderChars.TryGetValue(formatChar, chars) Then
                    If Array.IndexOf(chars, licenseData(licenseDataIndex)) = -1 Then Return False
                    licenseDataIndex += 1

                    Continue For
                End If

                If licenseData(licenseDataIndex) = Char.ToUpperInvariant(formatChar) Then licenseData = licenseData.Remove(licenseDataIndex, 1)
            Next

            Return True
        End Function
    End Module

    Public Enum LicenseTypes
        Asd
    End Enum
End Namespace