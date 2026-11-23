using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;

namespace RAC.Infrastructure;

/// <summary>
/// Isolated SQL policy proposal and review ledger. An approved policy is
/// documentation only, never order permission. No live execution integration.
/// </summary>
public sealed class ProductionRiskPolicyStore(IDbContextFactory<RacDbContext> factory)
{
    public static string CanonicalLimits(ProductionRiskLimits limits)
    {
        if (!limits.IsValid) throw new InvalidDataException("Invalid production policy.");
        return JsonSerializer.Serialize(new
        {
            limits.MaximumCapitalUsdt, limits.MaximumOrderUsdt,
            limits.MaximumDailyLossPercent, limits.MaximumDrawdownPercent,
            limits.MaximumOpenPositions, limits.MaximumOrdersPerHour,
            limits.MaximumOrdersPerDay,
            AllowedSymbols = limits.AllowedSymbols.Order(StringComparer.Ordinal).ToArray()
        });
    }

    public static string Sha256(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    public static void ValidateReview(string proposedBy, string reviewer,
        string role, string reason)
    {
        if (string.IsNullOrWhiteSpace(proposedBy) ||
            string.IsNullOrWhiteSpace(reviewer) ||
            proposedBy.Equals(reviewer, StringComparison.OrdinalIgnoreCase) ||
            role != "ADMIN" || string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length is < 20 or > 500)
            throw new InvalidOperationException("Separate administrator review required.");
    }

    public async Task<ProductionRiskPolicyRecord> ProposeAsync(
        ProductionRiskLimits limits, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new ArgumentException("Proposer required.", nameof(actor));
        var json = CanonicalLimits(limits);
        if (json.Length > 2000) throw new InvalidDataException("Policy too large.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var version = (await db.ProductionRiskPolicies
            .MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var proposal = new ProductionRiskPolicyRecord
        {
            Version = version, LimitsJson = json,
            LimitsSha256 = Sha256(json), ProposedBy = actor.Trim(),
            ProposedUtc = DateTimeOffset.UtcNow, Status = "PENDING_REVIEW"
        };
        db.ProductionRiskPolicies.Add(proposal);
        await db.SaveChangesAsync(ct);
        db.ProductionRiskPolicyAudits.Add(new ProductionRiskPolicyAudit
        {
            PolicyId = proposal.Id, Action = "PROPOSED", Actor = actor.Trim(),
            Reason = "Research-only risk policy submitted for independent review.",
            LimitsSha256 = proposal.LimitsSha256, CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return proposal;
    }

    public async Task<ProductionRiskPolicyRecord> ReviewAsync(
        long policyId, string reviewer, string role, string reason,
        bool approve, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var policy = await db.ProductionRiskPolicies.SingleAsync(x => x.Id == policyId, ct);
        ValidateReview(policy.ProposedBy, reviewer, role, reason);
        if (policy.Status != "PENDING_REVIEW" ||
            Sha256(policy.LimitsJson) != policy.LimitsSha256)
            throw new InvalidDataException("Policy already reviewed or modified.");
        policy.Status = approve ? "REVIEWED_NO_TRADING_PERMISSION" : "REJECTED";
        policy.ReviewedBy = reviewer.Trim();
        policy.ReviewedUtc = DateTimeOffset.UtcNow;
        policy.ReviewReason = reason.Trim();
        db.ProductionRiskPolicyAudits.Add(new ProductionRiskPolicyAudit
        {
            PolicyId = policy.Id, Action = approve ? "REVIEWED" : "REJECTED",
            Actor = reviewer.Trim(), Reason = reason.Trim(),
            LimitsSha256 = policy.LimitsSha256, CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return policy;
    }
}
