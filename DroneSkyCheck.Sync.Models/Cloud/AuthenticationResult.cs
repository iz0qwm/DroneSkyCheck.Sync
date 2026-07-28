namespace DroneSkyCheck.Sync.Models.Cloud;

public sealed class AuthenticationResult
{
    private AuthenticationResult(
        bool isSuccess,
        CloudUser? user,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        User = user;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public CloudUser? User { get; }

    public string ErrorMessage { get; }

    public static AuthenticationResult Success(CloudUser user)
    {
        return new AuthenticationResult(true, user, string.Empty);
    }

    public static AuthenticationResult Failure(string errorMessage)
    {
        return new AuthenticationResult(false, null, errorMessage);
    }
}

