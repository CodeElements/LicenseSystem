using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

#if ALLOW_OFFLINE
using System.Text.RegularExpressions;
#endif

#if !NETSTANDARD
using System.Net;
#endif

#if NET20
using System.Net;
using System.Collections.Specialized;

#else
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

#endif

namespace CodeElements
{
    /// <summary>
    ///     The CodeElements license system that provides the abilities to activate/validate computers and access the online
    ///     service (variables & methods)
    /// </summary>
    [CodeElementsLicenseSystem(LicenseSystemVersion)]
    public static class LicenseSystem
    {
        private const string LicenseSystemVersion = "1.0";
        private static bool _isInitialized;
        private static bool _isLicenseVerified;
        private static string _hardwareIdString;
        private static JwToken _currentToken;

#if NET20
        private static readonly WebClient Client = new WebClient();
#else
        private static readonly HttpClient Client;
#endif

#if NET20
        private static readonly object ConnectionLock = new object();
#else
        private static readonly SemaphoreSlim ConnectionSemaphore = new SemaphoreSlim(1, 1);
#endif

        private static LicenseTypes _licenseType;
        private static DateTime? _expirationDate;
        private static string _customerName;
        private static string _customerEmail;

        private static readonly Uri LicenseSystemBaseUri = new Uri("https://service.codeelements.net:2313/");
        private static readonly Uri MethodExecutionBaseUri = new Uri("https://exec.codeelements.net:2313/");
        private static Uri _verifyLicenseUri;
        private static Uri _activateLicenseUri;
        private static string _getVariableUri;
        private static string _executeMethodUri;
        private static readonly CodeElementsCertificateValidator CertificateValidator = new CodeElementsCertificateValidator();

        private static Guid _projectId;
        private static string _licenseKeyFormat;

#if ALLOW_OFFLINE
        private static RSAParameters _publicKey;
        private const string LicenseFilename = "license.elements";
#endif

        static LicenseSystem()
        {
#if NETSTANDARD
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = ServerCertificateCustomValidationCallback
            };
            Client = new HttpClient(handler);
#else
            ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
#if !NET20
            Client = new HttpClient();
#endif //NET20
#endif //NETSTANDARD
        }

        /// <summary>
        ///     The type of the current license
        /// </summary>
        public static LicenseTypes LicenseType
        {
            get
            {
                CheckLicenseVerified();
                return _licenseType;
            }
            private set => _licenseType = value;
        }

        /// <summary>
        ///     The expiration date in the UTC time zone. If the property is null, the license does not have an expiration date
        /// </summary>
        public static DateTime? ExpirationDate
        {
            get
            {
                CheckLicenseVerified();
                return _expirationDate;
            }
            private set => _expirationDate = value;
        }

        /// <summary>
        ///     The current hardware id of the machine
        /// </summary>
        public static byte[] HardwareId { get; } = GenerateHardwareId();

        /// <summary>
        ///     The current hardware id of the machine formatted as string
        /// </summary>
        public static string HardwareIdString => _hardwareIdString ?? (_hardwareIdString =
                                                     BitConverter.ToString(HardwareId).Replace("-", null)
                                                         .ToLowerInvariant());

        /// <summary>
        ///     The customer name of the license
        /// </summary>
        public static string CustomerName
        {
            get
            {
                CheckLicenseVerified();
                return _customerName;
            }
            private set => _customerName = value;
        }

        /// <summary>
        ///     The customer E-Mail address of the license
        /// </summary>
        public static string CustomerEmail
        {
            get
            {
                CheckLicenseVerified();
                return _customerEmail;
            }
            private set => _customerEmail = value;
        }

        /// <summary>
        ///     Initialize the License System. This method must be called once at the start of your application
        /// </summary>
#if MANUAL_INITIALIZATION
        /// <param name="projectId">The guid of the license system project</param>
        /// <param name="licenseKeyFormat">The format of the license keys</param>
#if ALLOW_OFFLINE
        /// <param name="publicKey">The public key of the license system used to validate offline license files</param>
#endif
        /// <param name="version">The current version of your application</param>
#endif
        public static void Initialize(
#if MANUAL_INITIALIZATION
            Guid projectId, string licenseKeyFormat,
#if ALLOW_OFFLINE
            RSAParameters publicKey,
#endif
#endif
            string version = null
        )
        {
            if (_isInitialized)
                throw new InvalidOperationException("Initialize must only be called once.");

#if MANUAL_INITIALIZATION
            _projectId = projectId;
#if ALLOW_OFFLINE
            _publicKey = publicKey;
#endif
            _licenseKeyFormat = licenseKeyFormat;
#endif

            _verifyLicenseUri = new Uri(LicenseSystemBaseUri,
                $"v1/projects/{_projectId:N}/l/licenses/activations/verify?hwid={HardwareIdString}");
            _activateLicenseUri = new Uri(LicenseSystemBaseUri,
                $"v1/projects/{_projectId:N}/l/licenses/activations?hwid={HardwareIdString}&includeCustomer=true");
            _getVariableUri = new Uri(LicenseSystemBaseUri, $"v1/projects/{_projectId:N}/l/variables").AbsoluteUri +
                              "/{0}";
            _executeMethodUri = new Uri(MethodExecutionBaseUri, $"v1/{_projectId:N}").AbsoluteUri + "/{0}";

            var os = GetOperatingSystem();
            var lang = CultureInfo.CurrentUICulture;
            var userAgent = new StringBuilder()
                .AppendFormat("CodeElementsLicenseSystem/{0} ", LicenseSystemVersion)
                .AppendFormat("({0} {1}; {2}) ", os.OperatingSystem, os.Version.ToString(3), lang)
                .AppendFormat("app/{0} ", version ?? "0.0.0")
                .ToString();

#if NET20
            Client.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
#else
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
#endif

            _isInitialized = true;
        }

        private static readonly Dictionary<char, char[]> PlaceholderChars =
            new Dictionary<char, char[]>
            {
                {'*', "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray()}, //letters and numbers
                {'#', "1234567890".ToCharArray()}, //numbers
                {'&', "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray()}, //letters
                {'0', "0123456789ABCDEF".ToCharArray()} //hex chars
            };

        /// <summary>
        ///     Try to convert the given license key to the actual data using the format set in this class
        /// </summary>
        /// <param name="licenseKey">The license key that should be parsed</param>
        /// <param name="licenseData">The relevant license data</param>
        /// <returns>Return true if the conversion succeeded, else return false</returns>
        public static bool TryParseLicenseKey(string licenseKey, out string licenseData)
        {
#if NET20
            if (string.IsNullOrEmpty(licenseKey?.Trim()))
#else
            if (string.IsNullOrWhiteSpace(licenseKey))
#endif
            {
                licenseData = null;
                return false;
            }

            licenseData = licenseKey.ToUpperInvariant(); //unify
            var licenseDataIndex = 0;

            foreach (var c in _licenseKeyFormat)
            {
                if (licenseDataIndex >= licenseData.Length)
                    return false;

                var formatChar = c;
                if (PlaceholderChars.TryGetValue(formatChar, out var chars))
                {
                    if (Array.IndexOf(chars, licenseData[licenseDataIndex++]) == -1)
                        return false;

                    continue;
                }

                if (licenseData[licenseDataIndex] == char.ToUpperInvariant(formatChar))
                    licenseData = licenseData.Remove(licenseDataIndex, 1);
            }

            return true;
        }

#if NET20
        /// <summary>
        ///     Check if the current machine is activated and if the license is valid.
        /// </summary>
        /// <returns>Return a result which specifies the current state.</returns>
        public static ComputerCheckResult CheckComputer()
        {
            CheckInitialized();

            try
            {
                var response = Client.DownloadString(_verifyLicenseUri);
                var information = Deserialize<LicenseInformation>(response);
#if ALLOW_OFFLINE
                UpdateLicenseFile(information);
#endif
                return ComputerCheckResult.Valid;
            }
            catch (WebException e)
            {
                Stream responseStream;
                if ((responseStream = e.Response.GetResponseStream()) != null)
                    using (responseStream)
                    {
                        if (DeserializeErrors(new StreamReader(responseStream).ReadToEnd(), out var errors))
                        {
                            var error = errors[0];
                            switch (error.Code)
                            {
                                case ErrorCode.LicenseSystem_Activations_InvalidHardwareId:
                                    throw new InvalidOperationException(error.Message);
                            }

                            TerminateOfflineLicense();
                            return (ComputerCheckResult) error.Code;
                        }
                    }
            }
            catch (Exception)
            {
            }

#if ALLOW_OFFLINE
            if (CheckLicenseFile())
                return ComputerCheckResult.Valid;
#endif

            return ComputerCheckResult.ConnectionFailed;
        }

        /// <summary>
        ///     Activate the current machine using the license key
        /// </summary>
        /// <param name="licenseKey">The license key</param>
        /// <returns>Return a result which specifies whether the operation was successful or something went wrong.</returns>
        /// <exception cref="FormatException">
        ///     Thrown when the license key format does not match the format of the service. It is
        ///     recommended to check the format using <see cref="TryParseLicenseKey" /> before calling this method.
        /// </exception>
        public static ComputerActivationResult ActivateComputer(string licenseKey)
        {
            CheckInitialized();

            var requestUri = AddParameter(_activateLicenseUri, "key", licenseKey);

#if ALLOW_OFFLINE
            requestUri = AddParameter(requestUri, "getLicense", "true");
#endif
#if GET_CUSTOMER_INFORMATION
                requestUri = AddParameter(requestUri, "includeCustomerInfo", "true");
#endif

            try
            {
                var response =
                    Encoding.UTF8.GetString(Client.UploadValues(requestUri, "POST", new NameValueCollection()));
#if ALLOW_OFFLINE
                var information = Deserialize<OfflineLicenseInformation>(response);
                WriteLicenseFile(information);
#else
                    var information = Deserialize<LicenseInformation>(response);
#endif
                ApplyLicenseInformation(information);
                return ComputerActivationResult.Valid;
            }
            catch (WebException e)
            {
                Stream responseStream;
                if ((responseStream = e.Response.GetResponseStream()) != null)
                    using (responseStream)
                    {
                        if (DeserializeErrors(new StreamReader(responseStream).ReadToEnd(), out var errors))
                        {
                            foreach (var error in errors)
                                switch (error.Code)
                                {
                                    case ErrorCode.LicenseSystem_Activations_InvalidHardwareId:
                                        throw new InvalidOperationException(error.Message);
                                    case ErrorCode.LicenseSystem_Activations_InvalidLicenseKeyFormat:
                                        throw new FormatException("The format of the license key is invalid.");
                                }

                            TerminateOfflineLicense();
                            return (ComputerActivationResult) errors[0].Code;
                        }
                    }
            }

            return ComputerActivationResult.ConnectionFailed;
        }
#else
        /// <summary>
        ///     Check if the current machine is activated and if the license is valid.
        /// </summary>
        /// <returns>Return a result which specifies the current state.</returns>
        public static async Task<ComputerCheckResult> CheckComputer()
        {
            CheckInitialized();

            try
            {
                var response = await Client.GetAsync(_verifyLicenseUri).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var licenseInfo =
                        Deserialize<LicenseInformation>(
                            await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    ApplyLicenseInformation(licenseInfo);
#if ALLOW_OFFLINE
                    await UpdateLicenseFile(licenseInfo).ConfigureAwait(false);
#endif
                    return ComputerCheckResult.Valid;
                }

                //on deserialize error move to end of function
                var restError =
                    Deserialize<RestError[]>(await response.Content.ReadAsStringAsync().ConfigureAwait(false))[0];
                if (restError == null)
                    response.EnsureSuccessStatusCode();

                switch (restError.Code)
                {
                    case ErrorCode.LicenseSystem_Activations_InvalidHardwareId:
                        throw new InvalidOperationException(restError.Message);
                }

                TerminateOfflineLicense();
                return (ComputerCheckResult) restError.Code;
            }
            catch (HttpRequestException)
            {
            } //for connection errors
            catch (JsonException)
            {
            } //in case that the http server returns an error instead of the webservice

#if ALLOW_OFFLINE
            if (CheckLicenseFile())
                return ComputerCheckResult.Valid;
#endif

            return ComputerCheckResult.ConnectionFailed;
        }

        /// <summary>
        ///     Activate the current machine using the license key
        /// </summary>
        /// <param name="licenseKey">The license key</param>
        /// <returns>Return a result which specifies whether the operation was successful or something went wrong.</returns>
        /// <exception cref="FormatException">
        ///     Thrown when the license key format does not match the format of the service. It is
        ///     recommended to check the format using <see cref="TryParseLicenseKey" /> before calling this method.
        /// </exception>
        public static async Task<ComputerActivationResult> ActivateComputer(string licenseKey)
        {
            CheckInitialized();

            try
            {
                var requestUri = AddParameter(_activateLicenseUri, "key", licenseKey);

#if ALLOW_OFFLINE
                requestUri = AddParameter(requestUri, "getLicense", "true");
#endif
#if GET_CUSTOMER_INFORMATION
                requestUri = AddParameter(requestUri, "includeCustomerInfo", "true");
#endif
                using (var response = await Client.PostAsync(requestUri, null).ConfigureAwait(false))
                {
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
#if ALLOW_OFFLINE
                        var information = Deserialize<OfflineLicenseInformation>(responseString);
                        WriteLicenseFile(information);
#else
                        var information = Deserialize<LicenseInformation>(responseString);
#endif
                        ApplyLicenseInformation(information);
                        return ComputerActivationResult.Valid;
                    }

                    if (!DeserializeErrors(responseString, out var errors))
                        return ComputerActivationResult.ConnectionFailed;

                    foreach (var error in errors)
                        switch (error.Code)
                        {
                            case ErrorCode.LicenseSystem_Activations_InvalidHardwareId:
                                throw new InvalidOperationException(error.Message);
                            case ErrorCode.LicenseSystem_Activations_InvalidLicenseKeyFormat:
                                throw new FormatException("The format of the license key is invalid.");
                        }

                    TerminateOfflineLicense();
                    return (ComputerActivationResult)errors[0].Code;
                }
            }
            catch (HttpRequestException)
            {
            } //for connection errors
            catch (JsonException)
            {
            } //in case that the http server returns an error instead of the webservice

            return ComputerActivationResult.ConnectionFailed;
        }
#endif

        #region Access

#if NET20
        /// <summary>
        ///     Verify that the license system is initialized and the license valid. Throws an exception if the conditions are not
        ///     met.
        /// </summary>
        public static void VerifyAccess()
        {
            CheckInitialized();
            CheckJwt();
        }

        /// <summary>
        ///     Check if the license type of the current license is within the <see cref="licenseTypes" />. Throw an exception if
        ///     not.
        /// </summary>
        /// <param name="licenseTypes">The license types that are required</param>
        public static void Require(params LicenseTypes[] licenseTypes)
        {
            VerifyAccess();

            if (Array.IndexOf(licenseTypes, LicenseType) == -1)
                throw new UnauthorizedAccessException("Your license is not permitted to execute that operation.");
        }

        /// <summary>
        ///     Check whether the current license type is within the <see cref="licenseTypes" />. Return false if not.
        /// </summary>
        /// <param name="licenseTypes">The license types that should be checked against</param>
        /// <returns>Return true if the current license type is part of the <see cref="licenseTypes" />, return false if not.</returns>
        public static bool Check(params LicenseTypes[] licenseTypes)
        {
            VerifyAccess();
            return Array.IndexOf(licenseTypes, LicenseType) > -1;
        }
#else
        /// <summary>
        ///     Verify that the license system is initialized and the license valid. Throws an exception if the conditions are not
        ///     met.
        /// </summary>
        public static async Task VerifyAccess()
        {
            CheckInitialized();
            await CheckJwt().ConfigureAwait(false);
        }

        /// <summary>
        ///     Check if the license type of the current license is within the <see cref="licenseTypes" />. Throw an exception if
        ///     not.
        /// </summary>
        /// <param name="licenseTypes">The license types that are required</param>
        public static async Task Require(params LicenseTypes[] licenseTypes)
        {
            await VerifyAccess().ConfigureAwait(false);

            if (Array.IndexOf(licenseTypes, LicenseType) == -1)
                throw new UnauthorizedAccessException("Your license is not permitted to execute that operation.");
        }

        /// <summary>
        ///     Check whether the current license type is within the <see cref="licenseTypes" />. Return false if not.
        /// </summary>
        /// <param name="licenseTypes">The license types that should be checked against</param>
        /// <returns>Return true if the current license type is part of the <see cref="licenseTypes" />, return false if not.</returns>
        public static async Task<bool> Check(params LicenseTypes[] licenseTypes)
        {
            await VerifyAccess().ConfigureAwait(false);
            return Array.IndexOf(licenseTypes, LicenseType) > -1;
        }
#endif

        #endregion

        #region Online Service

#if NET20
        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable</param>
        /// <returns>Return the current value</returns>
        public static T GetOnlineVariable<T>(string name)
        {
            CheckJwt();

            //no type check
            var variable = InternalGetOnlineVariable(name, null);
            var variableType = variable.GetNetType();

            if (variableType != typeof(T))
            {
                var message =
                    $"The variable \"{name}\" is of type {variableType}, but the generic type {typeof(T)} was submitted.";
#if ENFORCE_VARIABLE_TYPES
                throw new ArgumentException(message);
#else
                Debug.Print(message);
#endif
            }

            return Deserialize<T>(variable.Value);
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        /// <returns>Return the current value</returns>
        public static T GetOnlineVariable<T>(string name, int obfuscationKey)
        {
            CheckJwt();

            //no type check
            var variable = InternalGetOnlineVariable(name, obfuscationKey);
            var variableType = variable.GetNetType();

            if (variableType != typeof(T))
            {
                var message =
                    $"The variable \"{name}\" is of type {variableType}, but the generic type {typeof(T)} was submitted.";
#if ENFORCE_VARIABLE_TYPES
                throw new ArgumentException(message);
#else
                Debug.Print(message);
#endif
            }

            return Deserialize<T>(variable.Value);
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <returns>Return the current value</returns>
        public static object GetOnlineVariable(string name)
        {
            CheckJwt();

            var variable = InternalGetOnlineVariable(name, null);
            return Deserialize(variable.Value, variable.GetNetType());
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        /// <returns>Return the current value</returns>
        public static object GetOnlineVariable(string name, int obfuscationKey)
        {
            CheckJwt();

            var variable = InternalGetOnlineVariable(name, obfuscationKey);
            return Deserialize(variable.Value, variable.GetNetType());
        }

        /// <summary>
        ///     Execute an online method and get return the result
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="name">The name of the method</param>
        /// <param name="parameters">The parameters for the method to execute</param>
        /// <returns>Return the result of the method</returns>
        public static T ExecuteOnlineMethod<T>(string name, params object[] parameters)
        {
            CheckJwt();

            var returnValue = InternalExecuteOnlineMethod(name, parameters, null);
            return Deserialize<T>(returnValue.Value);
        }

        /// <summary>
        ///     Execute an online method and get return the result
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="name">The name of the method</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the method <see cref="name" /></param>
        /// <param name="parameters">The parameters for the method to execute</param>
        /// <returns>Return the result of the method</returns>
        public static T ExecuteOnlineMethod<T>(string name, int obfuscationKey, params object[] parameters)
        {
            CheckJwt();

            var returnValue = InternalExecuteOnlineMethod(name, parameters, obfuscationKey);
            return Deserialize<T>(returnValue.Value);
        }

        private static OnlineVariableValue InternalGetOnlineVariable(string name, int? obfuscationKey)
        {
            var uri = new Uri(string.Format(_getVariableUri, name));
            if (obfuscationKey != null)
                uri = AddParameter(uri, "obfuscationKey", obfuscationKey.Value.ToString());

            try
            {
                var response = Client.DownloadString(uri);
                return Deserialize<OnlineVariableValue>(response);
            }
            catch (WebException e)
            {
                Stream responseStream;
                if ((responseStream = e.Response.GetResponseStream()) != null)
                    using (responseStream)
                    {
                        if (DeserializeErrors(new StreamReader(responseStream).ReadToEnd(), out var errors))
                        {
                            foreach (var error in errors)
                                switch (error.Code)
                                {
                                    case ErrorCode.LicenseSystem_Variables_NotFound:
                                        throw new InvalidOperationException(
                                            $"The variable \"{name}\" could not be found.");
                                }

                            throw new InvalidOperationException(errors[0].Message);
                        }
                    }

                throw e;
            }
        }

        private static OnlineVariableValue InternalExecuteOnlineMethod(string name, object[] parameters,
            int? obfuscationKey)
        {
            var uri = new Uri(string.Format(_executeMethodUri, name));
            var queryParameters = new List<string>();

            if (obfuscationKey != null)
                queryParameters.Add("obfuscationKey=" + Uri.EscapeDataString(obfuscationKey.Value.ToString()));

            if (parameters?.Length > 0)
                for (var i = 0; i < parameters.Length; i++)
                    queryParameters.Add("arg" + i + "=" + Uri.EscapeDataString(Serialize(parameters[i])));

            var builder = new UriBuilder(uri)
            {
                Query = string.Join("&", queryParameters.ToArray())
            };
            uri = builder.Uri;

            try
            {
                var response = Client.DownloadString(uri);
                return Deserialize<OnlineVariableValue>(response);
            }
            catch (WebException e)
            {
                Stream responseStream;
                if ((responseStream = e.Response.GetResponseStream()) != null)
                    using (responseStream)
                    {
                        if (DeserializeErrors(new StreamReader(responseStream).ReadToEnd(), out var errors))
                        {
                            foreach (var error in errors)
                                switch (error.Code)
                                {
                                    case ErrorCode.LicenseSystem_Methods_NotFound:
                                        throw new InvalidOperationException(
                                            $"The method \"{name}\" could not be found.");
                                    case ErrorCode.LicenseSystem_Methods_ExecutionFailed:
                                        throw new InvalidOperationException(
                                            $"The execution of method \"{name}\" failed.");
                                }

                            throw new InvalidOperationException(errors[0].Message);
                        }
                    }

                throw e;
            }
        }
#else
        /* Just a short statement about these synchronous methods to whoever reads that code:
         * Yes, it's not nice, but there are two things to consider:
         * 1) Because the methods won't take a long time, synchronous execution is sometimes required
         * 2) We are not using any libraries here. We are only using the HttpClient and some own methods, so
         *    we have full control. There exists a problem with calling Task.Result that causes a deadlock
         *    (you can read about it here: http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html),
         *    but one way to get around it is calling ConfigureAwait(false) on every call. If that is guranteed,
         *    there shouldn't be any problem. Because we have a nice overview over all async calls (because they are
         *    all in this one file) and because it's the easiest way, I implemented these synchronous wrappers.
         */

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable</param>
        /// <returns>Return the current value</returns>
        public static T GetOnlineVariable<T>(string name)
        {
            return GetOnlineVariableAsync<T>(name).Result;
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        /// <returns>Return the current value</returns>
        public static T GetOnlineVariable<T>(string name, int obfuscationKey)
        {
            return GetOnlineVariableAsync<T>(name, obfuscationKey).Result;
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <returns>Return the current value</returns>
        public static object GetOnlineVariable(string name)
        {
            return GetOnlineVariableAsync(name).Result;
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        /// <returns>Return the current value</returns>
        public static object GetOnlineVariable(string name, int obfuscationKey)
        {
            return GetOnlineVariableAsync(name, obfuscationKey).Result;
        }

        /// <summary>
        ///     Execute an online method and return the result
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="name">The name of the method</param>
        /// <param name="parameters">The parameters for the method to execute</param>
        /// <returns>Return the result of the method</returns>
        public static T ExecuteOnlineMethod<T>(string name, params object[] parameters)
        {
            return ExecuteOnlineMethodAsync<T>(name, parameters).Result;
        }

        /// <summary>
        ///     Execute an online method and return the result
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="name">The name of the method</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the method <see cref="name" /></param>
        /// <param name="parameters">The parameters for the method to execute</param>
        /// <returns>Return the result of the method</returns>
        public static T ExecuteOnlineMethod<T>(string name, int obfuscationKey, params object[] parameters)
        {
            return ExecuteOnlineMethodAsync<T>(name, obfuscationKey, parameters).Result;
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable</param>
        /// <returns>Return the current value</returns>
        public static async Task<T> GetOnlineVariableAsync<T>(string name)
        {
            await CheckJwt().ConfigureAwait(false);

            //no type check
            var variable = await InternalGetOnlineVariableAsync(name, null).ConfigureAwait(false);
            var variableType = variable.GetNetType();

            if (variableType != typeof(T))
            {
                var message =
                    $"The variable \"{name}\" is of type {variableType}, but the generic type {typeof(T)} was submitted.";
#if ENFORCE_VARIABLE_TYPES
                throw new ArgumentException(message);
#else
                Debug.WriteLine(message);
#endif
            }

            return Deserialize<T>(variable.Value);
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        /// <returns>Return the current value</returns>
        public static async Task<T> GetOnlineVariableAsync<T>(string name, int obfuscationKey)
        {
            await CheckJwt().ConfigureAwait(false);

            //no type check
            var variable = await InternalGetOnlineVariableAsync(name, obfuscationKey).ConfigureAwait(false);
            var variableType = variable.GetNetType();

            if (variableType != typeof(T))
            {
                var message =
                    $"The variable \"{name}\" is of type {variableType}, but the generic type {typeof(T)} was submitted.";
#if ENFORCE_VARIABLE_TYPES
                throw new ArgumentException(message);
#else
                Debug.WriteLine(message);
#endif
            }

            return Deserialize<T>(variable.Value);
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <returns>Return the current value</returns>
        public static async Task<object> GetOnlineVariableAsync(string name)
        {
            await CheckJwt().ConfigureAwait(false);

            var variable = await InternalGetOnlineVariableAsync(name, null).ConfigureAwait(false);
            return Deserialize(variable.Value, variable.GetNetType());
        }

        /// <summary>
        ///     Get the value of an online variable
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the variable <see cref="name" /></param>
        /// <returns>Return the current value</returns>
        public static async Task<object> GetOnlineVariableAsync(string name, int obfuscationKey)
        {
            await CheckJwt().ConfigureAwait(false);

            var variable = await InternalGetOnlineVariableAsync(name, obfuscationKey).ConfigureAwait(false);
            return Deserialize(variable.Value, variable.GetNetType());
        }

        /// <summary>
        ///     Execute an online method and return the result
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="name">The name of the method</param>
        /// <param name="parameters">The parameters for the method to execute</param>
        /// <returns>Return the result of the method</returns>
        public static async Task<T> ExecuteOnlineMethodAsync<T>(string name, params object[] parameters)
        {
            await CheckJwt().ConfigureAwait(false);

            var returnValue = await InternalExecuteOnlineMethodAsync(name, parameters, null).ConfigureAwait(false);
            return Deserialize<T>(returnValue.Value);
        }

        /// <summary>
        ///     Execute an online method and return the result
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="name">The name of the method</param>
        /// <param name="obfuscationKey">The key which was used to obfuscate the method <see cref="name" /></param>
        /// <param name="parameters">The parameters for the method to execute</param>
        /// <returns>Return the result of the method</returns>
        public static async Task<T> ExecuteOnlineMethodAsync<T>(string name, int obfuscationKey,
            params object[] parameters)
        {
            await CheckJwt().ConfigureAwait(false);

            var returnValue =
                await InternalExecuteOnlineMethodAsync(name, parameters, obfuscationKey).ConfigureAwait(false);
            return Deserialize<T>(returnValue.Value);
        }

        private static async Task<OnlineVariableValue> InternalGetOnlineVariableAsync(string name, int? obfuscationKey)
        {
            var uri = new Uri(string.Format(_getVariableUri, name));
            if (obfuscationKey != null)
                uri = AddParameter(uri, "obfuscationKey", obfuscationKey.Value.ToString());

            var response = await Client.GetAsync(uri).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return Deserialize<OnlineVariableValue>(
                    await response.Content.ReadAsStringAsync().ConfigureAwait(false));

            RestError restError;
            try
            {
                restError = Deserialize<RestError>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            catch (Exception)
            {
                restError = null;
            }
            if (restError == null)
                response.EnsureSuccessStatusCode();

            switch (restError.Code)
            {
                case ErrorCode.LicenseSystem_Variables_NotFound:
                    throw new InvalidOperationException($"The variable \"{name}\" could not be found.");
                default:
                    throw new InvalidOperationException(restError.Message);
            }
        }

        private static async Task<OnlineVariableValue> InternalExecuteOnlineMethodAsync(string name,
            object[] parameters, int? obfuscationKey)
        {
            var uri = new Uri(string.Format(_executeMethodUri, name));
            var queryParameters = new Dictionary<string, string>();

            if (obfuscationKey != null)
                queryParameters.Add("obfuscationKey", obfuscationKey.Value.ToString());

            if (parameters?.Length > 0)
                for (var i = 0; i < parameters.Length; i++)
                    queryParameters.Add("arg" + i, Serialize(parameters[i]));

            var builder = new UriBuilder(uri)
            {
                Query = string.Join("&",
                    queryParameters.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"))
            };
            uri = builder.Uri;

            using (var response = await Client.GetAsync(uri).ConfigureAwait(false))
            {
                if (response.IsSuccessStatusCode)
                    return Deserialize<OnlineVariableValue>(
                        await response.Content.ReadAsStringAsync().ConfigureAwait(false));

                RestError restError;
                try
                {
                    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    restError = Deserialize<RestError>(responseText);
                }
                catch (Exception)
                {
                    restError = null;
                }

                if (restError == null)
                    response.EnsureSuccessStatusCode();

                switch (restError.Code)
                {
                    case ErrorCode.LicenseSystem_Methods_NotFound:
                        throw new InvalidOperationException($"The method \"{name}\" could not be found.");
                    case ErrorCode.LicenseSystem_Methods_ExecutionFailed:
                        throw new InvalidOperationException($"The execution of method \"{name}\" failed.");
                    default:
                        throw new InvalidOperationException(restError.Message);
                }
            }
        }
#endif

        #endregion

        private static void CheckInitialized()
        {
            if (!_isInitialized)
                throw new InvalidOperationException(
                    "This method is only available after the initialization. Please call Initialize() first.");
        }

        private static void CheckLicenseVerified()
        {
            CheckInitialized();
            if (!_isLicenseVerified)
                throw new InvalidOperationException(
                    "This operation is only available after checking the license. Please call CheckComputer() first.");
        }

#if NET20
        private static void CheckJwt()
        {
            CheckInitialized();

            if (_currentToken == null || DateTime.UtcNow.AddMinutes(1) > _currentToken.TokenExpirationDate)
                lock (ConnectionLock)
                {
                    if (_currentToken == null || _currentToken.TokenExpirationDate > DateTime.UtcNow.AddMinutes(1))
                    {
                        var result = CheckComputer();
                        if (result != ComputerCheckResult.Valid)
                            throw new LicenseCheckFailedException(result);
                    }
                }
        }
#else
        private static async Task CheckJwt()
        {
            CheckInitialized();

            if (_currentToken == null || DateTime.UtcNow.AddMinutes(1) > _currentToken.TokenExpirationDate)
            {
                await ConnectionSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (_currentToken == null || _currentToken.TokenExpirationDate > DateTime.UtcNow.AddMinutes(1))
                    {
                        var result = await CheckComputer().ConfigureAwait(false);
                        if (result != ComputerCheckResult.Valid)
                            throw new LicenseCheckFailedException(result);
                    }
                }
                finally
                {
                    ConnectionSemaphore.Release();
                }
            }
        }
#endif

        private static void ApplyLicenseInformation(LicenseInformation info)
        {
            ExpirationDate = info.ExpirationDateUtc;
            LicenseType = info.LicenseType;
            CustomerName = info.CustomerName;
            CustomerEmail = info.CustomerEmail;

#if NET20
            Client.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + info.Jwt);
#else
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", info.Jwt);
#endif
            _currentToken = Deserialize<JwToken>(Encoding.UTF8.GetString(UrlBase64Decode(info.Jwt.Split('.')[1])));

            _isLicenseVerified = true;
        }

        #region Offline Verification

        [Conditional("ALLOW_OFFLINE")]
        private static void TerminateOfflineLicense()
        {
#if ALLOW_OFFLINE
            if (File.Exists(LicenseFilename))
                File.Delete(LicenseFilename);
#endif
        }

#if ALLOW_OFFLINE
        private const string ExpirationDateNullValue = "Never";

        private static bool CheckLicenseFile()
        {
            var licenseFile = new FileInfo(LicenseFilename);
            if (licenseFile.Exists)
            {
                var licenseInfo = ParseLicenseFile(File.ReadAllText(licenseFile.FullName));
                return licenseInfo.Verify(HardwareId);
            }

            return false;
        }

#if NET20
        private static void UpdateLicenseFile(LicenseInformation info)
        {
            var licenseFile = new FileInfo(LicenseFilename);
            if (licenseFile.Exists)
            {
                var licenseInfo = ParseLicenseFile(File.ReadAllText(licenseFile.FullName));
                if (info.Equals(licenseInfo))
                    return;
            }

            var result = Client.DownloadString(_activateLicenseUri);
            var offlineLicense = Deserialize<OfflineLicenseInformation>(result);
            WriteLicenseFile(offlineLicense);
        }
#else
        private static async Task UpdateLicenseFile(LicenseInformation info)
        {
            var licenseFile = new FileInfo(LicenseFilename);
            if (licenseFile.Exists)
            {
                var licenseInfo = ParseLicenseFile(File.ReadAllText(licenseFile.FullName));
                if (info.Equals(licenseInfo))
                    return;
            }

            var result = await Client.GetAsync(_activateLicenseUri).ConfigureAwait(false);
            var responseString = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!result.IsSuccessStatusCode)
            {
                if (DeserializeErrors(responseString, out var errors))
                    throw new HttpRequestException("An error occurred when trying to request the new license file.");

                throw new InvalidOperationException($"Error occurred when updating license file: {errors[0].Message}");
            }

            var offlineLicense = Deserialize<OfflineLicenseInformation>(responseString);
            WriteLicenseFile(offlineLicense);
        }
#endif

        private static void WriteLicenseFile(OfflineLicenseInformation info)
        {
            using (var fileStream = new FileStream(LicenseFilename, FileMode.Create, FileAccess.Write))
            using (var streamWriter = new StreamWriter(fileStream))
            {
                streamWriter.WriteLine("----------BEGIN LICENSE----------");
                streamWriter.WriteLine($"Name: {info.CustomerName}");
                streamWriter.WriteLine($"E-Mail: {info.CustomerEmail}");
                streamWriter.WriteLine($"License Type: {(int) info.LicenseType}");
                streamWriter.WriteLine(
                    $"Expiration: {info.ExpirationDateUtc?.ToString("O") ?? ExpirationDateNullValue}");

                const int chunkSize = 32;
                var signatureIndex = 0;

                while (signatureIndex != info.Signature.Length)
                {
                    var length = Math.Min(info.Signature.Length - signatureIndex, chunkSize);
                    streamWriter.WriteLine(info.Signature.Substring(signatureIndex, length));
                    signatureIndex += length;
                }
                streamWriter.WriteLine("-----------END LICENSE-----------");
            }
        }

        private static OfflineLicenseInformation ParseLicenseFile(string content)
        {
            var options = RegexOptions.IgnoreCase;
            var match = Regex.Match(content,
                @"^\s*-+BEGIN LICENSE-+\s*(\r\n|\r|\n)\s*Name: (?<name>(.*?))(\r\n|\r|\n)\s*E-Mail: (?<email>(.*?))(\r\n|\r|\n)\s*License Type:\s*(?<type>(\d+?))(\r\n|\r|\n)\s*Expiration:\s*(?<expiration>(.*?))(\r\n|\r|\n)\s*(?<data>(\s|.)*?)-+END LICENSE-+\s*$",
                options);

            if (!match.Success)
                throw new FormatException("The license file could not be parsed.");

            var information =
                new OfflineLicenseInformation
                {
                    LicenseType = (LicenseTypes) int.Parse(match.Groups["type"].Value),
                    ExpirationDateUtc = match.Groups["expiration"].Value
                        .Equals(ExpirationDateNullValue, StringComparison.OrdinalIgnoreCase)
                        ? (DateTime?) null
                        : DateTime.ParseExact(match.Groups["expiration"].Value, "O", null),
                    CustomerName = match.Groups["name"].Value,
                    CustomerEmail = match.Groups["email"].Value
                };

            var rawStringData = match.Groups["data"].Value;
            if (string.IsNullOrEmpty(rawStringData))
                throw new FormatException("The license signature must not be empty.");

            var signatureBuilder = new StringBuilder(rawStringData.Length);
            foreach (var c in rawStringData)
                if (char.IsLetterOrDigit(c))
                    signatureBuilder.Append(c);

            information.Signature = signatureBuilder.ToString();
            return information;
        }
#endif

        #endregion

        #region Serialization

        private static readonly JsonSerializerSettings JsonSerializerSettings =
            new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()};

        private static T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, JsonSerializerSettings);
        }

        private static object Deserialize(string value, Type type)
        {
            return JsonConvert.DeserializeObject(value, type, JsonSerializerSettings);
        }

        private static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, JsonSerializerSettings);
        }

        #endregion

        #region Hardware ID

#if NETSTANDARD
        private static byte[] GenerateHardwareId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsHardwareId();

            throw new NotImplementedException();
        }
#else
        private static byte[] GenerateHardwareId()
        {
            return GetWindowsHardwareId();
        }
#endif

        private static byte[] GetWindowsHardwareId()
        {
            var numberData = new byte[4];

#if NETSTANDARD
            var sb = new StringBuilder(260);
            var len = GetSystemDirectory(sb, 260);
            var systemDirectory = sb.ToString(0, len);
#else
            var systemDirectory = Environment.SystemDirectory;
#endif

            if (GetVolumeInformation(Path.GetPathRoot(systemDirectory), null, 255, out var serialNumber,
                out var _, out var _, null, 255))
                Buffer.BlockCopy(BitConverter.GetBytes(serialNumber), 0, numberData, 0, 4);
            else
                throw new InvalidOperationException(
                    $"The volume information could not be retrieved. Error: {Marshal.GetLastWin32Error()}");

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(numberData);
            }
        }

#if NETSTANDARD
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int GetSystemDirectory([Out] StringBuilder sb, int length);
#endif

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformation(string rootPathName, StringBuilder volumeNameBuffer,
            int volumeNameSize, out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer, int nFileSystemNameSize);

        #endregion

        #region Operating System

        private class OperatingSystemInfo
        {
            public OperatingSystemInfo(OperatingSystemType operatingSystem, Version version)
            {
                OperatingSystem = operatingSystem;
                Version = version;
            }

            public OperatingSystemType OperatingSystem { get; }
            public Version Version { get; }
        }

        private enum OperatingSystemType
        {
            Windows,
            WindowsServer,
            Linux,
            OSX
        }

#if NETSTANDARD
        private static OperatingSystemInfo GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var description = RuntimeInformation.OSDescription;
                var versionPart = description.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last();

                return new OperatingSystemInfo(OperatingSystemType.Windows, Version.Parse(versionPart));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new OperatingSystemInfo(OperatingSystemType.OSX, null);

            return new OperatingSystemInfo(OperatingSystemType.Linux, null);
        }
#else
        private static OperatingSystemInfo GetOperatingSystem()
        {
            var osVersion = Environment.OSVersion;
            switch (osVersion.Platform)
            {
                case PlatformID.Win32NT:
                    //as suggested by Microsoft https://msdn.microsoft.com/en-us/library/windows/desktop/ms724429(v=vs.85).aspx
                    //https://stackoverflow.com/questions/25986331/how-to-determine-windows-version-in-future-proof-way
                    //version numbers: https://stackoverflow.com/questions/2819934/detect-windows-version-in-net

                    var versionEx = new OSVERSIONINFOEX
                    {
                        dwOSVersionInfoSize = (uint) Marshal.SizeOf(typeof(OSVERSIONINFOEX))
                    };
                    GetVersionEx(ref versionEx); //if that fails, we just have a workstation
                    var isServer = versionEx.wProductType == ProductType.VER_NT_SERVER;

                    var fileVersion =
                        FileVersionInfo.GetVersionInfo(
                            Path.Combine(Environment.SystemDirectory, "kernel32.dll"));

                    return new OperatingSystemInfo(
                        isServer ? OperatingSystemType.WindowsServer : OperatingSystemType.Windows,
                        new Version(fileVersion.ProductMajorPart, fileVersion.ProductMinorPart,
                            fileVersion.ProductBuildPart, 0));
            }

            //that should not happen as we are on .Net 4.6 that should not run on Linux (expect using Mono, but the .Net Standard version would
            //be better then)
            //https://stackoverflow.com/questions/5116977/how-to-check-the-os-version-at-runtime-e-g-windows-or-linux-without-using-a-con
            //int p = (int) Environment.OSVersion.Platform;
            //if (p == 4 || p == 6 || p == 128)
            return new OperatingSystemInfo(OperatingSystemType.Linux, osVersion.Version);
        }

        [DllImport("kernel32")]
        private static extern bool GetVersionEx(ref OSVERSIONINFOEX osvi);

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX
        {
            public uint dwOSVersionInfoSize;
            private readonly uint dwMajorVersion;
            private readonly uint dwMinorVersion;
            private readonly uint dwBuildNumber;
            private readonly uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] private readonly string szCSDVersion;
            private readonly ushort wServicePackMajor;
            private readonly ushort wServicePackMinor;
            private readonly ushort wSuiteMask;
            public readonly ProductType wProductType;
            private readonly byte wReserved;
        }

        private enum ProductType : byte
        {
            VER_NT_DOMAIN_CONTROLLER = 0x0000002,
            VER_NT_SERVER = 0x0000003,
            VER_NT_WORKSTATION = 0x0000001
        }
#endif

        #endregion

        #region Data Transfer Objects

        private class LicenseInformation
        {
            public LicenseTypes LicenseType { get; set; }
            public DateTime? ExpirationDateUtc { get; set; }
            public string CustomerName { get; set; }
            public string CustomerEmail { get; set; }
            public string Jwt { get; set; }

            public bool Equals(LicenseInformation other)
            {
                return LicenseType == other.LicenseType && ExpirationDateUtc.Equals(other.ExpirationDateUtc) &&
                       string.Equals(CustomerName, other.CustomerName) &&
                       string.Equals(CustomerEmail, other.CustomerEmail);
            }
        }

#if ALLOW_OFFLINE
        private class OfflineLicenseInformation : LicenseInformation
        {
            public string Signature { get; set; }

            public bool Verify(byte[] hardwareId)
            {
                var dataString = new StringBuilder(46);
#if GET_CUSTOMER_INFORMATION
                AppendStringGeneralized(dataString, CustomerName);
                AppendStringGeneralized(dataString, CustomerEmail);
#endif
                dataString.Append(BitConverter.ToString(hardwareId));
                dataString.Append((int) LicenseType);

                if (ExpirationDate != null)
                    dataString.Append(ExpirationDate.Value.ToString("O"));

                var dataBuffer = Encoding.UTF8.GetBytes(dataString.ToString());
                var signature = StringToByteArray(Signature);

#if NETSTANDARD
                using (var provider = RSA.Create())
                {
                    provider.ImportParameters(_publicKey);
                    return provider.VerifyData(dataBuffer, signature, HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1);
                }
#else
                using (var provider = new RSACryptoServiceProvider())
                {
                    provider.ImportParameters(_publicKey);
                    provider.PersistKeyInCsp = false;
                    return provider.VerifyData(dataBuffer, SHA256.Create(), signature);
                }
#endif
            }

#if GET_CUSTOMER_INFORMATION
            private static void AppendStringGeneralized(StringBuilder stringBuilder, string data)
            {
                if (string.IsNullOrEmpty(data))
                    return;

                foreach (var c in data)
                    if (!char.IsWhiteSpace(c))
                        stringBuilder.Append(char.ToUpperInvariant(c));
            }
#endif
        }
#endif

        private class JwToken
        {
            private DateTime? _tokenExpirationDate;

            [JsonProperty("exp")]
            private string Exp { get; set; }

            public DateTime TokenExpirationDate => _tokenExpirationDate ?? (_tokenExpirationDate =
                                                       new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                                                           .AddSeconds(int.Parse(Exp))).Value;
        }

        private class OnlineVariableValue
        {
            private static readonly Dictionary<VariableType, Type> _frameworkTypes;

            static OnlineVariableValue()
            {
                _frameworkTypes = new Dictionary<VariableType, Type>
                {
                    {VariableType.SByte, typeof(sbyte)},
                    {VariableType.Byte, typeof(byte)},
                    {VariableType.Int16, typeof(short)},
                    {VariableType.UInt16, typeof(ushort)},
                    {VariableType.Int32, typeof(int)},
                    {VariableType.UInt32, typeof(uint)},
                    {VariableType.Int64, typeof(long)},
                    {VariableType.UInt64, typeof(ulong)},
                    {VariableType.Char, typeof(char)},
                    {VariableType.Single, typeof(float)},
                    {VariableType.Double, typeof(double)},
                    {VariableType.Boolean, typeof(bool)},
                    {VariableType.String, typeof(string)}
                };
            }

            public string Type { get; set; }
            public string Value { get; set; }

            public Type GetNetType()
            {
                if (string.IsNullOrEmpty(Type))
                    return typeof(object); //for null values

                //parse
                var isArray = false;
                var isList = false;

                if (Type.EndsWith("[]"))
                {
                    isArray = true;
                    Type = Type.Substring(0, Type.Length - 2);
                }
                else if (Type.StartsWith("List<"))
                {
                    isList = true;
                    Type = Type.Substring(5, Type.Length - 6); //trim last >
                }

                var variableType = (VariableType) Enum.Parse(typeof(VariableType), Type, true);

                //to type
                var baseType = _frameworkTypes[variableType];
                if (isArray)
                    baseType = baseType.MakeArrayType();
                else if (isList)
                    baseType = typeof(List<>).MakeGenericType(baseType);

                return baseType;
            }
        }

        private enum VariableType : byte
        {
            SByte,
            Byte,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Char,
            Single,
            Double,
            Boolean,
            String
        }

        private class RestError
        {
            public string Type { get; set; }
            public string Message { get; set; }
            public ErrorCode Code { get; set; }
        }

        private enum ErrorCode
        {
            LicenseSystem_NotFound = 2000,
            LicenseSystem_Disabled = 2001,
            LicenseSystem_Expired = 2003,

            LicenseSystem_Licenses_NotFound = 3011,

            LicenseSystem_Activations_InvalidHardwareId = 6000,
            LicenseSystem_Activations_LicenseNotFound,
            LicenseSystem_Activations_LicenseDeactivated,
            LicenseSystem_Activations_LicenseExpired,
            LicenseSystem_Activations_AddressLimitReached,
            LicenseSystem_Activations_InvalidLicenseKeyFormat,
            LicenseSystem_Activations_ActivationLimitReached,

            LicenseSystem_Variables_NotFound = 7003,

            LicenseSystem_Methods_NotFound = 8015,
            LicenseSystem_Methods_ParameterMissing,
            LicenseSystem_Methods_InvalidParameter,
            LicenseSystem_Methods_ExecutionFailed
        }

        #endregion

        #region Utilities

        private static bool DeserializeErrors(string value, out RestError[] errors)
        {
            try
            {
                errors = Deserialize<RestError[]>(value);
                return true;
            }
            catch (JsonReaderException)
            {
                errors = null;
                return false;
            }
        }

        private static Uri AddParameter(Uri uri, string paramName, string paramValue)
        {
            var uriBuilder = new UriBuilder(uri);
            if (string.IsNullOrEmpty(uriBuilder.Query))
                uriBuilder.Query = $"{paramName}={Uri.EscapeDataString(paramValue)}";
            else //for some reasons, the uri builder adds a '?' before the value when setting the property
                uriBuilder.Query = uriBuilder.Query.Remove(0, 1) + $"&{paramName}={Uri.EscapeDataString(paramValue)}";

            return uriBuilder.Uri;
        }

        //Source: https://stackoverflow.com/a/9995303/4166138
        private static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            var arr = new byte[hex.Length >> 1];
            for (var i = 0; i < hex.Length >> 1; ++i)
                arr[i] = (byte) ((GetHexVal(hex[i << 1]) << 4) + GetHexVal(hex[(i << 1) + 1]));

            return arr;
        }

        private static int GetHexVal(char hex)
        {
            return hex - (hex < 58 ? 48 : (hex < 97 ? 55 : 87));
        }

        //Source: https://github.com/neosmart/UrlBase64/blob/master/UrlBase64/UrlBase64.cs
        private static byte[] UrlBase64Decode(string encoded)
        {
            var chars = new List<char>(encoded.ToCharArray());
            char[] twoPads = {'=', '='};

            for (var i = 0; i < chars.Count; ++i)
                if (chars[i] == '_')
                    chars[i] = '/';
                else if (chars[i] == '-')
                    chars[i] = '+';

            switch (encoded.Length % 4)
            {
                case 2:
                    chars.AddRange(twoPads);
                    break;
                case 3:
                    chars.Add('=');
                    break;
            }

            var array = chars.ToArray();
            return Convert.FromBase64CharArray(array, 0, array.Length);
        }

        #endregion

        #region Exceptions

        /// <summary>
        ///     The exception that is thrown when the license is checked and the response is negative
        /// </summary>
        public class LicenseCheckFailedException : Exception
        {
            /// <summary>
            ///     Initialize a new instance of <see cref="LicenseCheckFailedException"/>
            /// </summary>
            /// <param name="result">The result received from the server</param>
            public LicenseCheckFailedException(ComputerCheckResult result) : base(
                $"Checking the license failed because the server returned {result} instead of a confirmation.")
            {
                Result = result;
            }

            /// <summary>
            ///     The result that specifys why the license check failed. Read the enum documentation for more information.
            /// </summary>
            public ComputerCheckResult Result { get; }
        }

        #endregion

        #region Certificate Validation

#if NETSTANDARD
        private static bool ServerCertificateCustomValidationCallback(HttpRequestMessage arg1, X509Certificate2 arg2,
            X509Chain arg3, SslPolicyErrors arg4)
        {
            return ValidateCertificate(arg2, arg4);
        }
#else
        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return ValidateCertificate(new X509Certificate2(certificate), sslpolicyerrors);
        }
#endif

        private static bool ValidateCertificate(X509Certificate2 certificate, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) > 0 ||
                (errors & SslPolicyErrors.RemoteCertificateNameMismatch) > 0)
                return false;

            return CertificateValidator.Validate(certificate);
        }

        private class CodeElementsCertificateValidator
        {
            private readonly X509Certificate2 _authority;

            public CodeElementsCertificateValidator()
            {
                _authority = new X509Certificate2(Convert.FromBase64String(CertificateData));
            }

            public bool Validate(X509Certificate2 certificate)
            {
                var chain = new X509Chain
                {
                    ChainPolicy =
                    {
                        RevocationMode = X509RevocationMode.NoCheck,
                        RevocationFlag = X509RevocationFlag.ExcludeRoot,
                        VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority,
                        VerificationTime = DateTime.Now,
                        UrlRetrievalTimeout = new TimeSpan(0, 0, 0)
                    }
                };

                // This part is very important. You're adding your known root here.
                // It doesn't have to be in the computer store at all. Neither certificates do.
                chain.ChainPolicy.ExtraStore.Add(_authority);

                if (!chain.Build(certificate))
                    return false;

#if NET20
                var valid = false;
                foreach (var chainElement in chain.ChainElements)
                {
                    var x509ChainElement = (X509ChainElement) chainElement;
                    if (x509ChainElement.Certificate.Thumbprint == _authority.Thumbprint)
                    {
                        valid = true;
                        break;
                    }
                }
#else
// This piece makes sure it actually matches your known root
                var valid = chain.ChainElements
                    .Cast<X509ChainElement>()
                    .Any(x => x.Certificate.Thumbprint == _authority.Thumbprint);
#endif


                if (!valid)
                    return false;

                return true;
            }

            private const string CertificateData = @"MIIDIzCCAgugAwIBAgIJALldXI2KvykGMA0GCSqGSIb3DQEBCwUAMBAxDjAMBgNV
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
/9FZzHnx/Qkr9JdaLc4f+MxbfnLfNgW4eUtJZaHhzbWPG0+3man5";
        }

        #endregion

        /// <summary>
        ///     The result of checking whether a machine is connected to a project
        /// </summary>
        public enum ComputerCheckResult
        {
            /// <summary>
            ///     The machine is registered and the license is activated.
            /// </summary>
            Valid,

            /// <summary>
            ///     The connection to the CodeElements server failed. Either the client (this) computer doesn't have internet access or
            ///     our servers are down (which is unlikely).
            /// </summary>
            ConnectionFailed,

            /// <summary>
            ///     The project was disabled
            /// </summary>
            ProjectDisabled = 100,

            /// <summary>
            ///     The project with the given project id was not found or doesn't have a license service connected.
            /// </summary>
            ProjectNotFound = 101,

            /// <summary>
            ///     The project with the given project id does not have a license system set up
            /// </summary>
            LicenseSystemNotFound = 2000,

            /// <summary>
            ///     The license system was disabled
            /// </summary>
            LicenseSystemDisabled = 2001,

            /// <summary>
            ///     The project expired, the developer (you) let the service run out.
            /// </summary>
            LicenseSystemExpired = 2003,

            /// <summary>
            ///     The license was not found.
            /// </summary>
            LicenseNotFound = 6001,

            /// <summary>
            ///     The license is deactivated.
            /// </summary>
            LicenseDeactivated = 6002,

            /// <summary>
            ///     The license did expire.
            /// </summary>
            LicenseExpired = 6003,

            /// <summary>
            ///     The IP address limit was exhausted. Too many different ip addresses tried to access the license in the last 24
            ///     hours.
            /// </summary>
            IpLimitExhausted = 6004
        }

        /// <summary>
        ///     The result of activating a computer
        /// </summary>
        public enum ComputerActivationResult
        {
            /// <summary>
            ///     The computer was activated successfully
            /// </summary>
            Valid,

            /// <summary>
            ///     The connection to the CodeElements server failed. Either the client (this) computer doesn't have internet access or
            ///     our servers are down (which is unlikely).
            /// </summary>
            ConnectionFailed,

            /// <summary>
            ///     The project was disabled
            /// </summary>
            ProjectDisabled = 100,

            /// <summary>
            ///     The project with the given project id was not found or doesn't have a license service connected.
            /// </summary>
            ProjectNotFound = 101,

            /// <summary>
            ///     The project with the given project id does not have a license system set up
            /// </summary>
            LicenseSystemNotFound = 2000,

            /// <summary>
            ///     The license system was disabled
            /// </summary>
            LicenseSystemDisabled = 2001,

            /// <summary>
            ///     The project expired, the developer (you) let the service run out.
            /// </summary>
            LicenseSystemExpired = 2003,

            /// <summary>
            ///     The license was not found.
            /// </summary>
            LicenseNotFound = 3011,

            /// <summary>
            ///     The license is deactivated.
            /// </summary>
            LicenseDeactivated = 6002,

            /// <summary>
            ///     The license did expire.
            /// </summary>
            LicenseExpired = 6003,

            /// <summary>
            ///     The IP address limit was exhausted. Too many different ip addresses tried to access the license in the last 24
            ///     hours.
            /// </summary>
            IpLimitExhausted = 6004,

            /// <summary>
            ///     Cannot activate this computer with the license because there are already the maximum amount of computers registered
            ///     to the license.
            /// </summary>
            ActivationLimitExhausted = 6006
        }

        /// <summary>
        ///     The license types of your project. TODO This enum must be replaced by your definitions.
        /// </summary>
        public enum LicenseTypes
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CodeElementsLicenseSystemAttribute : Attribute
    {
        // ReSharper disable once UnusedParameter.Local
        public CodeElementsLicenseSystemAttribute(string version)
        {
        }
    }
}