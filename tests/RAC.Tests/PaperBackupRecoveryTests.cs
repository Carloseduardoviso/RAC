using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RAC.Api.Security;
using RAC.Domain;
using RAC.Infrastructure;
using RAC.Application;
namespace RAC.Tests;
public class PaperBackupRecoveryTests
{
    [Fact]
    public async Task RestoreAndReconcile()
    {
        var backup=Environment.GetEnvironmentVariable("RAC_REHEARSAL_BACKUP");
        if(string.IsNullOrEmpty(backup))return;
        var name="RAC_RECOVERY_TEST_"+Guid.NewGuid().ToString("N");
        var master=@"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True";
        await using var cn=new SqlConnection(master);
        await cn.OpenAsync();
        string Q(string s)=>"N'"+s.Replace("'","''")+"'";
        async Task Exec(string sql){await using var cmd=new SqlCommand(sql,cn);cmd.CommandTimeout=120;await cmd.ExecuteNonQueryAsync();}
        var moves=new List<string>();
        var dir=Path.Combine(Path.GetTempPath(),name);
        Directory.CreateDirectory(dir);
        try
        {
            await using(var cmd=new SqlCommand("RESTORE FILELISTONLY FROM DISK="+Q(backup),cn))
            await using(var r=await cmd.ExecuteReaderAsync())
                while(await r.ReadAsync())
                    moves.Add("MOVE "+Q(r.GetString(0))+" TO "+Q(Path.Combine(dir,moves.Count+".db")));
            await Exec("RESTORE DATABASE ["+name+"] FROM DISK="+Q(backup)+" WITH CHECKSUM, "+string.Join(",",moves));
            await Exec("DBCC CHECKDB (["+name+"]) WITH NO_INFOMSGS, ALL_ERRORMSGS");
            var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(master.Replace("Database=master","Database="+name)).Options;
            await using(var db=new RacDbContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE rac.RecoveryApprovalUses (ApprovalId uniqueidentifier PRIMARY KEY, Reviewer nvarchar(100) NOT NULL, PositionId bigint NOT NULL, ConsumedUtc datetimeoffset NOT NULL)");
                var account=await db.PaperAccounts.AsNoTracking().SingleAsync();
                var pos=await db.PaperPositions.AsNoTracking().SingleAsync();
                Assert.Equal("DOTUSDT",pos.Symbol);
                Assert.True(account.RiskLocked);Assert.False(account.Running);
                Assert.Equal(17,await db.PaperFills.CountAsync());
                var audits=await db.AuditEvents.CountAsync();
                var services=new ServiceCollection();services.AddLogging();services.AddRacAuthentication(true);
                using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
                var tokens=new PaperRecoveryTokenService(scope.ServiceProvider.GetRequiredService<IAuthorizationService>(),new EphemeralDataProtectionProvider());
                var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,"backup-rehearsal"),new Claim(ClaimTypes.Role,RacRoles.RiskReviewer)},"test"));
                var now=DateTimeOffset.UtcNow;
                var expected=PaperStopRecovery.Calculate(false,true,new(pos.Id,pos.Symbol,pos.Quantity,pos.EntryPriceBrl,pos.EntryFeeBrl,pos.StopPriceBrl),account.CashBrl,.648m,5.8654m,now,now);
                var token=await tokens.IssueAsync(principal,pos.Id,pos.Symbol,Convert.ToBase64String(account.RowVersion),now);
                await new PaperRecoveryTokenRehearsal(tokens).ApplyAsync(db,token,principal,pos.Id,.648m,5.8654m,now);
                db.ChangeTracker.Clear();
                var after=await db.PaperAccounts.SingleAsync();
                Assert.True(after.RiskLocked);Assert.False(after.Running);
                Assert.Equal(expected.EquityAfter,after.CashBrl,10);
                Assert.Empty(await db.PaperPositions.ToListAsync());
                Assert.Equal(18,await db.PaperFills.CountAsync());
                Assert.Equal(audits+1,await db.AuditEvents.CountAsync());
                Assert.Empty(await db.TestnetOrders.ToListAsync());
                Assert.Equal(1,await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM rac.RecoveryApprovalUses").SingleAsync());
                Console.WriteLine($"BACKUP_RECONCILED cash={after.CashBrl} fee={expected.Fee} realized={expected.Realized}");
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await Exec("IF DB_ID("+Q(name)+") IS NOT NULL BEGIN ALTER DATABASE ["+name+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+name+"]; END");
            Directory.Delete(dir,true);
        }
    }
}
