using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Tests;

public class HttpPublicApiTests : IClassFixture<HttpPublicApiTests.Factory>
{
    private readonly Factory _factory;

    public HttpPublicApiTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Unauthenticated_v1_request_returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_workspace_header_is_rejected_for_api_key()
    {
        var (apiKey, wsA, wsB) = await _factory.SeedPersonalApiKeyAsync(["gamification:read"]);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Add("X-Rotbeh-Workspace", wsB.ToString());
        var response = await client.GetAsync("/api/v1/gamification/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _ = wsA;
    }

    [Fact]
    public async Task Missing_api_scope_returns_403()
    {
        var (apiKey, ws, _) = await _factory.SeedPersonalApiKeyAsync(["gamification:read"]);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Add("X-Rotbeh-Workspace", ws.ToString());
        var response = await client.GetAsync("/api/v1/rewards");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("scope_forbidden", body);
    }

    [Fact]
    public async Task Student_cannot_access_school_analytics()
    {
        var (apiKey, ws) = await _factory.SeedSchoolStudentKeyAsync(["analytics:read"]);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Add("X-Rotbeh-Workspace", ws.ToString());
        var response = await client.GetAsync("/api/v1/analytics/school");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cross_workspace_assessment_access_is_denied()
    {
        var (apiKey, wsAllowed, foreignAssessmentId) = await _factory.SeedAssessmentIsolationAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        client.DefaultRequestHeaders.Add("X-Rotbeh-Workspace", wsAllowed.ToString());
        var response = await client.GetAsync($"/api/v1/assessments/{foreignAssessmentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthorized_member_cannot_manage_webhooks()
    {
        var (cookieOwner, cookieStudent, ws) = await _factory.SeedSchoolOwnerAndStudentSessionsAsync();
        var denied = await _factory.SendAuthedJsonAsync(HttpMethod.Post, "/api/integrations/webhooks", cookieStudent, ws, new
        {
            url = "https://8.8.8.8/hooks",
            eventTypes = new[] { "LevelUp" },
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await _factory.SendAuthedJsonAsync(HttpMethod.Post, "/api/integrations/webhooks", cookieOwner, ws, new
        {
            url = "https://8.8.8.8/hooks",
            eventTypes = new[] { "LevelUp" },
        });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task Webhook_registration_rejects_prohibited_destinations_over_http()
    {
        var (cookie, ws) = await _factory.SeedPersonalOwnerSessionAsync();
        var response = await _factory.SendAuthedJsonAsync(HttpMethod.Post, "/api/integrations/webhooks", cookie, ws, new
        {
            url = "https://127.0.0.1/hooks",
            eventTypes = new[] { "LevelUp" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Api_client_creation_cannot_silently_obtain_wildcard_scopes()
    {
        var (cookie, ws) = await _factory.SeedPersonalOwnerSessionAsync();

        var missingScopes = await _factory.SendAuthedJsonAsync(HttpMethod.Post, "/api/integrations/api-clients", cookie, ws, new { name = "no-scopes" });
        Assert.Equal(HttpStatusCode.BadRequest, missingScopes.StatusCode);

        var wildcard = await _factory.SendAuthedJsonAsync(HttpMethod.Post, "/api/integrations/api-clients", cookie, ws, new
        {
            name = "star",
            scopes = new[] { "*" },
        });
        Assert.Equal(HttpStatusCode.Forbidden, wildcard.StatusCode);

        var ok = await _factory.SendAuthedJsonAsync(HttpMethod.Post, "/api/integrations/api-clients", cookie, ws, new
        {
            name = "limited",
            scopes = new[] { "gamification:read" },
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "http-" + Guid.NewGuid().ToString("N");
        private readonly string _protectionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        private int _userSeq = 20000;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                RemoveAppDbContext(services);
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));
                services.RemoveAll(typeof(IOptions<IntegrationsOptions>));
                services.AddSingleton(Options.Create(new IntegrationsOptions { WebhookProtectionKey = _protectionKey }));
            });
        }

        private static void RemoveAppDbContext(IServiceCollection services)
        {
            var remove = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(AppDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || (descriptor.ServiceType.IsGenericType
                        && descriptor.ServiceType.GetGenericTypeDefinition().FullName?
                            .Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal) == true
                        && descriptor.ServiceType.GenericTypeArguments.Any(arg => arg == typeof(AppDbContext)))
                    || descriptor.ImplementationType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in remove)
                services.Remove(descriptor);
        }

        public async Task<(string ApiKey, Guid WsA, Guid WsB)> SeedPersonalApiKeyAsync(string[] scopes)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clients = scope.ServiceProvider.GetRequiredService<ApiClientService>();
            var ownerId = Interlocked.Increment(ref _userSeq);
            var otherId = Interlocked.Increment(ref _userSeq);
            db.Users.AddRange(
                new UserAccount { Id = ownerId, Username = "u" + ownerId, NormalizedUsername = "u" + ownerId, PasswordHash = "x", CreatedAt = DateTime.UtcNow },
                new UserAccount { Id = otherId, Username = "u" + otherId, NormalizedUsername = "u" + otherId, PasswordHash = "x", CreatedAt = DateTime.UtcNow });
            var wsA = Guid.NewGuid();
            var wsB = Guid.NewGuid();
            db.Workspaces.AddRange(
                new Workspace { Id = wsA, Type = WorkspaceType.Personal, Name = "A", OwnerUserId = ownerId, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Workspace { Id = wsB, Type = WorkspaceType.Personal, Name = "B", OwnerUserId = otherId, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.WorkspaceMembers.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = wsA,
                UserId = ownerId,
                Role = WorkspaceMemberRole.Student,
                Status = WorkspaceMemberStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            var created = JsonDocument.Parse(JsonSerializer.Serialize(
                await clients.CreateAsync(ownerId, 0, "http-key", wsA, scopes, CancellationToken.None)));
            return (created.RootElement.GetProperty("apiKey").GetString()!, wsA, wsB);
        }

        public async Task<(string ApiKey, Guid SchoolWs)> SeedSchoolStudentKeyAsync(string[] scopes)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clients = scope.ServiceProvider.GetRequiredService<ApiClientService>();
            var ownerId = Interlocked.Increment(ref _userSeq);
            var studentId = Interlocked.Increment(ref _userSeq);
            db.Users.AddRange(
                new UserAccount { Id = ownerId, Username = "o" + ownerId, NormalizedUsername = "o" + ownerId, PasswordHash = "x", CreatedAt = DateTime.UtcNow },
                new UserAccount { Id = studentId, Username = "s" + studentId, NormalizedUsername = "s" + studentId, PasswordHash = "x", CreatedAt = DateTime.UtcNow });
            var ws = Guid.NewGuid();
            db.Workspaces.Add(new Workspace
            {
                Id = ws,
                Type = WorkspaceType.School,
                Name = "School",
                OwnerUserId = ownerId,
                Status = WorkspaceStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.Schools.Add(new School
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws,
                Name = "School",
                Slug = "s" + ownerId,
                OwnerUserId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.WorkspaceMembers.AddRange(
                new WorkspaceMember { Id = Guid.NewGuid(), WorkspaceId = ws, UserId = ownerId, Role = WorkspaceMemberRole.SchoolOwner, Status = WorkspaceMemberStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new WorkspaceMember { Id = Guid.NewGuid(), WorkspaceId = ws, UserId = studentId, Role = WorkspaceMemberRole.Student, Status = WorkspaceMemberStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            // Create as owner then reassign key ownership to student so the principal is a student member.
            var created = JsonDocument.Parse(JsonSerializer.Serialize(
                await clients.CreateAsync(ownerId, 0, "student-key", ws, scopes, CancellationToken.None)));
            var clientId = created.RootElement.GetProperty("id").GetGuid();
            var apiKey = created.RootElement.GetProperty("apiKey").GetString()!;
            var row = await db.ApiClients.SingleAsync(item => item.Id == clientId);
            row.OwnerUserId = studentId;
            await db.SaveChangesAsync();
            return (apiKey, ws);
        }

        public async Task<(string ApiKey, Guid AllowedWs, Guid ForeignAssessmentId)> SeedAssessmentIsolationAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clients = scope.ServiceProvider.GetRequiredService<ApiClientService>();
            var ownerId = Interlocked.Increment(ref _userSeq);
            var otherId = Interlocked.Increment(ref _userSeq);
            db.Users.AddRange(
                new UserAccount { Id = ownerId, Username = "a" + ownerId, NormalizedUsername = "a" + ownerId, PasswordHash = "x", CreatedAt = DateTime.UtcNow },
                new UserAccount { Id = otherId, Username = "b" + otherId, NormalizedUsername = "b" + otherId, PasswordHash = "x", CreatedAt = DateTime.UtcNow });
            var wsA = Guid.NewGuid();
            var wsB = Guid.NewGuid();
            db.Workspaces.AddRange(
                new Workspace { Id = wsA, Type = WorkspaceType.Personal, Name = "A", OwnerUserId = ownerId, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Workspace { Id = wsB, Type = WorkspaceType.Personal, Name = "B", OwnerUserId = otherId, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.WorkspaceMembers.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = wsA,
                UserId = ownerId,
                Role = WorkspaceMemberRole.Student,
                Status = WorkspaceMemberStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            var foreignId = Guid.NewGuid();
            db.Assessments.Add(new AssessmentDefinition
            {
                Id = foreignId,
                WorkspaceId = wsB,
                Title = "Foreign",
                Kind = AssessmentKind.TopicQuiz,
                Status = AssessmentStatus.Published,
                CreatedByUserId = otherId,
                PassScore = 50,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            var created = JsonDocument.Parse(JsonSerializer.Serialize(
                await clients.CreateAsync(ownerId, 0, "assess", wsA, ["assessments:read"], CancellationToken.None)));
            return (created.RootElement.GetProperty("apiKey").GetString()!, wsA, foreignId);
        }

        public async Task<(string OwnerCookie, string StudentCookie, Guid SchoolWs)> SeedSchoolOwnerAndStudentSessionsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<KonkoorHelp.Api.Services.PasswordService>();
            var ownerId = Interlocked.Increment(ref _userSeq);
            var studentId = Interlocked.Increment(ref _userSeq);
            var ownerName = "own" + ownerId;
            var studentName = "stu" + studentId;
            db.Users.AddRange(
                new UserAccount { Id = ownerId, Username = ownerName, NormalizedUsername = ownerName, PasswordHash = passwords.Hash("password1"), CreatedAt = DateTime.UtcNow },
                new UserAccount { Id = studentId, Username = studentName, NormalizedUsername = studentName, PasswordHash = passwords.Hash("password1"), CreatedAt = DateTime.UtcNow });
            var ws = Guid.NewGuid();
            db.Workspaces.Add(new Workspace
            {
                Id = ws,
                Type = WorkspaceType.School,
                Name = "SchoolHttp",
                OwnerUserId = ownerId,
                Status = WorkspaceStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.Schools.Add(new School
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws,
                Name = "SchoolHttp",
                Slug = "sch" + ownerId,
                OwnerUserId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.WorkspaceMembers.AddRange(
                new WorkspaceMember { Id = Guid.NewGuid(), WorkspaceId = ws, UserId = ownerId, Role = WorkspaceMemberRole.SchoolOwner, Status = WorkspaceMemberStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new WorkspaceMember { Id = Guid.NewGuid(), WorkspaceId = ws, UserId = studentId, Role = WorkspaceMemberRole.Student, Status = WorkspaceMemberStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            // personal workspaces for login resolve (must include memberships)
            var personalOwner = Guid.NewGuid();
            var personalStudent = Guid.NewGuid();
            db.Workspaces.AddRange(
                new Workspace { Id = personalOwner, Type = WorkspaceType.Personal, Name = "p-o", OwnerUserId = ownerId, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Workspace { Id = personalStudent, Type = WorkspaceType.Personal, Name = "p-s", OwnerUserId = studentId, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.WorkspaceMembers.AddRange(
                new WorkspaceMember { Id = Guid.NewGuid(), WorkspaceId = personalOwner, UserId = ownerId, Role = WorkspaceMemberRole.Student, Status = WorkspaceMemberStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new WorkspaceMember { Id = Guid.NewGuid(), WorkspaceId = personalStudent, UserId = studentId, Role = WorkspaceMemberRole.Student, Status = WorkspaceMemberStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var ownerCookie = IssueSessionCookie(db, ownerId);
            var studentCookie = IssueSessionCookie(db, studentId);
            await db.SaveChangesAsync();
            return (ownerCookie, studentCookie, ws);
        }

        public async Task<(string Cookie, Guid Ws)> SeedPersonalOwnerSessionAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<KonkoorHelp.Api.Services.PasswordService>();
            var userId = Interlocked.Increment(ref _userSeq);
            var username = "pers" + userId;
            db.Users.Add(new UserAccount
            {
                Id = userId,
                Username = username,
                NormalizedUsername = username,
                PasswordHash = passwords.Hash("password1"),
                CreatedAt = DateTime.UtcNow,
            });
            var ws = Guid.NewGuid();
            db.Workspaces.Add(new Workspace
            {
                Id = ws,
                Type = WorkspaceType.Personal,
                Name = "Personal",
                OwnerUserId = userId,
                Status = WorkspaceStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.WorkspaceMembers.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws,
                UserId = userId,
                Role = WorkspaceMemberRole.Student,
                Status = WorkspaceMemberStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            var cookie = IssueSessionCookie(db, userId);
            await db.SaveChangesAsync();
            return (cookie, ws);
        }

        private static string IssueSessionCookie(AppDbContext db, int userId)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            db.AuthSessions.Add(new AuthSession
            {
                TokenHash = KonkoorHelp.Api.Services.SessionService.Hash(token),
                UserId = userId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds(),
            });
            return $"kh_session={token}";
        }

        public Task<HttpResponseMessage> SendAuthedJsonAsync(HttpMethod method, string path, string cookie, Guid workspaceId, object body) =>
            SendAuthedAsync(method, path, cookie, workspaceId, body);

        public async Task<HttpResponseMessage> SendAuthedAsync(HttpMethod method, string path, string cookie, Guid workspaceId, object? body = null)
        {
            var client = CreateClient();
            using var request = new HttpRequestMessage(method, path);
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            request.Headers.TryAddWithoutValidation("X-Rotbeh-Workspace", workspaceId.ToString());
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
    }
}
