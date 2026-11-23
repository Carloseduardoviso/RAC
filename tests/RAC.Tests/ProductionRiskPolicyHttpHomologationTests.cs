using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>
/// HTTP integration against a subprocess and a randomly named disposable SQL database.
/// Never points at operational RAC and never creates Binance orders.
/// </summary>
public sealed class ProductionRiskPolicyHttpHomologationTests
{
    private static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(35);

    [Fact]
    public async Task Users_roles_csrf_reviews_and_session_revocation_work_over_http()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dbName = "RAC_MVP1513_HTTP_" + Guid.NewGuid().ToString("N");
        var connection = @"Server=(localdb)\MSSQLLocalDB;Database=" + dbName +
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection, sql => sql.CommandTimeout(30)).Options;
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20));
        var hasher = new PasswordHasher<RacUser>();
        var names = new[] { ("admin", RacRoles.Administrator),
            ("reviewer", RacRoles.RiskReviewer), ("viewer", RacRoles.Viewer) };
        var seedIds = new Dictionary<string, Guid>();
        Process? child = null;
        using var anonymous = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            await using (var db = new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                foreach (var (name, role) in names)
                {
                    var user = new RacUser { UserName = name,
                        NormalizedUserName = name.ToUpperInvariant(),
                        Role = role, PasswordHash = "" };
                    user.PasswordHash = hasher.HashPassword(user, password);
                    seedIds[name] = user.Id;
                    db.Users.Add(user);
                }
                await db.SaveChangesAsync();
            }
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var folder = Path.GetDirectoryName(typeof(RAC.Api.Services.ProductionRiskPolicyEndpoints)
                .Assembly.Location)!;
            var apiDll = Path.Combine(folder, "RAC.Api.dll");
            Assert.True(File.Exists(apiDll), "Missing API binary for isolated HTTP homologation.");
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = folder, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(apiDll);
            start.ArgumentList.Add("--urls");
            start.ArgumentList.Add("http://127.0.0.1:" + port);
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["RAC_SQLSERVER_CONNECTION"] = connection;
            start.Environment["Rac__MarketData__Enabled"] = "false";
            child = Process.Start(start);
            Assert.NotNull(child);
            var baseUrl = "http://127.0.0.1:" + port;
            var ready = false;
            var startAt = Stopwatch.StartNew();
            while (startAt.Elapsed < StartupLimit && !child.HasExited)
            {
                try
                {
                    var response = await anonymous.GetAsync(baseUrl + "/api/health");
                    if (response.IsSuccessStatusCode) { ready = true; break; }
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(200);
            }
            Assert.True(ready, "Disposable API failed to become healthy.");

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymous.GetAsync(baseUrl + "/api/risk/policies")).StatusCode);
            using var admin = new Session(baseUrl);
            using var reviewer = new Session(baseUrl);
            using var viewer = new Session(baseUrl);
            await admin.Login("admin", password);
            await reviewer.Login("reviewer", password);
            await viewer.Login("viewer", password);
            using var noCsrf = await admin.Client.PostAsJsonAsync(
                baseUrl + "/api/risk/policies", new { });
            Assert.Equal(HttpStatusCode.Forbidden, noCsrf.StatusCode);

            Assert.Equal(HttpStatusCode.Forbidden,
                (await viewer.Client.GetAsync(baseUrl + "/api/risk/policies")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await reviewer.Client.GetAsync(baseUrl + "/api/risk/policies")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await reviewer.Post("/api/risk/policies/1/review",
                    new { approve = true, reason = "Review by non-admin should be refused." })).StatusCode);
            var limits = new { maximumCapitalUsdt = 100,
                maximumOrderUsdt = 10, maximumDailyLossPercent = 1,
                maximumDrawdownPercent = 10, maximumOpenPositions = 1,
                maximumOrdersPerHour = 3, maximumOrdersPerDay = 10,
                allowedSymbols = new[] { "BTCUSDT" } };
            Assert.Equal(HttpStatusCode.Forbidden,
                (await viewer.Post("/api/risk/policies", limits)).StatusCode);
            var created = await reviewer.Post("/api/risk/policies", limits);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var proposal = await created.Content.ReadFromJsonAsync<JsonElement>();
            var proposalId = proposal.GetProperty("id").GetInt64();
            Assert.Equal(HttpStatusCode.Forbidden,
                (await reviewer.Post("/api/risk/policies/" + proposalId + "/review",
                    new { approve = true, reason = "Reviewer must not approve." })).StatusCode);
            var reviewed = await admin.Post("/api/risk/policies/" + proposalId + "/review",
                new { approve = true, reason = "Independent administrator review for documentation." });
            Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
            var reviewJson = await reviewed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("REVIEWED_NO_TRADING_PERMISSION",
                reviewJson.GetProperty("status").GetString());
            Assert.False(reviewJson.GetProperty("orderPlacementEnabled").GetBoolean());
            Assert.Equal(HttpStatusCode.Conflict,
                (await admin.Post("/api/risk/policies/" + proposalId + "/review",
                    new { approve = true, reason = "Duplicate review should be rejected." })).StatusCode);
            var own = await admin.Post("/api/risk/policies", limits);
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            var ownId = (await own.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetInt64();
            Assert.Equal(HttpStatusCode.Conflict,
                (await admin.Post("/api/risk/policies/" + ownId + "/review",
                    new { approve = true, reason = "Self-approval should not be allowed." })).StatusCode);
            var report = await reviewer.Client.GetFromJsonAsync<JsonElement>(
                baseUrl + "/api/risk/policies");
            Assert.Equal(1, report.GetProperty("security")
                .GetProperty("reviewedDocumentOnly").GetInt32());
            Assert.Equal(1, report.GetProperty("security")
                .GetProperty("pendingReview").GetInt32());
            Assert.False(report.GetProperty("tradingEnabled").GetBoolean());

            // Role/session is revalidated from SQL on each request.
            await using (var db = new RacDbContext(options))
            {
                var disabled = await db.Users.SingleAsync(u => u.Id == seedIds["reviewer"]);
                disabled.Enabled = false;
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await reviewer.Client.GetAsync(baseUrl + "/api/risk/policies")).StatusCode);
            await using (var db = new RacDbContext(options))
            {
                Assert.Equal(2, await db.ProductionRiskPolicies.CountAsync());
                Assert.Equal(3, await db.ProductionRiskPolicyAudits.CountAsync());
                Assert.Equal(0, await db.TestnetOrders.CountAsync());
            }
        }
        finally
        {
            if (child is { HasExited: false })
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }
            child?.Dispose();
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class Session : IDisposable
    {
        private readonly string _baseUrl;
        public HttpClient Client { get; }
        public Session(string baseUrl)
        {
            _baseUrl = baseUrl;
            Client = new HttpClient(new HttpClientHandler {
                UseCookies = true, CookieContainer = new CookieContainer()
            }) { Timeout = TimeSpan.FromSeconds(10) };
        }
        public async Task Login(string user, string password)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                _baseUrl + "/api/auth/login") {
                Content = JsonContent.Create(new { userName = user, password })
            };
            request.Headers.TryAddWithoutValidation("X-RAC-CSRF", "1");
            using var response = await Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        public async Task<HttpResponseMessage> Post(string path, object json)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path) {
                Content = JsonContent.Create(json)
            };
            request.Headers.TryAddWithoutValidation("X-RAC-CSRF", "1");
            return await Client.SendAsync(request);
        }
        public void Dispose() => Client.Dispose();
    }
}
