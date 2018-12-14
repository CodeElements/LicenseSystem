#Disable Warning BC42030 ' Variable is passed by reference before it has been assigned a value
Imports System.Globalization
Imports System.IO
Imports System.Net.Security
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Serialization

#If ALLOW_OFFLINE Then
Imports System.Text.RegularExpressions

#End If

#If Not NETSTANDARD Then
Imports System.Net

#End If

#If NET20 Then
Imports System.Collections.Specialized

#Else
Imports System.Linq
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Threading
Imports System.Threading.Tasks

#End If

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
        Private ReadOnly ConnectionLock As New Object()
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
        Private _publicKey As RSAParameters
        Private Const LicenseFilename = "license.elements"
#End If

        Private Sub Init()
#If NETSTANDARD Then
            Dim handler = New HttpClientHandler()
            handler.ServerCertificateCustomValidationCallback = AddressOf ServerCertificateValidationCallback
            Client = new HttpClient(handler)
#Else
            ServicePointManager.ServerCertificateValidationCallback = AddressOf ServerCertificateValidationCallback
#If Not NET20 Then
            Client = New HttpClient()
#End If 'NET20
#End If 'NETSTANDARD
        End Sub

        ''' <summary>
        '''     The type of the current license
        ''' </summary>
        Public Property LicenseType As LicenseTypes
            Get
                CheckLicenseVerified()
                Return _licenseType
            End Get
            Private Set
                _licenseType = Value
            End Set
        End Property

        ''' <summary>
        '''     The expiration date in the UTC time zone. If the property is null, the license does not have an expiration date
        ''' </summary>
        Public Property ExpirationDate As DateTime?
            Get
                CheckLicenseVerified()
                Return _expirationDate
            End Get
            Private Set
                _expirationDate = Value
            End Set
        End Property

        ''' <summary>
        '''     The current hardware id of the machine
        ''' </summary>
        Public ReadOnly Property HardwareId As Byte() = GenerateHardwareId()

        ''' <summary>
        '''     The current hardware id of the machine formatted as string
        ''' </summary>
        Public ReadOnly Property HardwareIdString As String
            Get
                If (_hardwareIdString Is Nothing) Then
                    _hardwareIdString = BitConverter.ToString(HardwareId).Replace("-", Nothing).ToLowerInvariant()
                End If
                Return _hardwareIdString
            End Get
        End Property

        ''' <summary>
        '''     The customer name of the license
        ''' </summary>
        Public Property CustomerName As String
            Get
                CheckLicenseVerified()
                Return _customerName
            End Get
            Private Set
                _customerName = Value
            End Set
        End Property

        ''' <summary>
        '''     The customer E-Mail address of the license
        ''' </summary>
        Public Property CustomerEmail As String
            Get
                CheckLicenseVerified()
                Return _customerEmail
            End Get
            Private Set
                _customerEmail = Value
            End Set
        End Property

        ''' <summary>
        '''     Initialize the License System. This method must be called once at the start of your application
        ''' </summary>
#If MANUAL_INITIALIZATION Then
        ''' <param name="projectId">The guid of the license system project</param>
        ''' <param name="licenseKeyFormat">The format of the license keys</param>
#If ALLOW_OFFLINE Then
        ''' <param name="publicKey">The public key of the license system used to validate offline license files</param>
#End If
        ''' <param name="version">The current version of your application</param>
#End If
#If MANUAL_INITIALIZATION AndAlso ALLOW_OFFLINE Then
        Public Sub Initialize(projectId As Guid, licenseKeyFormat As String, publicKey As RSAParameters, Optional version As String = Nothing)
#ElseIf MANUAL_INITIALIZATION Then
        Public Sub Initialize(projectId As Guid, licenseKeyFormat As String, Optional version As String = Nothing)
#Else
        Public Sub Initialize(Optional version As String = Nothing)
#End If
            If _isInitialized Then
                Throw New InvalidOperationException("Initialize must only be called once.")
            End If

            Init() ' Static constructor

#If MANUAL_INITIALIZATION Then
            _projectId = projectId
#If ALLOW_OFFLINE Then
            _publicKey = publicKey
#End If
            _licenseKeyFormat = licenseKeyFormat
#End If

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

#If NET20 Then
        ''' <summary>
        '''     Check if the current machine is activated and if the license is valid.
        ''' </summary>
        ''' <returns>Return a result which specifies the current state.</returns>
        Public Function CheckComputer() As ComputerCheckResult
            CheckInitialized()

            Try
                Dim response = Client.DownloadString(_verifyLicenseUri)
                Dim licenseInfo = Deserialize(Of LicenseInformation)(response)
                ApplyLicenseInformation(licenseInfo)
#If ALLOW_OFFLINE
                UpdateLicenseFile(licenseInfo)
#End If
                Return ComputerCheckResult.Valid
            Catch e As WebException
                Dim responseStream As Stream = e.Response.GetResponseStream()

                If responseStream IsNot Nothing Then
                    Using responseStream
                        Dim errors As RestError()
                        If DeserializeErrors(New StreamReader(responseStream).ReadToEnd(), errors) Then
                            Dim [error] = errors(0)

                            Select Case [error].Code
                                Case ErrorCode.LicenseSystem_Activations_InvalidHardwareId
                                    Throw New InvalidOperationException([error].Message)
                            End Select

                            TerminateOfflineLicense()
                            Return CType([error].Code, ComputerCheckResult)
                        End If
                    End Using
                End If
            Catch ex As Exception
            End Try

#If ALLOW_OFFLINE
            If CheckLicenseFile() Then
                Return ComputerCheckResult.Valid
            End If
#End If
            Return ComputerCheckResult.ConnectionFailed
        End Function

        ''' <summary>
        '''     Activate the current machine using the license key
        ''' </summary>
        ''' <param name="licenseKey">The license key</param>
        ''' <returns>Return a result which specifies whether the operation was successful or something went wrong.</returns>
        ''' <exception cref="FormatException">
        '''     Thrown when the license key format does not match the format of the service. It is
        '''     recommended to check the format using <see cref="TryParseLicenseKey" /> before calling this method.
        ''' </exception>
        Public Function ActivateComputer(licenseKey As String) As ComputerActivationResult
            CheckInitialized()

            Dim requestUri = AddParameter(_activateLicenseUri, "key", licenseKey)

#If ALLOW_OFFLINE Then
            requestUri = AddParameter(requestUri, "getLicense", "true")
#End If
#If GET_CUSTOMER_INFORMATION Then
                requestUri = AddParameter(requestUri, "includeCustomerInfo", "true");
#End If

            Try
                Dim response =
                    Encoding.UTF8.GetString(Client.UploadValues(requestUri, "POST", New NameValueCollection()))
#If ALLOW_OFFLINE Then
                Dim information = Deserialize<OfflineLicenseInformation>(response)
                WriteLicenseFile(information)
#Else
                Dim information = Deserialize(Of LicenseInformation)(response)
#End If
                ApplyLicenseInformation(information)
                Return ComputerActivationResult.Valid
            Catch e As WebException
                Dim responseStream As Stream = e.Response.GetResponseStream()

                If responseStream IsNot Nothing Then
                                Using responseStream
                                    Dim errors As RestError()
                                    If DeserializeErrors(New StreamReader(responseStream).ReadToEnd(), errors) Then
                                        Dim [error] = errors(0)

                                        Select Case [error].Code
                                            Case ErrorCode.LicenseSystem_Activations_InvalidHardwareId
                                                Throw New InvalidOperationException([error].Message)
                                            Case ErrorCode.LicenseSystem_Activations_InvalidLicenseKeyFormat
                                                Throw New FormatException("The format of the license key is invalid.")
                                        End Select

                                        TerminateOfflineLicense()
                                        Return CType([error].Code, ComputerActivationResult)
                                    End If
                                End Using
                            End If
            End Try

            Return ComputerActivationResult.ConnectionFailed
        End Function
#Else

        ''' <summary>
        '''     Check if the current machine is activated and if the license is valid.
        ''' </summary>
        ''' <returns>Return a result which specifies the current state.</returns>
        Public Async Function CheckComputer() As Task(Of ComputerCheckResult)
            CheckInitialized()

            Try
                Dim response = Await Client.GetAsync(_verifyLicenseUri).ConfigureAwait(False)

                If response.IsSuccessStatusCode Then
                    Dim licenseInfo = Deserialize(Of LicenseInformation)(Await response.Content.ReadAsStringAsync().ConfigureAwait(False))
                    ApplyLicenseInformation(licenseInfo)
#If ALLOW_OFFLINE
                    await UpdateLicenseFile(licenseInfo).ConfigureAwait(false)
#End If
                    Return ComputerCheckResult.Valid
                End If

                Dim errors As RestError()
                If Not DeserializeErrors(Await response.Content.ReadAsStringAsync().ConfigureAwait(False), errors) Then
                    response.EnsureSuccessStatusCode()
                End If

                'on deserialize error move to end of function
                Dim restError = errors(0)
                Select Case restError.Code
                    Case ErrorCode.LicenseSystem_Activations_InvalidHardwareId
                        Throw New InvalidOperationException(restError.Message)
                End Select

                TerminateOfflineLicense()
                Return CType(restError.Code, ComputerCheckResult)
            Catch HttpException As HttpRequestException 'for connection errors
            Catch SerializationException As JsonException 'in case that the http server returns an error instead of the webservice
            End Try

#If ALLOW_OFFLINE
            If CheckLicenseFile() Then
                Return ComputerCheckResult.Valid
            End If
#End If

            Return ComputerCheckResult.ConnectionFailed
        End Function

        ''' <summary>
        '''     Activate the current machine using the license key
        ''' </summary>
        ''' <param name="licenseKey">The license key</param>
        ''' <returns>Return a result which specifies whether the operation was successful or something went wrong.</returns>
        ''' <exception cref="FormatException">
        '''     Thrown when the license key format does not match the format of the service. It is
        '''     recommended to check the format using <see cref="TryParseLicenseKey" /> before calling this method.
        ''' </exception>
        Public Async Function ActivateComputer(licenseKey As String) As Task(Of ComputerActivationResult)
            CheckInitialized()

            Try
                Dim requestUri = AddParameter(_activateLicenseUri, "key", licenseKey)

                Using response = Await Client.PostAsync(requestUri, Nothing).ConfigureAwait(False)
                    Dim responseString = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)

                    If response.IsSuccessStatusCode Then
                        Dim information = Deserialize(Of LicenseInformation)(responseString)
                        ApplyLicenseInformation(information)
                        Return ComputerActivationResult.Valid
                    End If

                    Dim errors As RestError()
                    If Not DeserializeErrors(responseString, errors) Then Return ComputerActivationResult.ConnectionFailed

                    For Each err As RestError In errors
                        Select Case err.Code
                            Case ErrorCode.LicenseSystem_Activations_InvalidHardwareId
                                Throw New InvalidOperationException(err.Message)
                            Case ErrorCode.LicenseSystem_Activations_InvalidLicenseKeyFormat
                                Throw New FormatException("The format of the license key is invalid.")
                        End Select
                    Next

                    TerminateOfflineLicense()
                    Return CType(errors(0).Code, ComputerActivationResult)
                End Using

            Catch ex As HttpRequestException
            Catch ex As JsonException
            End Try

            Return ComputerActivationResult.ConnectionFailed
        End Function
#End If

#Region "Access"
#If NET20 Then
        ''' <summary>
        '''     Verify that the license system is initialized and the license valid. Throws an exception if the conditions are not
        '''     met.
        ''' </summary>
        Public Sub VerifyAccess()
            CheckInitialized()
            CheckJwt()
        End Sub

        ''' <summary>
        '''     Check if the license type of the current license is within the <see cref="licenseTypes" />. Throw an exception if
        '''     not.
        ''' </summary>
        ''' <param name="licenseTypes">The license types that are required</param>
        Public Sub Require(ParamArray licenseTypes As LicenseTypes())
            VerifyAccess()
            If Array.IndexOf(licenseTypes, LicenseType) = -1 Then Throw New UnauthorizedAccessException("Your license is not permitted to execute that operation.")
        End Sub

        ''' <summary>
        '''     Check whether the current license type is within the <see cref="licenseTypes" />. Return false if not.
        ''' </summary>
        ''' <param name="licenseTypes">The license types that should be checked against</param>
        ''' <returns>Return true if the current license type is part of the <see cref="licenseTypes" />, return false if not.</returns>
        Public Function Check(ParamArray licenseTypes As LicenseTypes()) As Boolean
            VerifyAccess()
            Return Array.IndexOf(licenseTypes, LicenseType) > -1
        End Function
#Else
        ''' <summary>
        '''     Verify that the license system is initialized and the license valid. Throws an exception if the conditions are not
        '''     met.
        ''' </summary>
        Async Function VerifyAccess() As Task
            CheckInitialized()
            Await CheckJwt().ConfigureAwait(False)
        End Function

        ''' <summary>
        '''     Check if the license type of the current license is within the <see cref="licenseTypes" />. Throw an exception if
        '''     not.
        ''' </summary>
        ''' <param name="licenseTypes">The license types that are required</param>
        Async Function Require(ParamArray licenseTypes As LicenseTypes()) As Task
            Await VerifyAccess().ConfigureAwait(False)
            If Array.IndexOf(licenseTypes, LicenseType) = -1 Then Throw New UnauthorizedAccessException("Your license is not permitted to execute that operation.")
        End Function

        ''' <summary>
        '''     Check whether the current license type is within the <see cref="licenseTypes" />. Return false if not.
        ''' </summary>
        ''' <param name="licenseTypes">The license types that should be checked against</param>
        ''' <returns>Return true if the current license type is part of the <see cref="licenseTypes" />, return false if not.</returns>
        Async Function Check(ParamArray licenseTypes As LicenseTypes()) As Task(Of Boolean)
            Await VerifyAccess().ConfigureAwait(False)
            Return Array.IndexOf(licenseTypes, LicenseType) > -1
        End Function

#End If
#End Region

#Region "Online Service"

#If NET20 Then
        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <typeparam name="T">The type of the variable</typeparam>
        ''' <param name="name">The name of the variable</param>
        ''' <returns>Return the current value</returns>
        Public Function GetOnlineVariable(Of T)(name As String) As T
            CheckJwt()

            'no type check
            Dim variable = InternalGetOnlineVariable(name, Nothing)
            Dim variableType = variable.GetNetType()

            If Not variableType.Equals(GetType(T)) Then
                Dim message = $"The variable ""{name}"" is of type {variableType}, but the generic type {GetType(T)} was submitted."
#If ENFORCE_VARIABLE_TYPES Then
                Throw New ArgumentException(message)
#Else
                Debug.Print(message)
#End If
            End If

            Return Deserialize(Of T)(variable.Value)
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <typeparam name="T">The type of the variable</typeparam>
        ''' <param name="name">The name of the variable</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        ''' <returns>Return the current value</returns>
        Public Function GetOnlineVariable(Of T)(name As String, obfuscationKey As Integer) As T
            CheckJwt()

            Dim variable = InternalGetOnlineVariable(name, obfuscationKey)
            Dim variableType = variable.GetNetType()

            If Not variableType.Equals(GetType(T)) Then
                Dim message = $"The variable ""{name}"" is of type {variableType}, but the generic type {GetType(T)} was submitted."
#If ENFORCE_VARIABLE_TYPES Then
                Throw New ArgumentException(message)
#Else
                Debug.Print(message)
#End If
            End If

            Return Deserialize(Of T)(variable.Value)
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <param name="name">The name of the variable</param>
        ''' <returns>Return the current value</returns>
        Public Function GetOnlineVariable(name As String) As Object
            CheckJwt()

            Dim variable = InternalGetOnlineVariable(name, Nothing)
            Return Deserialize(variable.Value, variable.GetNetType())
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <param name="name">The name of the variable</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        ''' <returns>Return the current value</returns>
        Public Function GetOnlineVariable(name As String, obfuscationKey As Integer) As Object
            CheckJwt()

            Dim variable = InternalGetOnlineVariable(name, obfuscationKey)
            Return Deserialize(variable.Value, variable.GetNetType())
        End Function

        ''' <summary>
        '''     Execute an online method and get return the result
        ''' </summary>
        ''' <typeparam name="T">The type of the return value</typeparam>
        ''' <param name="name">The name of the method</param>
        ''' <param name="parameters">The parameters for the method to execute</param>
        ''' <returns>Return the result of the method</returns>
        Public Function ExecuteOnlineMethod(Of T)(name As String, ParamArray parameters As Object()) As T
            CheckJwt()

            Dim returnValue = InternalExecuteOnlineMethod(name, parameters, Nothing)
            Return Deserialize(Of T)(returnValue.Value)
        End Function

        ''' <summary>
        '''     Execute an online method and get return the result
        ''' </summary>
        ''' <typeparam name="T">The type of the return value</typeparam>
        ''' <param name="name">The name of the method</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the method <see cref="name" /></param>
        ''' <param name="parameters">The parameters for the method to execute</param>
        ''' <returns>Return the result of the method</returns>
        Public Function ExecuteOnlineMethod(Of T)(name As String, obfuscationKey As Integer, ParamArray parameters As Object()) As T
            CheckJwt()

            Dim returnValue = InternalExecuteOnlineMethod(name, parameters, obfuscationKey)
            Return Deserialize(Of T)(returnValue.Value)
        End Function

        Private Function InternalGetOnlineVariable(name As String, obfuscationKey As Integer?) As OnlineVariableValue
            Dim uri = New Uri(String.Format(_getVariableUri, name))
            If obfuscationKey IsNot Nothing Then uri = AddParameter(uri, "obfuscationKey", obfuscationKey.Value.ToString())

            Try
                Dim response = Client.DownloadString(uri)
                Return Deserialize(Of OnlineVariableValue)(response)
            Catch e As WebException
                Dim responseStream As Stream = e.Response.GetResponseStream()

                If responseStream IsNot Nothing Then
                    Using responseStream
                        Dim errors As RestError()
                        If DeserializeErrors(New StreamReader(responseStream).ReadToEnd(), errors) Then

                            For Each [error] In errors
                                Select Case [error].Code
                                    Case ErrorCode.LicenseSystem_Variables_NotFound
                                        Throw New InvalidOperationException($"The variable ""{name}"" could not be found.")
                                End Select
                            Next

                            Throw New InvalidOperationException(errors(0).Message)
                        End If
                    End Using
                End If

                Throw e
            End Try
        End Function

        Private Function InternalExecuteOnlineMethod(name As String, parameters As Object(), obfuscationKey As Integer?) As OnlineVariableValue
            Dim uri = New Uri(String.Format(_executeMethodUri, name))
            Dim queryParameters = New List(Of String)()
            If obfuscationKey IsNot Nothing Then queryParameters.Add("obfuscationKey=" & Uri.EscapeDataString(obfuscationKey.Value.ToString()))

            If parameters?.Length > 0 Then
                For i = 0 To parameters.Length - 1
                    queryParameters.Add("arg" & i & "=" & Uri.EscapeDataString(Serialize(parameters(i))))
                Next
            End If

            Dim builder = New UriBuilder(uri) With {
                .Query = String.Join("&", queryParameters.ToArray())
            }
            uri = builder.Uri

            Try
                Dim response = Client.DownloadString(uri)
                Return Deserialize(Of OnlineVariableValue)(response)
            Catch e As WebException
                Dim responseStream As Stream = e.Response.GetResponseStream()

                If responseStream IsNot Nothing Then
                    Using responseStream
                        
                        Dim errors As RestError()
                        If DeserializeErrors(New StreamReader(responseStream).ReadToEnd(), errors) Then
                            For Each [error] In errors
                                Select Case [error].Code
                                    Case ErrorCode.LicenseSystem_Methods_NotFound
                                        Throw New InvalidOperationException($"The method ""{name}"" could not be found.")
                                    Case ErrorCode.LicenseSystem_Methods_ExecutionFailed
                                        Throw New InvalidOperationException($"The execution of method ""{name}"" failed.")
                                End Select
                            Next

                            Throw New InvalidOperationException(errors(0).Message)
                        End If
                    End Using
                End If

                Throw e
            End Try
        End Function
#Else
        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <typeparam name="T">The type of the variable</typeparam>
        ''' <param name="name">The name of the variable</param>
        ''' <returns>Return the current value</returns>
        Function GetOnlineVariable(Of T)(name As String) As T
            Return GetOnlineVariableAsync(Of T)(name).Result
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <typeparam name="T">The type of the variable</typeparam>
        ''' <param name="name">The name of the variable</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        ''' <returns>Return the current value</returns>
        Function GetOnlineVariable(Of T)(name As String, obfuscationKey As Integer) As T
            Return GetOnlineVariableAsync(Of T)(name, obfuscationKey).Result
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <param name="name">The name of the variable</param>
        ''' <returns>Return the current value</returns>
        Function GetOnlineVariable(name As String) As Object
            Return GetOnlineVariableAsync(name).Result
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <param name="name">The name of the variable</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        ''' <returns>Return the current value</returns>
        Function GetOnlineVariable(name As String, obfuscationKey As Integer) As Object
            Return GetOnlineVariableAsync(name, obfuscationKey).Result
        End Function

        ''' <summary>
        '''     Execute an online method and return the result
        ''' </summary>
        ''' <typeparam name="T">The type of the return value</typeparam>
        ''' <param name="name">The name of the method</param>
        ''' <param name="parameters">The parameters for the method to execute</param>
        ''' <returns>Return the result of the method</returns>
        Function ExecuteOnlineMethod(Of T)(name As String, ParamArray parameters As Object()) As T
            Return ExecuteOnlineMethodAsync(Of T)(name, parameters).Result
        End Function

        ''' <summary>
        '''     Execute an online method and return the result
        ''' </summary>
        ''' <typeparam name="T">The type of the return value</typeparam>
        ''' <param name="name">The name of the method</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the method <see cref="name" /></param>
        ''' <param name="parameters">The parameters for the method to execute</param>
        ''' <returns>Return the result of the method</returns>
        Function ExecuteOnlineMethod(Of T)(name As String, obfuscationKey As Integer, ParamArray parameters As Object()) As T
            Return ExecuteOnlineMethodAsync(Of T)(name, obfuscationKey, parameters).Result
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <typeparam name="T">The type of the variable</typeparam>
        ''' <param name="name">The name of the variable</param>
        ''' <returns>Return the current value</returns>
        Async Function GetOnlineVariableAsync(Of T)(name As String) As Task(Of T)
            Await CheckJwt().ConfigureAwait(False)

            Dim variable = Await InternalGetOnlineVariableAsync(name, Nothing).ConfigureAwait(False)
            Dim variableType = variable.GetNetType()

            If Not variableType.Equals(GetType(T)) Then
                Dim message = $"The variable ""{name}"" is of type {variableType}, but the generic type {GetType(T)} was submitted."
#If ENFORCE_VARIABLE_TYPES Then
                Throw New ArgumentException(message)
#Else
                Debug.WriteLine(message)
#End If
            End If

            Return Deserialize(Of T)(variable.Value)
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <typeparam name="T">The type of the variable</typeparam>
        ''' <param name="name">The name of the variable</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        ''' <returns>Return the current value</returns>
        Async Function GetOnlineVariableAsync(Of T)(name As String, obfuscationKey As Integer) As Task(Of T)
            Await CheckJwt().ConfigureAwait(False)

            Dim variable = Await InternalGetOnlineVariableAsync(name, obfuscationKey).ConfigureAwait(False)
            Dim variableType = variable.GetNetType()

            If Not variableType.Equals(GetType(T)) Then
                Dim message = $"The variable ""{name}"" is of type {variableType}, but the generic type {GetType(T)} was submitted."
#If ENFORCE_VARIABLE_TYPES Then
                Throw New ArgumentException(message)
#Else
                Debug.WriteLine(message)
#End If
            End If

            Return Deserialize(Of T)(variable.Value)
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <param name="name">The name of the variable</param>
        ''' <returns>Return the current value</returns>
        Async Function GetOnlineVariableAsync(name As String) As Task(Of Object)
            Await CheckJwt().ConfigureAwait(False)

            Dim variable = Await InternalGetOnlineVariableAsync(name, Nothing).ConfigureAwait(False)
            Return Deserialize(variable.Value, variable.GetNetType())
        End Function

        ''' <summary>
        '''     Get the value of an online variable
        ''' </summary>
        ''' <param name="name">The name of the variable</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        ''' <returns>Return the current value</returns>
        Async Function GetOnlineVariableAsync(name As String, obfuscationKey As Integer) As Task(Of Object)
            Await CheckJwt().ConfigureAwait(False)

            Dim variable = Await InternalGetOnlineVariableAsync(name, obfuscationKey).ConfigureAwait(False)
            Return Deserialize(variable.Value, variable.GetNetType())
        End Function

        ''' <summary>
        '''     Execute an online method and return the result
        ''' </summary>
        ''' <typeparam name="T">The type of the return value</typeparam>
        ''' <param name="name">The name of the method</param>
        ''' <param name="parameters">The parameters for the method to execute</param>
        ''' <returns>Return the result of the method</returns>
        Async Function ExecuteOnlineMethodAsync(Of T)(name As String, ParamArray parameters As Object()) As Task(Of T)
            Await CheckJwt().ConfigureAwait(False)

            Dim returnValue = Await InternalExecuteOnlineMethodAsync(name, parameters, Nothing).ConfigureAwait(False)
            Return Deserialize(Of T)(returnValue.Value)
        End Function

        ''' <summary>
        '''     Execute an online method and return the result
        ''' </summary>
        ''' <typeparam name="T">The type of the return value</typeparam>
        ''' <param name="name">The name of the method</param>
        ''' <param name="obfuscationKey">The key which was used to obfuscate the method <see cref="name" /></param>
        ''' <param name="parameters">The parameters for the method to execute</param>
        ''' <returns>Return the result of the method</returns>
        Async Function ExecuteOnlineMethodAsync(Of T)(name As String, obfuscationKey As Integer, ParamArray parameters As Object()) As Task(Of T)
            Await CheckJwt().ConfigureAwait(False)

            Dim returnValue = Await InternalExecuteOnlineMethodAsync(name, parameters, obfuscationKey).ConfigureAwait(False)
            Return Deserialize(Of T)(returnValue.Value)
        End Function

        Private Async Function InternalGetOnlineVariableAsync(name As String, obfuscationKey As Integer?) As Task(Of OnlineVariableValue)
            Dim uri = New Uri(String.Format(_getVariableUri, name))
            If obfuscationKey IsNot Nothing Then uri = AddParameter(uri, "obfuscationKey", obfuscationKey.Value.ToString())

            Dim response = Await Client.GetAsync(uri).ConfigureAwait(False)
            If response.IsSuccessStatusCode Then Return Deserialize(Of OnlineVariableValue)(Await response.Content.ReadAsStringAsync().ConfigureAwait(False))

            Dim errors As RestError()
            Try
                DeserializeErrors(Await response.Content.ReadAsStringAsync().ConfigureAwait(False), errors)
            Catch ex As Exception
                errors = Nothing
            End Try

            If errors Is Nothing Then response.EnsureSuccessStatusCode()

            Dim restError = errors(0)
            Select Case restError.Code
                Case ErrorCode.LicenseSystem_Variables_NotFound
                    Throw New InvalidOperationException($"The variable ""{name}"" could not be found.")
                Case Else
                    Throw New InvalidOperationException(restError.Message)
            End Select
        End Function

        Private Async Function InternalExecuteOnlineMethodAsync(name As String, parameters As Object(), obfuscationKey As Integer?) As Task(Of OnlineVariableValue)
            Dim uri = New Uri(String.Format(_executeMethodUri, name))
            Dim queryParameters = New Dictionary(Of String, String)()

            If obfuscationKey IsNot Nothing Then queryParameters.Add("obfuscationKey", obfuscationKey.Value.ToString())

            If parameters?.Length > 0 Then
                For i = 0 To parameters.Length - 1
                    queryParameters.Add("arg" & i, Serialize(parameters(i)))
                Next
            End If

            Dim builder = New UriBuilder(uri) With {
                .Query = String.Join("&", queryParameters.[Select](Function(item) $"{item.Key}={Uri.EscapeDataString(item.Value)}"))
            }
            uri = builder.Uri

            Using response = Await Client.GetAsync(uri).ConfigureAwait(False)
                If response.IsSuccessStatusCode Then Return Deserialize(Of OnlineVariableValue)(Await response.Content.ReadAsStringAsync().ConfigureAwait(False))

                Dim errors As RestError()
                Try
                    DeserializeErrors(Await response.Content.ReadAsStringAsync().ConfigureAwait(False), errors)
                Catch ex As Exception
                    errors = Nothing
                End Try

                If errors Is Nothing Then response.EnsureSuccessStatusCode()

                Dim restError = errors(0)
                Select Case restError.Code
                    Case ErrorCode.LicenseSystem_Methods_NotFound
                        Throw New InvalidOperationException($"The method ""{name}"" could not be found.")
                    Case ErrorCode.LicenseSystem_Methods_ExecutionFailed
                        Throw New InvalidOperationException($"The execution of method ""{name}"" failed.")
                    Case Else
                        Throw New InvalidOperationException(restError.Message)
                End Select
            End Using
        End Function

#End If
#End Region

        Private Sub CheckInitialized()
            If Not _isInitialized Then Throw New InvalidOperationException("This method is only available after the initialization. Please call Initialize() first.")
        End Sub

        Private Sub CheckLicenseVerified()
            CheckInitialized()
            If Not _isLicenseVerified Then Throw New InvalidOperationException("This operation is only available after checking the license. Please call CheckComputer() first.")
        End Sub

#If NET20 Then
        Private Sub CheckJwt()
            CheckInitialized()

            If _currentToken Is Nothing OrElse DateTime.UtcNow.AddMinutes(1) > _currentToken.TokenExpirationDate Then
                SyncLock ConnectionLock
                    If _currentToken Is Nothing OrElse _currentToken.TokenExpirationDate > DateTime.UtcNow.AddMinutes(1) Then
                        Dim result = CheckComputer()
                        If result <> ComputerCheckResult.Valid Then Throw New LicenseCheckFailedException(result)
                    End If
                End SyncLock
            End If
        End Sub
#Else
        Private Async Function CheckJwt() As Task
            CheckInitialized()

            If _currentToken Is Nothing OrElse DateTime.UtcNow.AddMinutes(1) > _currentToken.TokenExpirationDate Then
                Await ConnectionSemaphore.WaitAsync().ConfigureAwait(False)

                Try
                    If _currentToken Is Nothing OrElse _currentToken.TokenExpirationDate > DateTime.UtcNow.AddMinutes(1) Then
                        Dim result = Await CheckComputer().ConfigureAwait(False)
                        If result <> ComputerCheckResult.Valid Then Throw New LicenseCheckFailedException(result)
                    End If
                Finally
                    ConnectionSemaphore.Release()
                End Try
            End If
        End Function
#End If

        Private Sub ApplyLicenseInformation(info As LicenseInformation)
            ExpirationDate = info.ExpirationDateUtc
            LicenseType = info.LicenseType
            CustomerName = info.CustomerName
            CustomerEmail = info.CustomerEmail

#If NET20 Then
            Client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + info.Jwt)
#Else
            Client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", info.Jwt)
#End If

            _currentToken = Deserialize(Of JwToken)(Encoding.UTF8.GetString(UrlBase64Decode(info.Jwt.Split("."c)(1))))
            _isLicenseVerified = True
        End Sub



#Region "Offline Verification"

        <Conditional("ALLOW_OFFLINE")>
        Private Sub TerminateOfflineLicense()
#If ALLOW_OFFLINE Then
            if (File.Exists(LicenseFilename))
                File.Delete(LicenseFilename)
#End If
        End Sub

#If ALLOW_OFFLINE Then
        Private Const ExpirationDateNullValue As String = "Never"

        Private Function CheckLicenseFile() As Boolean
            Dim licenseFile = New FileInfo(LicenseFilename)
            If licenseFile.Exists Then
                Dim licenseInfo = ParseLicenseFile(File.ReadAllText(licenseFile.FullName))
                Return licenseInfo.Verify(HardwareId)
            End If

            Return False
        End Function

#If NET20 Then
        Private Sub UpdateLicenseFile(info As LicenseInformation)
            Dim licenseFile = New FileInfo(LicenseFilename)

            If licenseFile.Exists Then
                Dim licenseInfo = ParseLicenseFile(File.ReadAllText(licenseFile.FullName))
                If info.Equals(licenseInfo) Then Return
            End If

            Dim result = Client.DownloadString(_activateLicenseUri)
            Dim offlineLicense = Deserialize(Of OfflineLicenseInformation)(result)
            WriteLicenseFile(offlineLicense)
        End Sub
#Else
        Private Async Function UpdateLicenseFile(info As LicenseInformation) As Task
            Dim licenseFile = New FileInfo(LicenseFilename)

            If licenseFile.Exists Then
                Dim licenseInfo = ParseLicenseFile(File.ReadAllText(licenseFile.FullName))
                If info.Equals(licenseInfo) Then Return
            End If

            Dim result = Await Client.GetAsync(_activateLicenseUri).ConfigureAwait(False)

            Dim responseString = Await result.Content.ReadAsStringAsync().ConfigureAwait(False)
            Dim errors As RestError()
            If Not result.IsSuccessStatusCode Then
                If DeserializeErrors(responseString, errors) Then Throw New HttpRequestException("An error occurred when trying to request the new license file.")
                Throw New InvalidOperationException($"Error occurred when updating license file: {errors(0).Message}")
            End If

            Dim offlineLicense = Deserialize(Of OfflineLicenseInformation)(responseString)
            WriteLicenseFile(offlineLicense)
        End Function
#End If

        Private Sub WriteLicenseFile(info As OfflineLicenseInformation)
            Using fileStream = New FileStream(LicenseFilename, FileMode.Create, FileAccess.Write)

                Using streamWriter = New StreamWriter(fileStream)
                    streamWriter.WriteLine("----------BEGIN LICENSE----------")
                    streamWriter.WriteLine($"Name: {info.CustomerName}")
                    streamWriter.WriteLine($"E-Mail: {info.CustomerEmail}")
                    streamWriter.WriteLine($"License Type: {CInt(info.LicenseType)}")
                    streamWriter.WriteLine($"Expiration: {If(info.ExpirationDateUtc?.ToString("O"), ExpirationDateNullValue)}")
                    Const chunkSize As Integer = 32
                    Dim signatureIndex = 0

                    While signatureIndex <> info.Signature.Length
                        Dim length = Math.Min(info.Signature.Length - signatureIndex, chunkSize)
                        streamWriter.WriteLine(info.Signature.Substring(signatureIndex, length))
                        signatureIndex += length
                    End While

                    streamWriter.WriteLine("-----------END LICENSE-----------")
                End Using
            End Using
        End Sub

        Private Function ParseLicenseFile(content As String) As OfflineLicenseInformation
            Dim options = RegexOptions.IgnoreCase
            Dim match = Regex.Match(content, "^\s*-+BEGIN LICENSE-+\s*(\r\n|\r|\n)\s*Name: (?<name>(.*?))(\r\n|\r|\n)\s*E-Mail: (?<email>(.*?))(\r\n|\r|\n)\s*License Type:\s*(?<type>(\d+?))(\r\n|\r|\n)\s*Expiration:\s*(?<expiration>(.*?))(\r\n|\r|\n)\s*(?<data>(\s|.)*?)-+END LICENSE-+\s*$", options)
            If Not match.Success Then Throw New FormatException("The license file could not be parsed.")
            Dim information = New OfflineLicenseInformation With {
                .LicenseType = CType(Integer.Parse(match.Groups("type").Value), LicenseTypes),
                .ExpirationDateUtc = If(match.Groups("expiration").Value.Equals(ExpirationDateNullValue, StringComparison.OrdinalIgnoreCase), CType(Nothing, DateTime?), DateTime.ParseExact(match.Groups("expiration").Value, "O", Nothing)),
                .CustomerName = match.Groups("name").Value,
                .CustomerEmail = match.Groups("email").Value
            }
            Dim rawStringData = match.Groups("data").Value
            If String.IsNullOrEmpty(rawStringData) Then Throw New FormatException("The license signature must not be empty.")
            Dim signatureBuilder = New StringBuilder(rawStringData.Length)

            For Each c In rawStringData
                If Char.IsLetterOrDigit(c) Then signatureBuilder.Append(c)
            Next

            information.Signature = signatureBuilder.ToString()
            Return information
        End Function

#End If
#End Region

#Region "Serialization"

        Private ReadOnly JsonSerializerSettings As JsonSerializerSettings = New JsonSerializerSettings With {
            .ContractResolver = New CamelCasePropertyNamesContractResolver()
        }

        Private Function Deserialize(Of T)(value As String) As T
            Return JsonConvert.DeserializeObject(Of T)(value, JsonSerializerSettings)
        End Function

        Private Function Deserialize(value As String, type As Type) As Object
            Return JsonConvert.DeserializeObject(value, type, JsonSerializerSettings)
        End Function

        Private Function Serialize(value As Object) As String
            Return JsonConvert.SerializeObject(value, JsonSerializerSettings)
        End Function

#End Region

#Region "Hardware ID"

#If NETSTANDARD Then
        Private Function GenerateHardwareId() As Byte()
            If (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Then
                Return GetWindowsHardwareId()
            End If

            Throw New NotImplementedException()
        End Function
#Else
        Private Function GenerateHardwareId() As Byte()
            Return GetWindowsHardwareId()
        End Function
#End If

        Private Function GetWindowsHardwareId() As Byte()
            Dim numberData = New Byte(3) {}

#If NETSTANDARD Then
            Dim sb = New StringBuilder()
            Dim len = GetSystemDirectory(sb, 260)
            Dim systemDirectory = sb.ToString(0, len)
#Else
            Dim systemDirectory = Environment.SystemDirectory
#End If

            Dim serialNumber As UInteger = 0, foo As UInteger, foo2 As UInteger

            If GetVolumeInformation(IO.Path.GetPathRoot(systemDirectory), Nothing, 255, serialNumber, foo, foo2, Nothing, 255) Then
                Buffer.BlockCopy(BitConverter.GetBytes(serialNumber), 0, numberData, 0, 4)
            Else
                Throw New InvalidOperationException($"The volume information could not be retrieved. Error: {Marshal.GetLastWin32Error()}")
            End If

            Using crypto = SHA256.Create()
                Return crypto.ComputeHash(numberData)
            End Using
        End Function

#If NETSTANDARD Then
        <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=False)>
        Private Function GetSystemDirectory(<Out> sb As StringBuilder, length As Integer) As Integer
        End Function
#End If
        <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
        Private Function GetVolumeInformation(rootPathName As String, volumeNameBuffer As StringBuilder, volumeNameSize As UInteger, <Out> ByRef volumeSerialNumber As UInteger, <Out> ByRef maximumComponentLength As UInteger, <Out> ByRef fileSystemFlags As UInteger, fileSystemNameBuffer As StringBuilder, nFileSystemNameSize As Integer) As Boolean
        End Function


#End Region

#Region "Operating System"

        Private Class OperatingSystemInfo
            Public Sub New(operatingSystem As OperatingSystemType, version As Version)
                operatingSystem = operatingSystem
                version = version
            End Sub

            Public ReadOnly Property OperatingSystem As OperatingSystemType
            Public ReadOnly Property Version As Version
        End Class

        Private Enum OperatingSystemType
            Windows
            WindowsServer
            Linux
            OSX
        End Enum

#If NETSTANDARD Then
        Private Function GetOperatingSystem() As OperatingSystemInfo
            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Dim description = RuntimeInformation.OSDescription
                Dim versionPart = description.Split({" "c}, StringSplitOptions.RemoveEmptyEntries).Last()
                Return New OperatingSystemInfo(OperatingSystemType.Windows, Version.Parse(versionPart))
            End If

            If RuntimeInformation.IsOSPlatform(OSPlatform.OSX) Then Return New OperatingSystemInfo(OperatingSystemType.OSX, Nothing)
            Return New OperatingSystemInfo(OperatingSystemType.Linux, Nothing)
        End Function
#Else
        Private Function GetOperatingSystem() As OperatingSystemInfo
            Dim osVersion = Environment.OSVersion

            Select Case osVersion.Platform
                Case PlatformID.Win32NT
                    'as suggested by Microsoft https:'msdn.microsoft.com/en-us/library/windows/desktop/ms724429(v=vs.85).aspx
                    'https:'stackoverflow.com/questions/25986331/how-to-determine-windows-version-in-future-proof-way
                    'version numbers: https:'stackoverflow.com/questions/2819934/detect-windows-version-in-net

                    Dim versionEx = New OSVERSIONINFOEX With {
                        .dwOSVersionInfoSize = CUInt(Marshal.SizeOf(GetType(OSVERSIONINFOEX)))
                    }
                    GetVersionEx(versionEx) 'if that fails, we just have a workstation
                    Dim isServer = versionEx.wProductType = ProductType.VER_NT_SERVER
                    Dim fileVersion = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory, "kernel32.dll"))

                    Return New OperatingSystemInfo(If(isServer, OperatingSystemType.WindowsServer, OperatingSystemType.Windows), New Version(fileVersion.ProductMajorPart, fileVersion.ProductMinorPart, fileVersion.ProductBuildPart, 0))
            End Select

            'that should not happen as we are on .Net 4.6 that should not run on Linux (expect using Mono, but the .Net Standard version would
            'be better then)
            'https:'stackoverflow.com/questions/5116977/how-to-check-the-os-version-at-runtime-e-g-windows-or-linux-without-using-a-con
            'int p = (int) Environment.OSVersion.Platform;
            'if (p == 4 || p == 6 || p == 128)
            Return New OperatingSystemInfo(OperatingSystemType.Linux, osVersion.Version)
        End Function

        <DllImport("kernel32")>
        Private Function GetVersionEx(ByRef osvi As OSVERSIONINFOEX) As Boolean
        End Function

        <StructLayout(LayoutKind.Sequential)>
        Private Structure OSVERSIONINFOEX
            Public dwOSVersionInfoSize As UInteger
            Private ReadOnly dwMajorVersion As UInteger
            Private ReadOnly dwMinorVersion As UInteger
            Private ReadOnly dwBuildNumber As UInteger
            Private ReadOnly dwPlatformId As UInteger
            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)>
            Private ReadOnly szCSDVersion As String
            Private ReadOnly wServicePackMajor As UShort
            Private ReadOnly wServicePackMinor As UShort
            Private ReadOnly wSuiteMask As UShort
            Public ReadOnly wProductType As ProductType
            Private ReadOnly wReserved As Byte
        End Structure

        Private Enum ProductType As Byte
            VER_NT_DOMAIN_CONTROLLER = &H0000002
            VER_NT_SERVER = &H0000003
            VER_NT_WORKSTATION = &H0000001
        End Enum
#End If

#End Region

#Region "Data Transfer Objects"

        Private Class LicenseInformation
            Public Property LicenseType As LicenseTypes
            Public Property ExpirationDateUtc As DateTime?
            Public Property CustomerName As String
            Public Property CustomerEmail As String
            Public Property Jwt As String

            Public Overloads Function Equals(other As LicenseInformation) As Boolean
                Return LicenseType = other.LicenseType AndAlso ExpirationDateUtc.Equals(other.ExpirationDateUtc) AndAlso String.Equals(CustomerName, other.CustomerName) AndAlso String.Equals(CustomerEmail, other.CustomerEmail)
            End Function
        End Class

#If ALLOW_OFFLINE
      Private Class OfflineLicenseInformation
        Inherits LicenseInformation

        Public Property Signature As String

        Public Function Verify(hardwareId As Byte()) As Boolean
            Dim dataString = New StringBuilder(46)
#If GET_CUSTOMER_INFORMATION Then
            AppendStringGeneralized(dataString, CustomerName)
            AppendStringGeneralized(dataString, CustomerEmail)
#End If
            dataString.Append(BitConverter.ToString(hardwareId))
            dataString.Append(CInt(LicenseType))

            If ExpirationDate IsNot Nothing Then dataString.Append(ExpirationDate.Value.ToString("O"))
            Dim dataBuffer = Encoding.UTF8.GetBytes(dataString.ToString())
            Dim signature = StringToByteArray(Signature)
#If NETSTANDARD Then
            Using provider = RSA.Create()
                provider.ImportParameters(_publicKey)
                Return provider.VerifyData(dataBuffer, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            End Using
#Else
            Using provider = New RSACryptoServiceProvider()
                provider.ImportParameters(_publicKey)
                provider.PersistKeyInCsp = False
                Return provider.VerifyData(dataBuffer, SHA256.Create(), signature)
            End Using
#End If
        End Function

#If GET_CUSTOMER_INFORMATION Then
            Private Shared Sub AppendStringGeneralized(stringBuilder As StringBuilder, data As String)
                If String.IsNullOrEmpty(data) Then Return

                For Each c In data
                    If Not Char.IsWhiteSpace(c) Then stringBuilder.Append(Char.ToUpperInvariant(c))
                Next
            End Sub
#End If
    End Class
#End If

        Private Class JwToken
            Private _tokenExpirationDate As DateTime?

            <JsonProperty("exp")>
            Private Property Exp As String

            Public ReadOnly Property TokenExpirationDate As DateTime
                Get
                    If (_tokenExpirationDate Is Nothing) Then
                        _tokenExpirationDate = New DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Long.Parse(Exp))
                    End If
                    Return _tokenExpirationDate.Value
                End Get
            End Property
        End Class

        Private Class OnlineVariableValue
            Private Shared ReadOnly _frameworkTypes As Dictionary(Of VariableType, Type) = New Dictionary(Of VariableType, Type) From {
                    {VariableType.SByte, GetType(SByte)},
                    {VariableType.Byte, GetType(Byte)},
                    {VariableType.Int16, GetType(Short)},
                    {VariableType.UInt16, GetType(UShort)},
                    {VariableType.Int32, GetType(Integer)},
                    {VariableType.UInt32, GetType(UInteger)},
                    {VariableType.Int64, GetType(Long)},
                    {VariableType.UInt64, GetType(ULong)},
                    {VariableType.Char, GetType(Char)},
                    {VariableType.Single, GetType(Single)},
                    {VariableType.Double, GetType(Double)},
                    {VariableType.Boolean, GetType(Boolean)},
                    {VariableType.String, GetType(String)}
            }

            Public Property Type As String
            Public Property Value As String

            Public Function GetNetType() As Type
                If String.IsNullOrEmpty(Type) Then Return GetType(Object) ' for null values

                'parse
                Dim isArray = False
                Dim isList = False

                If Type.EndsWith("[]") Then
                    isArray = True
                    Type = Type.Substring(0, Type.Length - 2)
                ElseIf Type.StartsWith("List<") Then
                    isList = True
                    Type = Type.Substring(5, Type.Length - 6)
                End If

                Dim variableType = CType([Enum].Parse(GetType(VariableType), Type, True), VariableType)

                'to type
                Dim baseType = _frameworkTypes(variableType)
                If isArray Then
                    baseType = baseType.MakeArrayType()
                ElseIf isList Then
                    baseType = GetType(List(Of)).MakeGenericType(baseType)
                End If

                Return baseType
            End Function
        End Class

        Private Enum VariableType As Byte
            [SByte]
            [Byte]
            Int16
            UInt16
            Int32
            UInt32
            Int64
            UInt64
            [Char]
            [Single]
            [Double]
            [Boolean]
            [String]
        End Enum

        Private Class RestError
            Public Property Type As String
            Public Property Message As String
            Public Property Code As ErrorCode
        End Class

        Private Enum ErrorCode
            LicenseSystem_NotFound = 2000
            LicenseSystem_Disabled = 2001
            LicenseSystem_Expired = 2003

            LicenseSystem_Licenses_NotFound = 3011

            LicenseSystem_Activations_InvalidHardwareId = 6000
            LicenseSystem_Activations_LicenseNotFound
            LicenseSystem_Activations_LicenseDeactivated
            LicenseSystem_Activations_LicenseExpired
            LicenseSystem_Activations_AddressLimitReached
            LicenseSystem_Activations_InvalidLicenseKeyFormat
            LicenseSystem_Activations_ActivationLimitReached

            LicenseSystem_Variables_NotFound = 7003

            LicenseSystem_Methods_NotFound = 8015
            LicenseSystem_Methods_ParameterMissing
            LicenseSystem_Methods_InvalidParameter
            LicenseSystem_Methods_ExecutionFailed
        End Enum

#End Region

#Region "Utilities"

        Private Function DeserializeErrors(value As String, <Out> ByRef errors As RestError()) As Boolean
            Try
                errors = Deserialize(Of RestError())(value)
                Return True
            Catch ex As JsonReaderException
                errors = Nothing
                Return False
            End Try
        End Function

        Private Function AddParameter(uri As Uri, paramName As String, paramValue As String) As Uri
            Dim uriBuilder = New UriBuilder(uri)

            If String.IsNullOrEmpty(uriBuilder.Query) Then
                uriBuilder.Query = $"{paramName}={Uri.EscapeDataString(paramValue)}"
            Else 'for some reasons, the uri builder adds a '?' before the value when setting the property
                uriBuilder.Query = uriBuilder.Query.Remove(0, 1) & $"&{paramName}={Uri.EscapeDataString(paramValue)}"
            End If

            Return uriBuilder.Uri
        End Function

        Private Function StringToByteArray(hex As String) As Byte()
            If hex.Length Mod 2 = 1 Then Throw New Exception("The binary key cannot have an odd number of digits")

            Dim arr = New Byte(hex.Length >> 1 - 1) {}
            For i = 0 To arr.Length - 1
                arr(i) = CByte(((GetHexVal(hex(i << 1)) << 4) + GetHexVal(hex((i << 1) + 1))))
            Next

            Return arr
        End Function

        Private Function GetHexVal(hex As Char) As Integer
            Return AscW(hex) - (If(AscW(hex) < 58, 48, (If(AscW(hex) < 97, 55, 87))))
        End Function

        'Source: https://github.com/neosmart/UrlBase64/blob/master/UrlBase64/UrlBase64.cs
        Private Function UrlBase64Decode(encoded As String) As Byte()
            Dim chars = New List(Of Char)(encoded.ToCharArray())
            Dim twoPads As Char() = {"="c, "="c}

            For i = 0 To chars.Count - 1
                If chars(i) = "_"c Then
                    chars(i) = "/"c
                ElseIf chars(i) = "-"c Then
                    chars(i) = "+"c
                End If
            Next

            Select Case encoded.Length Mod 4
                Case 2
                    chars.AddRange(twoPads)
                Case 3
                    chars.Add("="c)
            End Select

            Dim array = chars.ToArray()
            Return Convert.FromBase64CharArray(array, 0, array.Length)
        End Function

#End Region

#Region "Exceptions"

        ''' <summary>
        '''     The exception that is thrown when the license is checked and the response is negative
        ''' </summary>
        Public Class LicenseCheckFailedException
            Inherits Exception

            ''' <summary>
            '''     Initialize a new instance of <see cref="LicenseCheckFailedException"/>
            ''' </summary>
            ''' <param name="result">The result received from the server</param>
            Public Sub New(result As ComputerCheckResult)
                MyBase.New($"Checking the license failed because the server returned {result} instead of a confirmation.")
                result = result
            End Sub

            ''' <summary>
            '''     The result that specifys why the license check failed. Read the enum documentation for more information.
            ''' </summary>
            Public ReadOnly Property Result As ComputerCheckResult
        End Class

#End Region

#Region "Certificate Validation"

#If NETSTANDARD Then
        Private Function ServerCertificateValidationCallback(arg1 As HttpRequestMessage, arg2 As X509Certificate2, arg3 As X509Chain, arg4 As SslPolicyErrors) As Boolean
            Return ValidateCertificate(arg2, arg4)
        End Function
#Else
        Private Function ServerCertificateValidationCallback(sender As Object, certificate As X509Certificate, chain As X509Chain, sslpolicyerrors As SslPolicyErrors) As Boolean
            Return ValidateCertificate(New X509Certificate2(certificate), sslpolicyerrors)
        End Function
#End If

        Private Function ValidateCertificate(certificate As X509Certificate2, errors As SslPolicyErrors) As Boolean
            If errors = SslPolicyErrors.None Then Return True
            If (errors And SslPolicyErrors.RemoteCertificateNotAvailable) > 0 OrElse (errors And SslPolicyErrors.RemoteCertificateNameMismatch) > 0 Then Return False

            Return CertificateValidator.Validate(certificate)
        End Function

        Private Class CodeElementsCertificateValidator
            Private ReadOnly _authority As X509Certificate2

            Public Sub New()
                _authority = New X509Certificate2(Convert.FromBase64String(CertificateData))
            End Sub

            Public Function Validate(certificate As X509Certificate2) As Boolean
                Dim chain = New X509Chain()
                With chain.ChainPolicy
                    .RevocationMode = X509RevocationMode.NoCheck
                    .RevocationFlag = X509RevocationFlag.ExcludeRoot
                    .VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority
                    .VerificationTime = DateTime.Now
                    .UrlRetrievalTimeout = TimeSpan.Zero
                End With

                ' This part is very important. You're adding your known root here.
                ' It doesn't have to be in the computer store at all. Neither certificates do.
                chain.ChainPolicy.ExtraStore.Add(_authority)

                If Not chain.Build(certificate) Then Return False

#If NET20 Then
                Dim valid = False
                For Each chainElement In chain.ChainElements
                    Dim x509ChainElement = DirectCast(chainElement, X509ChainElement)
                    If (x509ChainElement.Certificate.Thumbprint = _authority.Thumbprint)
                        valid = True
                        Exit For
                    End If
                Next
#Else
                Dim valid = chain.ChainElements.Cast(Of X509ChainElement)().Any(Function(x) x.Certificate.Thumbprint = _authority.Thumbprint)
#End If
                If Not valid Then Return False
                Return True
            End Function

            Private Const CertificateData As String = "MIIDIzCCAgugAwIBAgIJALldXI2KvykGMA0GCSqGSIb3DQEBCwUAMBAxDjAMBgNV
BAMMBUNFLUNBMCAXDTE4MDcwNzIyNDgwM1oYDzIwNzIwNDI0MjI0ODAzWjAQMQ4w
DAYDVQQDDAVDRS1DQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBALM1
mx7GvwPRbuSFJeipnQfeRMYZZ3q0J80n5sBykYw2t7dbQdqirzdL0azTVlyJ+YZS
j+YIqWagBUZfLUoyUZOTgO/H+AKqkEHgp5o9rllgux4ygxDQadz1Sn9FmXOa3v9o
WdHgFdGL9vgpSLBzMWv0/mzmo/5pyy5zjIvrJ7OdfoOzCQgkOSFSyslpZ5qxa42T
OEqwbwdsaGG74tc0FouS7zzFtRjbVfctCjuZr+8kSkGoqOdNWYBKFQxqhCMr3l1n
3SS0gDLyPo4bhgEFOGsyDnUomZ+pZmwhq+i+4oFn/kzVtfj7JmtPqH3cTtLRZKMq
MIy9j3+xM/hjwfagoM0CAwEAAaN+MHwwHQYDVR0OBBYEFPF3t5AFU7yycrigl2+5
j6HcApJ+MEAGA1UdIwQ5MDeAFPF3t5AFU7yycrigl2+5j6HcApJ+oRSkEjAQMQ4w
DAYDVQQDDAVDRS1DQYIJALldXI2KvykGMAwGA1UdEwQFMAMBAf8wCwYDVR0PBAQD
AgEGMA0GCSqGSIb3DQEBCwUAA4IBAQBK13owoHk7GLdqQlqwrtZXM7oghW2UtpHX
gxhni19kE8e1U3IRnZNmKJsMoEIEPS7EQUBbT7luLPvzzaQF4RgHtW7/xdhTf+rO
ZArnkeveF2TcePfeR7ckM31n8gOmxjoHpymnCrgVX3XfViqpoFgQy/aD6wpAwb3Q
uHGxpiPlZzzNJaRd4XAMCtkvS4asL0eolNJPmK5ruD8dMFFkkFaci8/H7TNtmW56
5HeN5l8kJ6StZYSfNo2IPlzMR45wForMH0e59fbHfKTnMXxnfzLkr6f3Ea0rFUEf
/9FZzHnx/Qkr9JdaLc4f+MxbfnLfNgW4eUtJZaHhzbWPG0+3man5"
        End Class
#End Region
        ''' <summary>
        '''     The result of checking whether a machine is connected to a project
        ''' </summary>
        Public Enum ComputerCheckResult
            ''' <summary>
            '''     The machine is registered and the license is activated.
            ''' </summary>
            Valid

            ''' <summary>
            '''     The connection to the CodeElements server failed. Either the client (this) computer doesn't have internet access or
            '''     our servers are down (which is unlikely).
            ''' </summary>
            ConnectionFailed

            ''' <summary>
            '''     The project was disabled
            ''' </summary>
            ProjectDisabled = 100

            ''' <summary>
            '''     The project with the given project id was not found or doesn't have a license service connected.
            ''' </summary>
            ProjectNotFound = 101

            ''' <summary>
            '''     The project with the given project id does not have a license system set up
            ''' </summary>
            LicenseSystemNotFound = 2000

            ''' <summary>
            '''     The license system was disabled
            ''' </summary>
            LicenseSystemDisabled = 2001

            ''' <summary>
            '''     The project expired, the developer (you) let the service run out.
            ''' </summary>
            LicenseSystemExpired = 2003

            ''' <summary>
            '''     The license was not found.
            ''' </summary>
            LicenseNotFound = 6001

            ''' <summary>
            '''     The license is deactivated.
            ''' </summary>
            LicenseDeactivated = 6002

            ''' <summary>
            '''     The license did expire.
            ''' </summary>
            LicenseExpired = 6003

            ''' <summary>
            '''     The IP address limit was exhausted. Too many different ip addresses tried to access the license in the last 24
            '''     hours.
            ''' </summary>
            IpLimitExhausted = 6004
        End Enum

        ''' <summary>
        '''     The result of activating a computer
        ''' </summary>
        Public Enum ComputerActivationResult
            ''' <summary>
            '''     The computer was activated successfully
            ''' </summary>
            Valid

            ''' <summary>
            '''     The connection to the CodeElements server failed. Either the client (this) computer doesn't have internet access or
            '''     our servers are down (which is unlikely).
            ''' </summary>
            ConnectionFailed

            ''' <summary>
            '''     The project was disabled
            ''' </summary>
            ProjectDisabled = 100

            ''' <summary>
            '''     The project with the given project id was not found or doesn't have a license service connected.
            ''' </summary>
            ProjectNotFound = 101

            ''' <summary>
            '''     The project with the given project id does not have a license system set up
            ''' </summary>
            LicenseSystemNotFound = 2000

            ''' <summary>
            '''     The license system was disabled
            ''' </summary>
            LicenseSystemDisabled = 2001

            ''' <summary>
            '''     The project expired, the developer (you) let the service run out.
            ''' </summary>
            LicenseSystemExpired = 2003

            ''' <summary>
            '''     The license was not found.
            ''' </summary>
            LicenseNotFound = 3011

            ''' <summary>
            '''     The license is deactivated.
            ''' </summary>
            LicenseDeactivated = 6002

            ''' <summary>
            '''     The license did expire.
            ''' </summary>
            LicenseExpired = 6003

            ''' <summary>
            '''     The IP address limit was exhausted. Too many different ip addresses tried to access the license in the last 24
            '''     hours.
            ''' </summary>
            IpLimitExhausted = 6004

            ''' <summary>
            '''     Cannot activate this computer with the license because there are already the maximum amount of computers registered
            '''     to the license.
            ''' </summary>
            ActivationLimitExhausted = 6006
        End Enum

        ''' <summary>
        '''     The license types of your project. TODO This enum must be replaced by your definitions.
        ''' </summary>
        Public Enum LicenseTypes
            ReplaceMe
        End Enum

    End Module
End Namespace