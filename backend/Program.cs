using System.Text.Json;
using KonkoorHelp.Api.Api;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Integrations;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Learning.Ai;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 48 * 1024 * 1024;
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.DictionaryKeyPolicy = null;
});

var connectionString = builder.Configuration.GetConnectionString("KonkoorHelp");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("رشته اتصال KonkoorHelp در تنظیمات سرور خالی است.");
    }

    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=KonkoorHelp;Trusted_Connection=True;TrustServerCertificate=True";
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<OutboxPublisher>();
builder.Services.AddScoped<StudyEventEmitter>();
builder.Services.AddScoped<StudyStore>();
builder.Services.AddScoped<NotesStore>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<GamificationProfileService>();
builder.Services.AddScoped<XpEngine>();
builder.Services.AddScoped<LevelEngine>();
builder.Services.AddScoped<BadgeEngine>();
builder.Services.AddScoped<QuestEngine>();
builder.Services.AddScoped<DailyMissionService>();
builder.Services.AddScoped<StreakEngine>();
builder.Services.AddScoped<GamificationQueryService>();
builder.Services.AddScoped<GamificationPipeline>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<RewardService>();
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<MasteryService>();
builder.Services.AddScoped<SpacedRepetitionService>();
builder.Services.AddScoped<AssessmentService>();
builder.Services.AddScoped<JourneyService>();
builder.Services.AddScoped<PersonalImprovementService>();
builder.Services.AddScoped<LearnerHomeService>();
builder.Services.AddScoped<AiContextBuilder>();
builder.Services.AddScoped<AiCoachService>();
builder.Services.AddScoped<SchoolAccess>();
builder.Services.AddScoped<SchoolService>();
builder.Services.AddScoped<SchoolRegistrationService>();
builder.Services.AddScoped<PlatformAdminService>();
builder.Services.AddScoped<TeacherPortalService>();
builder.Services.AddScoped<ParentPortalService>();
builder.Services.AddScoped<SchoolAnalyticsService>();
builder.Services.AddScoped<ApiClientService>();
builder.Services.AddSingleton<WebhookSecretProtector>();
builder.Services.AddSingleton<IWebhookDnsResolver, SystemWebhookDnsResolver>();
builder.Services.AddSingleton<IWebhookSocketConnector, TcpWebhookSocketConnector>();
builder.Services.AddSingleton<WebhookUrlSafety>();
builder.Services.AddScoped<WebhookService>();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<IntegrationsOptions>(builder.Configuration.GetSection(IntegrationsOptions.SectionName));
builder.Services.AddHttpClient(nameof(OpenAiCompatibleProvider));
builder.Services.AddHttpClient(nameof(WebhookService), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(sp =>
    WebhookSocketsHttpHandlerFactory.Create(sp.GetRequiredService<IWebhookSocketConnector>()));
builder.Services.AddSingleton<IAiProvider, NullAiProvider>();
builder.Services.AddSingleton<IAiProvider, HeuristicAiProvider>();
builder.Services.AddSingleton<IAiProvider, OpenAiCompatibleProvider>();
builder.Services.AddSingleton<IAiProviderResolver, AiProviderResolver>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<WebhookDispatcher>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddDbContextCheck<AppDbContext>("database");

var loginLimiter = new AttemptLimiter(10, TimeSpan.FromMinutes(15));
var registerLimiter = new AttemptLimiter(10, TimeSpan.FromHours(1));
var apiKeyLimiter = new AttemptLimiter(120, TimeSpan.FromMinutes(1));

var app = builder.Build();
app.UseForwardedHeaders();

await DatabaseBootstrap.ApplyAsync(app.Services, app.Logger);

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Name == "database",
});
app.MapGet("/health/live", () => Results.Json(new { status = "ok", utc = DateTime.UtcNow }));

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsJsonAsync(new { error = "درخواست بیش از حد بزرگ است." });
    }
    catch (BadHttpRequestException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "درخواست درست نیست." });
    }
    catch (Exception error)
    {
        app.Logger.LogError(error, "Unhandled API error");
        if (context.Response.HasStarted) throw;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        if (app.Environment.IsEnvironment("Testing"))
        {
            await context.Response.WriteAsJsonAsync(new { error = "خطای سرور.", detail = error.ToString() });
        }
        else
        {
            await context.Response.WriteAsJsonAsync(new { error = "خطای سرور." });
        }
    }
});

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    var hasApiKey = context.Request.Headers.TryGetValue(ApiClientService.HeaderName, out var apiKeyHeader)
        && !string.IsNullOrWhiteSpace(apiKeyHeader);

    // Cookie/browser mutations require Origin; machine API keys do not.
    if (!hasApiKey && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin))
        {
            var allowed = false;
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                var host = uri.Authority;
                var configured = context.RequestServices.GetRequiredService<IConfiguration>()
                    .GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];
                allowed = string.Equals(host, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
                    || host.Equals("127.0.0.1:5173", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("localhost:5173", StringComparison.OrdinalIgnoreCase)
                    || configured.Any(item => string.Equals(item, origin, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item, host, StringComparison.OrdinalIgnoreCase));
            }

            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "این درخواست از مبدا مجاز نیست." });
                return;
            }
        }
    }

    if (hasApiKey)
    {
        var key = apiKeyHeader.ToString();
        var limitKey = $"api|{context.Connection.RemoteIpAddress}|{key[..Math.Min(12, key.Length)]}";
        if (apiKeyLimiter.IsBlocked(limitKey))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "نرخ درخواست بیش از حد مجاز است." });
            return;
        }

        apiKeyLimiter.Hit(limitKey);
        var clients = context.RequestServices.GetRequiredService<ApiClientService>();
        Guid? requestedWorkspace = null;
        var header = context.Request.Headers[WorkspaceService.HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(header) && Guid.TryParse(header, out var parsed))
            requestedWorkspace = parsed;

        var auth = await clients.AuthenticateAsync(key, requestedWorkspace, context.RequestAborted);
        if (auth is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "کلید API نامعتبر است.", code = "api_key_invalid" });
            return;
        }

        var (client, workspaceId) = auth.Value;
        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var owner = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == client.OwnerUserId, context.RequestAborted);
        if (owner is null || owner.DeletedAt is not null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "کلید API نامعتبر است." });
            return;
        }

        var member = await db.WorkspaceMembers.AsNoTracking().FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == owner.Id && item.Status == WorkspaceMemberStatus.Active && item.DeletedAt == null,
            context.RequestAborted);
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(item => item.Id == workspaceId && item.DeletedAt == null, context.RequestAborted);
        if (workspace is null || (member is null && workspace.OwnerUserId != owner.Id))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "دسترسی به این فضا مجاز نیست.", code = "workspace_forbidden" });
            return;
        }

        context.Items["user"] = new SessionUser(owner.Id, owner.Username);
        context.Items[ApiClientService.ItemKey] = client;
        context.Items[WorkspaceService.ItemKey] = new WorkspaceContext(
            workspace.Id,
            workspace.Type,
            member?.Role ?? WorkspaceMemberRole.Student,
            owner.Id,
            workspace.Name);
        await next();
        return;
    }

    if (context.Request.Cookies.TryGetValue(SessionService.CookieName, out var token))
    {
        var sessions = context.RequestServices.GetRequiredService<SessionService>();
        var user = await sessions.FindAsync(token, context.RequestAborted);
        if (user is not null)
        {
            context.Items["user"] = user;
            var workspaces = context.RequestServices.GetRequiredService<WorkspaceService>();
            try
            {
                var header = context.Request.Headers[WorkspaceService.HeaderName].ToString();
                context.Items[WorkspaceService.ItemKey] = await workspaces.ResolveAsync(
                    user.Id,
                    string.IsNullOrWhiteSpace(header) ? null : header,
                    context.RequestAborted);
            }
            catch (WorkspaceForbiddenException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "دسترسی به این فضا مجاز نیست.", code = "workspace_forbidden" });
                return;
            }
        }
    }

    await next();
});

var api = app.MapGroup("/api");
api.MapAuth(loginLimiter, registerLimiter);
api.MapWorkspaces();
api.MapData();
api.MapNotes();
api.MapAccount();
api.MapGamification();
api.MapSocialGamification();
api.MapLearningIntelligence();
api.MapLearningJourney();
api.MapPersonalImprovement();
api.MapLearnerHome();
api.MapSchoolEdition();
api.MapPlatformAdmin();
api.MapIntegrations();
api.MapPublicApiV1();
api.Map("/{**path}", () => Results.Json(new { error = "این آدرس وجود ندارد." }, statusCode: StatusCodes.Status404NotFound));

var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (File.Exists(Path.Combine(webRoot, "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
