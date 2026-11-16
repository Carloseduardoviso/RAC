namespace RAC.Domain;

public static class RacRoles
{
    public const string Administrator = "Administrator";
    public const string Operator = "Operator";
    public const string RiskReviewer = "RiskReviewer";
    public const string Viewer = "Viewer";
}

public sealed class RacUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserName { get; set; }
    public required string NormalizedUserName { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public bool Enabled { get; set; } = true;
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
