using DroneSkyCheck.Sync.Models.Cloud;

namespace DroneSkyCheck.Sync.Core.Cloud;

public interface ICloudAuthenticationService
{
    bool IsAuthenticated { get; }

    Task<AuthenticationResult> Login(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginWithDscPortal(
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> HandleDscPortalCallback(
        Uri callbackUri,
        CancellationToken cancellationToken = default);

    void Logout();

    CloudUser? GetCurrentUser();

    Task<string?> GetIdTokenAsync(
        CancellationToken cancellationToken = default);
}
