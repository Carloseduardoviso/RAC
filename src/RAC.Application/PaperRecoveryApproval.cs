namespace RAC.Application;

// Approval evidence validation only; no operational execution or token issuance.
public static class PaperRecoveryApproval
{
    public sealed record Evidence(string Reviewer,long PositionId,string Symbol,
        string AccountVersion,DateTimeOffset IssuedUtc,Guid ApprovalId);
    public static void Validate(Evidence evidence,string authenticatedReviewer,
        bool authorized,long positionId,string symbol,string accountVersion,DateTimeOffset now)
    {
        if(!authorized || string.IsNullOrWhiteSpace(authenticatedReviewer) ||
           !string.Equals(evidence.Reviewer,authenticatedReviewer,StringComparison.Ordinal) ||
           positionId<=0 || evidence.PositionId!=positionId ||
           string.IsNullOrWhiteSpace(symbol) || evidence.Symbol!=symbol ||
           string.IsNullOrWhiteSpace(accountVersion) || evidence.AccountVersion!=accountVersion ||
           evidence.ApprovalId==Guid.Empty || evidence.IssuedUtc>now ||
           now-evidence.IssuedUtc>TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("Invalid recovery approval evidence.");
    }
}
