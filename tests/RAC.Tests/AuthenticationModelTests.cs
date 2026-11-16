using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class AuthenticationModelTests
{
    [Fact]
    public void Password_is_stored_as_hash_and_verified_correctly()
    {
        var user = new RacUser
        {
            UserName = "testuser",
            NormalizedUserName = "TESTUSER",
            PasswordHash = "",
            Role = RacRoles.Administrator
        };
        var hasher = new PasswordHasher<RacUser>();
        user.PasswordHash = hasher.HashPassword(user, "a-strong-temporary-example-123!");
        Assert.DoesNotContain("a-strong-temporary-example", user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, user.PasswordHash, "a-strong-temporary-example-123!"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, user.PasswordHash, "incorrect-value"));
    }

    [Fact]
    public void Normalized_user_name_has_unique_index()
    {
        using var db = new RacDbContext(
            new DbContextOptionsBuilder<RacDbContext>()
                .UseSqlServer("Server=(local);Database=ModelInspectionOnly;Integrated Security=True;TrustServerCertificate=True")
                .Options);
        var entity = db.Model.FindEntityType(typeof(RacUser));
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(RacUser.NormalizedUserName));
    }
}
