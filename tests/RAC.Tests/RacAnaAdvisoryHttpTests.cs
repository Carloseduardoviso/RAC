using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RAC.Api;
using RAC.Infrastructure;
using RAC.Application;
using System.Net;
using System.Text.Json;

namespace RAC.Tests;
public sealed class RacAnaAdvisoryHttpTests
{
    [Fact]
    public async Task HistoryEnforcesAuthorizationAndReturnsBoundedReadOnlyEvidence()
    {
        if(!OperatingSystem.IsWindows()) return;
        var connection=@"Server=(localdb)\MSSQLLocalDB;Database=RAC_ANA_HTTP_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True";
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContextFactory<RacDbContext>(o=>o.UseSqlServer(connection));
        builder.Configuration["Specialists:AnaAdvisory:Enabled"]="true";
        builder.Services.AddRacAnaAdvisory(builder.Configuration);
        builder.Services.AddAuthentication("test").AddCookie("test",o=>{
            o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
            o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
        });
        builder.Services.AddAuthorization(o=>o.AddPolicy("RacRiskReviewer",p=>p.RequireRole("RiskReviewer")));
        await using var app=builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRacAnaAdvisories();
        var factory=app.Services.GetRequiredService<IDbContextFactory<RacDbContext>>();
        await using var db=await factory.CreateDbContextAsync();
        try
        {
            await db.Database.MigrateAsync();
            var now=DateTimeOffset.UtcNow;
            var taskId=Guid.NewGuid();
            var notice=RacAgentCommunication.Create("carlos","ana",RacMessageKind.RiskNotice,"isolated risk",taskId,now);
            await new RacAgentMessageStore(factory).SaveAsync(notice,default);
            var id=notice.Id;
            await app.StartAsync();
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                while(!await db.AnaAdvisories.AsNoTracking().AnyAsync(timeout.Token))
                    await Task.Delay(100,timeout.Token);
            }
            using var client=new HttpClient { BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/ana/advisories")).StatusCode);
            var auditPath="/api/ana/research/freeze-audit/"+new string('A',64);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(auditPath)).StatusCode);
            var provider=app.Services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
            var scheme=await provider.GetSchemeAsync("test");
            var options=app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(scheme!.Name);
            string Cookie(string role) {
                var identity=new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,role)},"test");
                var ticket=new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity),new Microsoft.AspNetCore.Authentication.AuthenticationProperties { ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(5) },"test");
                return options.Cookie.Name+"="+options.TicketDataFormat.Protect(ticket);
            }
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie",Cookie("Viewer"));
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/ana/advisories")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(auditPath)).StatusCode);
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie",Cookie("RiskReviewer"));
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/ana/advisories?limit=101")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/ana/research/freeze-audit/invalid")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(auditPath)).StatusCode);
            Assert.False(await db.IndependentResearchFreezes.AnyAsync());
            var response=await client.GetAsync("/api/ana/advisories?limit=1");
            response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var row=Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(id,row.GetProperty("evidenceIds")[0].GetGuid());
            Assert.False(row.GetProperty("tradingAuthorized").GetBoolean());
            Assert.Equal("RISK_REVIEW_REQUIRED",row.GetProperty("status").GetString());
            Assert.Equal(1,await db.AgentMessages.CountAsync());
            Assert.Equal(1,await db.AnaAdvisories.CountAsync());
            Assert.False(await db.TestnetOrders.AnyAsync());
        }
        finally { await app.StopAsync(); await db.Database.EnsureDeletedAsync(); }
    }
    [Fact]
    public async Task EnabledFreezeAuditReturnsVerifiedEvidenceAndRejectsTampering()
    {
        if(!OperatingSystem.IsWindows())return;
        var connection=@"Server=(localdb)\MSSQLLocalDB;Database=RAC_AUDIT_HTTP_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True";
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContextFactory<RacDbContext>(o=>o.UseSqlServer(connection));
        builder.Services.AddSingleton<IndependentResearchFreezeStore>(sp=>new(sp.GetRequiredService<IDbContextFactory<RacDbContext>>(),new AuditClock()));
        builder.Services.AddAuthentication("test").AddCookie("test");
        builder.Services.AddAuthorization(o=>o.AddPolicy("RacRiskReviewer",p=>p.RequireRole("RiskReviewer")));
        await using var app=builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRacAnaAdvisories();
        var factory=app.Services.GetRequiredService<IDbContextFactory<RacDbContext>>();
        await using var db=await factory.CreateDbContextAsync();
        try
        {
            await db.Database.MigrateAsync();
            var now=new AuditClock().GetUtcNow();
            var symbols=new[]{"ABCUSDT"};
            var plan=ResearchExperimentPlan.Build(symbols);
            var engine=ResearchEngineCatalog.Current.Id;
            var evidence=plan.SelectMany(row=>Enumerable.Range(5,6).Select(m=>new ResearchExecutionManifest.Evidence(row,new DateOnly(2026,m,1),new string('A',64)))).ToArray();
            var manifest=ResearchExecutionManifest.Create(symbols,engine,evidence);
            var result=new ResearchExperimentReplay.Result(0,0,0,0,null,0,false);
            var executions=new ResearchQuantitativeExecutionStore(factory);
            foreach(var item in evidence)await executions.SaveAsync(item.Row,item.SourceSha256,item.Month,engine,result,now,default);
            await new ResearchExecutionManifestStore(factory).SaveAsync(symbols,engine,evidence,now,default);
            var stress=plan.First(x=>x.Parameters.SlippagePctPerSide==.20m);
            var ids=await app.Services.GetRequiredService<IndependentResearchFreezeStore>().SavePairAsync(stress,manifest.Id,default);
            await app.StartAsync();
            using var client=new HttpClient {BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
            var options=app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get("test");
            var identity=new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,"RiskReviewer")},"test");
            var ticket=new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity),new Microsoft.AspNetCore.Authentication.AuthenticationProperties {ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(5)},"test");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie",options.Cookie.Name+"="+options.TicketDataFormat.Protect(ticket));
            var review=new RacAnaRecentReview(factory,app.Services.GetRequiredService<IndependentResearchFreezeStore>());
            var verifiedReview=await review.ReviewAsync(now,TimeSpan.FromHours(1),default);
            Assert.Contains("RESEARCH:VERIFIED_FREEZES:2:2",verifiedReview.Reasons);
            Assert.Contains("RESEARCH:INDEPENDENT_VALIDATION_REQUIRED",verifiedReview.Reasons);
        Assert.Contains("RESEARCH:CLASSIFICATION:VALIDATION_PENDING",verifiedReview.Reasons);
        Assert.Contains("RESEARCH:ACTION:COLLECT_COMPLETE_INDEPENDENT_EVIDENCE",verifiedReview.Reasons);
            Assert.False(verifiedReview.TradingAuthorized);
            var path="/api/ana/research/freeze-audit/";
            var config=ResearchExperimentIdentity.Create(stress);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path+new string('F',64))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(path+ResearchExperimentIdentity.Create(stress with {Parameters=stress.Parameters with {SlippagePctPerSide=.05m}}))).StatusCode);
            using(var response=await client.GetAsync(path+config))
            {
                response.EnsureSuccessStatusCode();
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("HISTORICAL_EVIDENCE_VERIFIED",json.RootElement.GetProperty("status").GetString());
                Assert.Equal(ids.ToArray(),json.RootElement.GetProperty("freezeIds").EnumerateArray().Select(x=>x.GetString()).ToArray());
                Assert.False(json.RootElement.GetProperty("tradingAuthorized").GetBoolean());
                Assert.False(json.RootElement.GetProperty("independentValidation").GetBoolean());
            }
            var execution=await db.ResearchQuantitativeExecutions.FirstAsync(x=>x.ConfigurationId==config);
            var original=execution.ResultJson;
            execution.ResultJson=JsonSerializer.Serialize(result with {NetPct=1});
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync(path+config)).StatusCode);
            var rejectedReview=await review.ReviewAsync(now,TimeSpan.FromHours(1),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",rejectedReview.Status);
            Assert.False(rejectedReview.TradingAuthorized);
            execution.ResultJson=original;
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(path+config)).StatusCode);
            Assert.Equal(2,await db.IndependentResearchFreezes.CountAsync());
            Assert.False(await db.TestnetOrders.AnyAsync());
        }
        finally {await app.StopAsync();await db.Database.EnsureDeletedAsync();}
    }
    private sealed class AuditClock:TimeProvider
    {
        public override DateTimeOffset GetUtcNow()=>new(2026,11,25,0,0,0,TimeSpan.Zero);
    }
}
