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
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie",Cookie("RiskReviewer"));
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/ana/advisories?limit=101")).StatusCode);
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
}
