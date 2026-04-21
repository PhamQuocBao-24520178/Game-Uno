using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Security.Claims;
using UnoGame.API.Hubs;
using UnoGame.API.Middleware;
using UnoGame.API.Services;
using UnoGame.Core.Room;
using UnoGame.Infrastructure.Repositories;
using UnoGame.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ── Firebase Admin SDK ────────────────────────────────────────────────────────
var serviceAccountPath = cfg["Firebase:ServiceAccountPath"] ?? "firebase-service-account.json";
if (!File.Exists(serviceAccountPath))
    throw new FileNotFoundException(
        $"Firebase service account not found: {serviceAccountPath}\n" +
        "Download: Firebase Console → Project Settings → Service Accounts.");

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(serviceAccountPath)
});

// ── MongoDB ────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(cfg["MongoDB:ConnectionString"]
        ?? throw new InvalidOperationException("MongoDB:ConnectionString is required")));

builder.Services.AddScoped<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(cfg["MongoDB:DatabaseName"] ?? "uno_db"));

// ── Redis (optional) ──────────────────────────────────────────────────────────
var redisConn = cfg["Redis:ConnectionString"];
var hasRedis = !string.IsNullOrWhiteSpace(redisConn);
if (hasRedis)
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        _ => ConnectionMultiplexer.Connect(redisConn!));

// ── Memory Cache ──────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);

// ── HTTP Client (Firebase REST) ───────────────────────────────────────────────
builder.Services.AddHttpClient("firebase-rest", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ── Authentication — Firebase JWT Bearer ─────────────────────────────────────
var projectId = cfg["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId is required");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.Authority = $"https://securetoken.google.com/{projectId}";
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{projectId}",
            ValidateAudience = true,
            ValidAudience = projectId,
            ValidateLifetime = true,
        };
        // SignalR gửi token qua query string ?access_token=
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── SignalR ────────────────────────────────────────────────────────────────────
var signalR = builder.Services.AddSignalR(opt =>
{
    opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opt.MaximumReceiveMessageSize = 32 * 1024;
    opt.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    opt.KeepAliveInterval = TimeSpan.FromSeconds(15);
    opt.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

if (hasRedis)
    signalR.AddStackExchangeRedis(redisConn!,
        o => o.Configuration.ChannelPrefix = new RedisChannel("UnoGame", RedisChannel.PatternMode.Literal));

builder.Services.AddSingleton<IUserIdProvider, FirebaseUserIdProvider>();

// ── CORS ───────────────────────────────────────────────────────────────────────
var origins = cfg.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:8080"];

builder.Services.AddCors(opt => opt.AddPolicy("UnoPolicy", p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ── DI — Singleton ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
builder.Services.AddSingleton<IBotOrchestrator, BotOrchestrator>();

builder.Services.AddSingleton<IRoomManager>(sp => new RoomManager(
    sp.GetRequiredService<IRoomService>(),
    sp.GetRequiredService<IUserService>(),
    sp.GetRequiredService<IGameService>(),
    sp.GetRequiredService<ILogger<RoomManager>>(),
    sp.GetService<RoomManagerOptions>()));

if (hasRedis)
    builder.Services.AddSingleton<ITokenBlacklistService, RedisTokenBlacklistService>();
else
    builder.Services.AddSingleton<ITokenBlacklistService, MemoryTokenBlacklistService>();

// ── DI — Scoped ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IGameHistoryRepository, GameHistoryRepository>();

builder.Services.AddHostedService<WeeklyResetService>();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// ── OpenAPI (.NET 10 native) ──────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// DB indexes (idempotent)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await UnoGame.Infrastructure.DatabaseInitializer.InitializeAsync(db);
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();

if (app.Environment.IsDevelopment())
{
    // JSON spec tại: GET /openapi/v1.json
    // Dán URL vào https://editor.swagger.io để xem UI
    app.MapOpenApi();
}

app.UseCors("UnoPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FirebaseAuthMiddleware>();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    redis = hasRedis ? "enabled" : "disabled",
    dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    version = "1.0.0"
})).AllowAnonymous();

app.Run();

// ── Firebase UID → SignalR UserIdentifier ─────────────────────────────────────
public class FirebaseUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme))
            return;

        // Đảm bảo Components tồn tại
        document.Components ??= new OpenApiComponents();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Firebase ID Token — lấy từ POST /api/auth/login"
        };

        // Tạo security requirement dùng cú pháp OpenApi 2.0:
        //   OpenApiSecuritySchemeReference("id") thay thế cho pattern Reference cũ
        var requirement = new OpenApiSecurityRequirement
        {
            // Key: OpenApiSecuritySchemeReference(schemeId) — KHÔNG phải OpenApiSecurityScheme { Reference = ... }
            // OpenApiReference và ReferenceType không còn tồn tại trong OpenApi 2.0
            [new OpenApiSecuritySchemeReference("Bearer")] = []
        };

        // Áp dụng cho tất cả operations
        foreach (var pathItem in document.Paths.Values)
        {
            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }
    }
}