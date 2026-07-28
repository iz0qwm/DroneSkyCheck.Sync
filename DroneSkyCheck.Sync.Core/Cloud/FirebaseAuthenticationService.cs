using DroneSkyCheck.Sync.Models.Cloud;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DroneSkyCheck.Sync.Core.Cloud;

public sealed class FirebaseAuthenticationService : ICloudAuthenticationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FirebaseAuthenticationOptions _options;
    private readonly HttpClient _httpClient;

    private CloudUser? _currentUser;
    private string _idToken = string.Empty;
    private string _refreshToken = string.Empty;
    private DateTimeOffset _idTokenExpiresAtUtc;

    public FirebaseAuthenticationService()
        : this(FirebaseAuthenticationOptions.FromEnvironment())
    {
    }

    public FirebaseAuthenticationService(
        FirebaseAuthenticationOptions options)
        : this(options, new HttpClient())
    {
    }

    public FirebaseAuthenticationService(
        FirebaseAuthenticationOptions options,
        HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
    }

    public bool IsAuthenticated =>
        _currentUser != null &&
        !string.IsNullOrWhiteSpace(_refreshToken);

    public async Task<AuthenticationResult> Login(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return AuthenticationResult.Failure("Inserisci l'email.");

        if (string.IsNullOrWhiteSpace(password))
            return AuthenticationResult.Failure("Inserisci la password.");

        var request = new EmailPasswordLoginRequest(
            email,
            password,
            true);

        return await SignInWithFirebase(
            "accounts:signInWithPassword",
            request,
            "password",
            true,
            null,
            cancellationToken);
    }

    public async Task<AuthenticationResult> LoginWithDscPortal(
        CancellationToken cancellationToken = default)
    {
        try
        {
            WindowsProtocolRegistration.Register(_options.ProtocolScheme);

            var state = CreateUrlSafeRandomString(32);

            WindowsCredentialStore.Write(
                _options.PendingStateCredentialTarget,
                state);

            WindowsCredentialStore.Delete(
                _options.CompletedStateCredentialTarget);

            var loginUrl = BuildDesktopLoginUrl(state);

            using var process = Process.Start(
                new ProcessStartInfo(loginUrl)
                {
                    UseShellExecute = true
                });

            var expiresAt =
                DateTimeOffset.UtcNow.Add(_options.BrowserLoginTimeout);

            while (DateTimeOffset.UtcNow < expiresAt)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var completedState =
                    WindowsCredentialStore.Read(
                        _options.CompletedStateCredentialTarget);

                if (string.Equals(
                    completedState,
                    state,
                    StringComparison.Ordinal))
                {
                    WindowsCredentialStore.Delete(
                        _options.PendingStateCredentialTarget);

                    WindowsCredentialStore.Delete(
                        _options.CompletedStateCredentialTarget);

                    var restoreResult =
                        await RestoreSessionFromCredential(
                            cancellationToken);

                    return restoreResult
                        ? AuthenticationResult.Success(_currentUser!)
                        : AuthenticationResult.Failure(
                            "Login completato, ma non e stato possibile ripristinare la sessione.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    cancellationToken);
            }

            return AuthenticationResult.Failure(
                "Login non completato nel tempo previsto.");
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failure("Login annullato.");
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failure(
                $"Login web DSC non riuscito: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> HandleDscPortalCallback(
        Uri callbackUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(
                callbackUri.Scheme,
                _options.ProtocolScheme,
                StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticationResult.Failure(
                    "Callback DSC non valida.");
            }

            var queryValues =
                ParseQueryString(callbackUri.Query);

            if (!queryValues.TryGetValue("code", out var code) ||
                string.IsNullOrWhiteSpace(code))
            {
                return AuthenticationResult.Failure(
                    "La callback DSC non contiene il codice di autorizzazione.");
            }

            if (!queryValues.TryGetValue("state", out var receivedState) ||
                string.IsNullOrWhiteSpace(receivedState))
            {
                return AuthenticationResult.Failure(
                    "La callback DSC non contiene lo state.");
            }

            var expectedState =
                WindowsCredentialStore.Read(
                    _options.PendingStateCredentialTarget);

            if (!string.Equals(
                expectedState,
                receivedState,
                StringComparison.Ordinal))
            {
                return AuthenticationResult.Failure(
                    "Lo state della callback DSC non coincide.");
            }

            var redeemResponse =
                await RedeemDesktopAuthCode(
                    code,
                    receivedState,
                    cancellationToken);

            var operatorContext =
                redeemResponse.GetOperatorContext();

            var result =
                await SignInWithFirebase(
                    "accounts:signInWithCustomToken",
                    new CustomTokenLoginRequest(
                        redeemResponse.CustomToken,
                        true),
                    "dsc-portal",
                    true,
                    operatorContext,
                    cancellationToken);

            if (result.IsSuccess)
            {
                WindowsCredentialStore.Write(
                    _options.CompletedStateCredentialTarget,
                    receivedState);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failure(
                "Callback DSC annullata.");
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failure(
                $"Callback DSC non riuscita: {ex.Message}");
        }
    }

    public void Logout()
    {
        _currentUser = null;
        _idToken = string.Empty;
        _refreshToken = string.Empty;
        _idTokenExpiresAtUtc = default;

        WindowsCredentialStore.Delete(
            _options.RefreshTokenCredentialTarget);

        WindowsCredentialStore.Delete(
            _options.OperatorContextCredentialTarget);
    }

    public CloudUser? GetCurrentUser()
    {
        return _currentUser;
    }

    public async Task<string?> GetIdTokenAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            var restored =
                await RestoreSessionFromCredential(cancellationToken);

            if (!restored)
                return null;
        }

        if (DateTimeOffset.UtcNow < _idTokenExpiresAtUtc.AddMinutes(-2))
            return _idToken;

        var refreshResult =
            await RefreshIdToken(cancellationToken);

        return refreshResult
            ? _idToken
            : null;
    }

    private async Task<bool> RestoreSessionFromCredential(
        CancellationToken cancellationToken)
    {
        var storedRefreshToken =
            WindowsCredentialStore.Read(
                _options.RefreshTokenCredentialTarget);

        if (string.IsNullOrWhiteSpace(storedRefreshToken))
            return false;

        _refreshToken = storedRefreshToken;

        return await RefreshIdToken(cancellationToken);
    }

    private async Task<RedeemDesktopAuthCodeResponse> RedeemDesktopAuthCode(
        string code,
        string state,
        CancellationToken cancellationToken)
    {
        using var response =
            await _httpClient.PostAsJsonAsync(
                _options.RedeemDesktopAuthCodeUrl,
                new RedeemDesktopAuthCodeRequest
                {
                    Code = code,
                    State = state,
                    RedirectUri = _options.DesktopRedirectUri
                },
                JsonOptions,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                GetFirebaseErrorMessage(responseBody));
        }

        var redeemResponse =
            JsonSerializer.Deserialize<RedeemDesktopAuthCodeResponse>(
                responseBody,
                JsonOptions);

        if (redeemResponse == null ||
            string.IsNullOrWhiteSpace(redeemResponse.CustomToken))
        {
            throw new InvalidOperationException(
                "La Cloud Function DSC non ha restituito un customToken valido.");
        }

        if (!redeemResponse.GetOperatorContext().HasRequiredValues)
        {
            throw new InvalidOperationException(
                "La Cloud Function DSC non ha restituito operatorId e ownerUid validi.");
        }

        return redeemResponse;
    }

    private async Task<AuthenticationResult> SignInWithFirebase<TRequest>(
        string firebaseMethod,
        TRequest request,
        string providerId,
        bool persistRefreshToken,
        DscOperatorContext? operatorContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"https://identitytoolkit.googleapis.com/v1/{firebaseMethod}?key={Uri.EscapeDataString(_options.FirebaseApiKey)}";

            using var response =
                await _httpClient.PostAsJsonAsync(
                    url,
                    request,
                    JsonOptions,
                    cancellationToken);

            var responseBody =
                await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return AuthenticationResult.Failure(
                    GetFirebaseErrorMessage(responseBody));
            }

            var firebaseResponse =
                JsonSerializer.Deserialize<FirebaseSignInResponse>(
                    responseBody,
                    JsonOptions);

            if (firebaseResponse == null ||
                string.IsNullOrWhiteSpace(firebaseResponse.IdToken) ||
                string.IsNullOrWhiteSpace(firebaseResponse.RefreshToken))
            {
                return AuthenticationResult.Failure(
                    "Risposta Firebase non valida.");
            }

            await SetSession(
                firebaseResponse,
                providerId,
                persistRefreshToken,
                operatorContext,
                cancellationToken);

            return AuthenticationResult.Success(_currentUser!);
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failure("Login annullato.");
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failure(
                $"Login non riuscito: {ex.Message}");
        }
    }

    private async Task<bool> RefreshIdToken(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
            return false;

        var url =
            $"https://securetoken.googleapis.com/v1/token?key={Uri.EscapeDataString(_options.FirebaseApiKey)}";

        var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _refreshToken
            });

        using var response =
            await _httpClient.PostAsync(
                url,
                content,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Logout();
            return false;
        }

        var responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        var refreshResponse =
            JsonSerializer.Deserialize<FirebaseRefreshResponse>(
                responseBody,
                JsonOptions);

        if (refreshResponse == null ||
            string.IsNullOrWhiteSpace(refreshResponse.IdToken) ||
            string.IsNullOrWhiteSpace(refreshResponse.RefreshToken))
        {
            Logout();
            return false;
        }

        _idToken = refreshResponse.IdToken;
        _refreshToken = refreshResponse.RefreshToken;
        _idTokenExpiresAtUtc =
            DateTimeOffset.UtcNow.AddSeconds(
                ParseExpiresIn(refreshResponse.ExpiresIn));

        WindowsCredentialStore.Write(
            _options.RefreshTokenCredentialTarget,
            _refreshToken);

        var storedOperatorContext =
            ReadStoredOperatorContext();

        await LoadUserFromFirebase(
            refreshResponse.UserId,
            "firebase",
            storedOperatorContext,
            cancellationToken);

        return true;
    }

    private async Task SetSession(
        FirebaseSignInResponse firebaseResponse,
        string providerId,
        bool persistRefreshToken,
        DscOperatorContext? operatorContext,
        CancellationToken cancellationToken)
    {
        _idToken = firebaseResponse.IdToken;
        _refreshToken = firebaseResponse.RefreshToken;
        _idTokenExpiresAtUtc =
            DateTimeOffset.UtcNow.AddSeconds(
                ParseExpiresIn(firebaseResponse.ExpiresIn));

        if (persistRefreshToken)
        {
            WindowsCredentialStore.Write(
                _options.RefreshTokenCredentialTarget,
                _refreshToken);

            if (operatorContext?.HasRequiredValues == true)
            {
                WindowsCredentialStore.Write(
                    _options.OperatorContextCredentialTarget,
                    JsonSerializer.Serialize(
                        operatorContext,
                        JsonOptions));
            }
            else
            {
                WindowsCredentialStore.Delete(
                    _options.OperatorContextCredentialTarget);
            }
        }

        await LoadUserFromFirebase(
            firebaseResponse.LocalId,
            providerId,
            operatorContext,
            cancellationToken);
    }

    private async Task LoadUserFromFirebase(
        string fallbackUid,
        string providerId,
        DscOperatorContext? operatorContext,
        CancellationToken cancellationToken)
    {
        var resolvedOperatorContext =
            operatorContext?.HasRequiredValues == true
                ? operatorContext
                : null;

        try
        {
            var url =
                $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={Uri.EscapeDataString(_options.FirebaseApiKey)}";

            using var response =
                await _httpClient.PostAsJsonAsync(
                    url,
                    new FirebaseLookupRequest(_idToken),
                    JsonOptions,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                SetFallbackUser(
                    fallbackUid,
                    providerId,
                    resolvedOperatorContext);

                return;
            }

            var responseBody =
                await response.Content.ReadAsStringAsync(cancellationToken);

            var lookupResponse =
                JsonSerializer.Deserialize<FirebaseLookupResponse>(
                    responseBody,
                    JsonOptions);

            var user =
                lookupResponse?.Users.FirstOrDefault();

            if (user == null)
            {
                SetFallbackUser(
                    fallbackUid,
                    providerId,
                    resolvedOperatorContext);

                return;
            }

            resolvedOperatorContext ??=
                GetOperatorContext(user.CustomAttributes);

            _currentUser = new CloudUser
            {
                Uid = user.LocalId,
                Email = user.Email,
                DisplayName = GetDisplayName(
                    user.DisplayName,
                    user.Email,
                    user.LocalId),
                PhotoUrl = user.PhotoUrl,
                ProviderId = providerId,
                OperatorId = resolvedOperatorContext?.OperatorId ?? string.Empty,
                OwnerUid = resolvedOperatorContext?.OwnerUid ?? string.Empty,
                Plan = _options.AuthenticatedUserPlan
            };
        }
        catch
        {
            SetFallbackUser(
                fallbackUid,
                providerId,
                resolvedOperatorContext);
        }
    }

    private void SetFallbackUser(
        string uid,
        string providerId,
        DscOperatorContext? operatorContext)
    {
        _currentUser = new CloudUser
        {
            Uid = uid,
            DisplayName = "Account DSC",
            ProviderId = providerId,
            OperatorId = operatorContext?.OperatorId ?? string.Empty,
            OwnerUid = operatorContext?.OwnerUid ?? string.Empty,
            Plan = _options.AuthenticatedUserPlan
        };
    }

    private DscOperatorContext? ReadStoredOperatorContext()
    {
        var storedOperatorContext =
            WindowsCredentialStore.Read(
                _options.OperatorContextCredentialTarget);

        if (string.IsNullOrWhiteSpace(storedOperatorContext))
            return null;

        try
        {
            var operatorContext =
                JsonSerializer.Deserialize<DscOperatorContext>(
                    storedOperatorContext,
                    JsonOptions);

            return operatorContext?.HasRequiredValues == true
                ? operatorContext
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DscOperatorContext? GetOperatorContext(
        string customAttributes)
    {
        if (string.IsNullOrWhiteSpace(customAttributes))
            return null;

        try
        {
            using var document =
                JsonDocument.Parse(customAttributes);

            var operatorId =
                GetStringProperty(
                    document.RootElement,
                    "operatorId");

            var ownerUid =
                GetStringProperty(
                    document.RootElement,
                    "ownerUid");

            if (string.IsNullOrWhiteSpace(operatorId) ||
                string.IsNullOrWhiteSpace(ownerUid))
            {
                return null;
            }

            return new DscOperatorContext
            {
                OperatorId = operatorId,
                OwnerUid = ownerUid
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetStringProperty(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private string BuildDesktopLoginUrl(string state)
    {
        var queryValues = new Dictionary<string, string>
        {
            ["state"] = state,
            ["redirect_uri"] = _options.DesktopRedirectUri
        };

        return
            _options.DesktopLoginUrl +
            "?" +
            BuildQueryString(queryValues);
    }

    private static string GetDisplayName(
        string displayName,
        string email,
        string uid)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;

        if (!string.IsNullOrWhiteSpace(email))
            return email;

        return uid;
    }

    private static long ParseExpiresIn(string value)
    {
        return long.TryParse(value, out var seconds)
            ? seconds
            : 3600;
    }

    private static string GetFirebaseErrorMessage(string responseBody)
    {
        try
        {
            var errorResponse =
                JsonSerializer.Deserialize<FirebaseErrorResponse>(
                    responseBody,
                    JsonOptions);

            return errorResponse?.Error?.Message switch
            {
                "EMAIL_NOT_FOUND" => "Account non trovato.",
                "INVALID_PASSWORD" => "Password non valida.",
                "USER_DISABLED" => "Account disabilitato.",
                "INVALID_LOGIN_CREDENTIALS" => "Credenziali non valide.",
                string message when !string.IsNullOrWhiteSpace(message) => message,
                _ => "Richiesta Firebase non riuscita."
            };
        }
        catch
        {
            return string.IsNullOrWhiteSpace(responseBody)
                ? "Richiesta non riuscita."
                : responseBody;
        }
    }

    private static string CreateUrlSafeRandomString(int bytesLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(bytesLength);

        return Convert
            .ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildQueryString(
        IReadOnlyDictionary<string, string> queryValues)
    {
        return string.Join(
            "&",
            queryValues.Select(x =>
                $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts[1]));
    }

    private sealed record EmailPasswordLoginRequest(
        string Email,
        string Password,
        bool ReturnSecureToken);

    private sealed record CustomTokenLoginRequest(
        string Token,
        bool ReturnSecureToken);

    private sealed class RedeemDesktopAuthCodeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; init; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; init; } = string.Empty;

        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; init; } = string.Empty;
    }

    private sealed class RedeemDesktopAuthCodeResponse
    {
        [JsonPropertyName("customToken")]
        public string CustomToken { get; init; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; init; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("operatorId")]
        public string OperatorId { get; init; } = string.Empty;

        [JsonPropertyName("ownerUid")]
        public string OwnerUid { get; init; } = string.Empty;

        [JsonPropertyName("operator")]
        public RedeemDesktopAuthOperatorResponse? Operator { get; init; }

        public DscOperatorContext GetOperatorContext()
        {
            var operatorId =
                !string.IsNullOrWhiteSpace(OperatorId)
                    ? OperatorId
                    : Operator?.OperatorId ?? string.Empty;

            var ownerUid =
                !string.IsNullOrWhiteSpace(OwnerUid)
                    ? OwnerUid
                    : Operator?.OwnerUid ?? string.Empty;

            return new DscOperatorContext
            {
                OperatorId = operatorId,
                OwnerUid = ownerUid
            };
        }
    }

    private sealed class RedeemDesktopAuthOperatorResponse
    {
        [JsonPropertyName("operatorId")]
        public string OperatorId { get; init; } = string.Empty;

        [JsonPropertyName("ownerUid")]
        public string OwnerUid { get; init; } = string.Empty;
    }

    private sealed class FirebaseSignInResponse
    {
        [JsonPropertyName("localId")]
        public string LocalId { get; init; } = string.Empty;

        [JsonPropertyName("idToken")]
        public string IdToken { get; init; } = string.Empty;

        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expiresIn")]
        public string ExpiresIn { get; init; } = string.Empty;
    }

    private sealed class FirebaseRefreshResponse
    {
        [JsonPropertyName("id_token")]
        public string IdToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public string ExpiresIn { get; init; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; init; } = string.Empty;
    }

    private sealed record FirebaseLookupRequest(
        string IdToken);

    private sealed class FirebaseLookupResponse
    {
        [JsonPropertyName("users")]
        public IReadOnlyList<FirebaseLookupUser> Users { get; init; } =
            Array.Empty<FirebaseLookupUser>();
    }

    private sealed class FirebaseLookupUser
    {
        [JsonPropertyName("localId")]
        public string LocalId { get; init; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; init; } = string.Empty;

        [JsonPropertyName("photoUrl")]
        public string PhotoUrl { get; init; } = string.Empty;

        [JsonPropertyName("customAttributes")]
        public string CustomAttributes { get; init; } = string.Empty;
    }

    private sealed class FirebaseErrorResponse
    {
        [JsonPropertyName("error")]
        public FirebaseError? Error { get; init; }
    }

    private sealed class FirebaseError
    {
        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;
    }

    private sealed class DscOperatorContext
    {
        [JsonPropertyName("operatorId")]
        public string OperatorId { get; init; } = string.Empty;

        [JsonPropertyName("ownerUid")]
        public string OwnerUid { get; init; } = string.Empty;

        [JsonIgnore]
        public bool HasRequiredValues =>
            !string.IsNullOrWhiteSpace(OperatorId) &&
            !string.IsNullOrWhiteSpace(OwnerUid);
    }
}
