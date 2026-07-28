using DroneSkyCheck.Sync.Models.Cloud;

namespace DroneSkyCheck.Sync.Core.Cloud;

public sealed class FirebaseAuthenticationOptions
{
    private const string DefaultFirebaseApiKey =
        "AIzaSyBi0vbs3iPGMw3AWmtPaCh8RFZ-wwS4moc";

    public string FirebaseApiKey { get; init; } = DefaultFirebaseApiKey;

    public string DesktopLoginUrl { get; init; } =
        "https://droneskycheck-d0136.web.app/sync-login.html";

    public string DesktopRedirectUri { get; init; } =
        "droneskycheck-sync://auth/callback";

    public string RedeemDesktopAuthCodeUrl { get; init; } =
        "https://europe-west8-droneskycheck-d0136.cloudfunctions.net/redeemDesktopAuthCode";

    public string ProtocolScheme { get; init; } = "droneskycheck-sync";

    public string RefreshTokenCredentialTarget { get; init; } =
        "DroneSkyCheck.Sync.FirebaseRefreshToken";

    public string OperatorContextCredentialTarget { get; init; } =
        "DroneSkyCheck.Sync.OperatorContext";

    public string PendingStateCredentialTarget { get; init; } =
        "DroneSkyCheck.Sync.PendingAuthState";

    public string CompletedStateCredentialTarget { get; init; } =
        "DroneSkyCheck.Sync.CompletedAuthState";

    public TimeSpan BrowserLoginTimeout { get; init; } =
        TimeSpan.FromMinutes(5);

    public CloudAccountPlan AuthenticatedUserPlan { get; init; } =
        CloudAccountPlan.DscPlus;

    public static FirebaseAuthenticationOptions FromEnvironment()
    {
        return new FirebaseAuthenticationOptions
        {
            FirebaseApiKey =
                Environment.GetEnvironmentVariable("DSC_FIREBASE_API_KEY") ??
                DefaultFirebaseApiKey
        };
    }
}
