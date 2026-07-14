using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PetProductivity.Server.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Fail-fast: no arrancar sin la config crítica (mejor que reventar críptico en runtime).
// Va en user-secrets (local) o env vars (Render). Jwt:Key se suma cuando se cablee la auth (Fase 5 N2).
foreach (var key in new[] { "ConnectionStrings:DefaultConnection", "Gemini:ApiKey", "Jwt:Key" })
{
    if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
        throw new InvalidOperationException(
            $"Config crítica faltante: '{key}'. Configúrala en user-secrets (local) o variables de entorno (Render).");
}

// El secreto JWT (HS256) debe tener ≥256 bits de entropía; uno corto se rompe offline y permite forjar
// tokens con cualquier userId (takeover total). Genera uno seguro con: openssl rand -base64 48
if (System.Text.Encoding.UTF8.GetByteCount(builder.Configuration["Jwt:Key"]!) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes (256 bits) de entropía.");

// Add services to the container.
// OpenAPI nativo de .NET (Microsoft.AspNetCore.OpenApi); spec en /openapi/v1.json (solo Development).
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Auth de sesión (JWT): el userId sale del token, no del body.
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<PetProductivity.Server.Services.SessionService>(); // T14-C0
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"] ?? "petproductivity";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateLifetime = true,
        };
        // Los WebSockets no llevan headers: SignalR manda el token por query (?access_token=).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Tras el proxy de Render, RemoteIpAddress sería la IP del proxy (un solo bucket para todos). Con esto,
// .NET lee la IP real del cliente del X-Forwarded-For que añade Render → el rate-limit por IP tiene sentido.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear(); // Render es el único proxy de confianza
    options.KnownProxies.Clear();
});

// Rate-limit de endpoints caros/sensibles.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // IA: 10 tareas/min por usuario (del token; fallback a IP).
    options.AddPolicy("ai", httpContext =>
    {
        var uid = httpContext.User.GetUserId();
        var key = uid != Guid.Empty ? uid.ToString() : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon");
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    // Demo pública del Tasador (sin cuenta): 5/hora por IP. Protege la factura de Gemini;
    // más estricto que "ai" porque no hay usuario detrás, solo curiosos (o abuso).
    options.AddPolicy("demo", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter("demo:" + ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        });
    });
    // Auth: 10 intentos/min por IP en login/register (anti fuerza bruta y abuso de registro).
    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// Register Application Services
builder.Services.AddHttpClient(); // IHttpClientFactory para el canje OAuth de Google
builder.Services.AddHttpClient<IAiService, PetProductivity.Server.Services.GeminiAiService>(c =>
    c.Timeout = TimeSpan.FromSeconds(25)); // Gemini colgado → fallback heurístico rápido, no los 100 s del default
builder.Services.AddScoped<PetProductivity.Server.Services.AiJudgeService>();

// Register Database
builder.Services.AddDbContext<PetProductivity.Server.Data.AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Game Services
builder.Services.AddScoped<PetProductivity.Server.Services.PetService>();
builder.Services.AddScoped<PetProductivity.Server.Services.GroupService>();
builder.Services.AddScoped<PetProductivity.Server.Services.AccountService>(); // T14-C1: borrado de cuenta
builder.Services.AddScoped<PetProductivity.Server.Services.PushService>();
builder.Services.AddSingleton<PetProductivity.Server.Services.PresenceService>();
builder.Services.AddSingleton<PetProductivity.Server.Services.PetWriteLock>();
builder.Services.AddHostedService<PetProductivity.Server.Services.HealthDecayHostedService>();
builder.Services.AddHostedService<PetProductivity.Server.Services.AffectionDecayHostedService>();
builder.Services.AddHostedService<PetProductivity.Server.Services.FocusCleanupHostedService>();
builder.Services.AddHostedService<PetProductivity.Server.Services.StreakReminderHostedService>(); // T2: aviso nocturno de racha

// T15-C: error tracking (Sentry). Sin `Sentry:Dsn` en config = apagado (mismo patrón que PushService).
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.SendDefaultPii = false; // sin PII: no adjuntar usuario/IP a los eventos
    });

var app = builder.Build();

// Debe ir antes que todo lo que use IP o esquema (rate-limit, HttpsRedirection).
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // T14-C1: /privacidad.html (política de privacidad para Play Console)

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<PetProductivity.Server.Hubs.FamilyHub>("/hubs/family");

// Keep-warm: endpoint barato (sin auth, sin BD) para un pinger externo que evite el cold start de Render.
app.MapGet("/health", () => Results.Ok("ok"));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PetProductivity.Server.Data.AppDbContext>();
    db.Database.Migrate();
}

app.Run();
