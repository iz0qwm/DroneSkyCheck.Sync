namespace DroneSkyCheck.Sync.Models.Cloud;

public sealed class CloudUser
{
    public string Uid { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PhotoUrl { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string OperatorId { get; init; } = string.Empty;

    public string OwnerUid { get; init; } = string.Empty;

    public CloudAccountPlan Plan { get; init; } = CloudAccountPlan.Free;
}
