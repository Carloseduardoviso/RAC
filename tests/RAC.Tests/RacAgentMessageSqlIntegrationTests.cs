using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class RacAgentMessageSqlIntegrationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options) : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task PersistsMessagesAndRejectsDuplicateIdentityConflicts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "RAC_AGENT_MESSAGE_TEST_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=" + name +
                ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using (var db = new RacDbContext(options))
                await db.Database.MigrateAsync();
            var store = new RacAgentMessageStore(new Factory(options));
            var correlation = Guid.NewGuid();
            var message = RacAgentCommunication.Create("rebeca","daniel",RacMessageKind.TaskRequest,
                "Synthetic research task",correlation,DateTimeOffset.UtcNow);
            Assert.True(await store.SaveAsync(message,default));
            Assert.False(await store.SaveAsync(message,default));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveAsync(message with { Content = "Conflicting content" },default));
            var reopened = new RacAgentMessageStore(new Factory(options));
            var row = Assert.Single(await reopened.ReadInboxAsync("daniel",correlation,10,default));
            Assert.Equal(message.Id,row.Id);
            Assert.Empty(await reopened.ReadInboxAsync("gabriel",correlation,10,default));
            Assert.Empty(await reopened.ReadInboxAsync("daniel",Guid.NewGuid(),10,default));
        }
        finally
        {
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
