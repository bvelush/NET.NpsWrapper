using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Omni2FA.Net.Utils;

namespace Omni2FA.AuthClient {
    public class Authenticator : IDisposable {

        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        private int _authTimeout = 60; // seconds
        private string _serviceUrl = "http://localhost:8000";
        private int _waitBeforePoll = 10; // seconds
        private int _pollInterval = 1; // seconds
        private int _pollMaxSeconds = 60; // seconds
        private bool _enableTraceLogging = false;
        private bool _ignoreSslErrors = false;
        private string _basicAuthUsername = "";
        private string _basicAuthPassword = "";

        private const string _regPath = @"SOFTWARE\Omni2FA.NPS";
        private const string _authTimeoutKey = "AuthTimeout";
        private const string _serviceUrlKey = "ServiceUrl";
        private const string _waitBeforePollKey = "WaitBeforePoll";
        private const string _pollIntervalKey = "PollInterval";
        private const string _pollMaxSecondsKey = "PollMaxSeconds";
        private const string _enableTraceLoggingKey = "EnableTraceLogging";
        private const string _ignoreSslErrorsKey = "IgnoreSslErrors";
        private const string _basicAuthUsernameKey = "BasicAuthUsername";
        private const string _basicAuthPasswordKey = "BasicAuthPassword";

        public enum AuthStatusEnum {
            AUTH_FAILED = -1,
            AUTH_PENDING = 0,
            AUTH_SUCCESS = 1
        }

        public class AuthResultResponse {
            public AuthStatusEnum status { get; set; }
            // TODO: message and details are not used currently
        }

        public Authenticator() {
            // Log component initialization with datetime and size
            var moduleInfo = Log.GetModuleInfo();
            Log.Event(Log.Level.Information, $"Initializing Omni2FA.AuthClient {moduleInfo}");
            
            using (var registry = new Registry(_regPath)) {
                // Read settings from registry
                _authTimeout        = registry.GetIntRegistryValue(_authTimeoutKey, 60);
                _serviceUrl         = registry.GetStringRegistryValue(_serviceUrlKey, "http://localhost:8000");
                _waitBeforePoll     = registry.GetIntRegistryValue(_waitBeforePollKey, 10);
                _pollInterval       = registry.GetIntRegistryValue(_pollIntervalKey, 1);
                _pollMaxSeconds     = registry.GetIntRegistryValue(_pollMaxSecondsKey, 60);
                _enableTraceLogging = registry.GetBoolRegistryValue(_enableTraceLoggingKey, false);
                _ignoreSslErrors    = registry.GetBoolRegistryValue(_ignoreSslErrorsKey, false);
                _basicAuthUsername  = registry.GetStringRegistryValue(_basicAuthUsernameKey, "");
                _basicAuthPassword  = registry.GetStringRegistryValue(_basicAuthPasswordKey, "");
            }
                 
            Log.SetTraceLoggingEnabled(_enableTraceLogging);

            // Create HttpClientHandler with SSL and auth configuration
            var handler = new HttpClientHandler();
            
            // Configure SSL certificate validation
            if (_ignoreSslErrors) {
                handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                    Log.Event(Log.Level.Warning, "SSL certificate validation bypassed due to IgnoreSslErrors setting");
                    return true; // Accept all certificates
                };
                Log.Event(Log.Level.Information, "SSL certificate validation disabled");
            }

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(_authTimeout);

            // Configure basic authentication if credentials are provided
            if (!string.IsNullOrEmpty(_basicAuthUsername) && !string.IsNullOrEmpty(_basicAuthPassword)) {
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_basicAuthUsername}:{_basicAuthPassword}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                Log.Event(Log.Level.Information, $"Basic authentication configured for user: {_basicAuthUsername}");
            }

            Log.Event(Log.Level.Information, $"Omni2FA.Auth initialized with service URL: {_serviceUrl}");
        }

        public async Task<bool> AuthenticateAsync(string samid) {
            try {
                // TODO: lets generate requestid here, send auth request, then poll for result
                //var requestId = Guid.NewGuid().ToString();
                var authRequestJson = JsonConvert.SerializeObject(new { samid = samid, requestor = "SMK-RDG" });
                Log.Event(Log.Level.Trace, $"Sending authentication request for user: {samid} to {_serviceUrl}/Authenticate");
                var authenticateResponse = await _httpClient.PostAsync(
                    $"{_serviceUrl}/Authenticate", 
                    new StringContent(authRequestJson, Encoding.UTF8, "application/json")
                );
                if (!authenticateResponse.IsSuccessStatusCode) {
                    var responseContent = await authenticateResponse.Content.ReadAsStringAsync();
                    Log.Event(Log.Level.Error, $"Service responded with status: {authenticateResponse.StatusCode}, content: {responseContent}");
                    return false;
                }
                var authenticateResponseJson = await authenticateResponse.Content.ReadAsStringAsync();
                Log.Event(Log.Level.Trace, $"Received authentication response for user: {samid}, response: {authenticateResponseJson}");
                var authenticateResponseObj = JsonConvert.DeserializeObject<AuthResultResponse>(authenticateResponseJson);
                Log.Event(Log.Level.Trace, $"Deserialized authentication response for user: {samid}, status: {authenticateResponseObj?.status}");
                if (authenticateResponseObj == null) {
                    Log.Event(Log.Level.Error, $"Invalid response from service for user: {samid}");
                    return false;
                }
                if (authenticateResponseObj.status < 0) { // early answer, no need of polling
                    Log.Event(Log.Level.Trace, $"Authentication failed for user: {samid} without polling, status: {authenticateResponseObj.status}");
                    return false;
                }
                if (authenticateResponseObj.status > 0) { // early success, no need of polling
                    Log.Event(Log.Level.Trace, $"Authentication succeeded for user: {samid} without polling, status: {authenticateResponseObj.status}");
                    return true;
                }

                await Task.Delay(_waitBeforePoll * 1000);
                for (int i = 0; i < _pollMaxSeconds; i++) {
                    try {
                        Log.Event(Log.Level.Trace, $"Polling AuthResult for user: {samid}, attempt: {i + 1}");
                        var authResultResponse = await _httpClient.PostAsync(
                            $"{_serviceUrl}/AuthResult",
                            new StringContent(authRequestJson, Encoding.UTF8, "application/json")
                        );
                        var authResultResponseContent = await authResultResponse.Content.ReadAsStringAsync();
                        if (!authResultResponse.IsSuccessStatusCode) {
                            Log.Event(Log.Level.Warning, $"AuthResult responded with status: {authResultResponse.StatusCode}, content: {authResultResponseContent}");
                            return false;
                        }
                        var authResultResponseJson = JsonConvert.DeserializeObject<AuthResultResponse>(authResultResponseContent);
                        Log.Event(Log.Level.Trace, $"Polled AuthResult for user: {samid}, response: {authResultResponseContent}");
                        if (authResultResponseJson == null) {
                            Log.Event(Log.Level.Error, $"Invalid AuthResult response for user {samid}");
                            return false;
                        }
                        if (authResultResponseJson.status > 0) { // auth success
                            Log.Event(Log.Level.Trace, $"Authentication succeeded for user: {samid}");
                            return true;
                        }
                        if (authResultResponseJson.status < 0) { // auth failure
                            Log.Event(Log.Level.Trace, $"Authentication failed for user: {samid}");
                            return false;
                        }
                        // result == 1 (pending), continue polling
                        await Task.Delay(_pollInterval * 1000);
                    }
                    catch (TaskCanceledException ex) {
                        Log.Event(Log.Level.Error, $"Timeout reached while polling AuthResult for user {samid}", ex);
                        return false;
                    }
                    catch (HttpRequestException ex) {
                        Log.Event(Log.Level.Error, $"MFA Service is unreachable while polling AuthResult for user {samid}", ex);
                        return false;
                    }
                    catch (Exception ex) {
                        Log.Event(Log.Level.Error, $"Error polling AuthResult for user {samid}", ex);
                        return false;
                    }
                    await Task.Delay(1000); // Wait 1 second before next poll
                }
                Log.Event(Log.Level.Error, $"Authentication result not received in time for user: {samid}");
                return false;
            }
            catch (TaskCanceledException ex) {
                Log.Event(Log.Level.Error, $"Timeout reached while authenticating user {samid}", ex);
                return false;
            }
            catch (HttpRequestException ex) {
                Log.Event(Log.Level.Error, $"MFA Service is unreachable while authenticating user {samid}", ex);
                return false;
            }
            catch (Exception ex) {
                Log.Event(Log.Level.Error, $"Error authenticating user {samid}", ex);
                return false;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        ~Authenticator() {
            Dispose(false);
        }
    }
}
