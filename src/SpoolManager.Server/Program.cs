using System.Security.Claims;
using System.Text;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using SpoolManager.Infrastructure;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Infrastructure.Migrations;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Server.Filters;
using SpoolManager.Server.Middleware;
using SpoolManager.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddSpoolManagerDb(connectionString);

builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(r => r
        .AddMySql8()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(M001_InitialSchema).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMaterialRepository, MaterialRepository>();
builder.Services.AddScoped<ISpoolRepository, SpoolRepository>();
builder.Services.AddScoped<IPrinterRepository, PrinterRepository>();
builder.Services.AddScoped<IStorageLocationRepository, StorageLocationRepository>();
builder.Services.AddScoped<IDryerRepository, DryerRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<IOpenSpoolService, OpenSpoolService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IMaterialExportService, MaterialExportService>();
builder.Services.AddScoped<ISlicerProfileRepository, SlicerProfileRepository>();
builder.Services.AddScoped<ISlicerExportService, SlicerExportService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ISmtpSettingsRepository, SmtpSettingsRepository>();
builder.Services.AddScoped<ISiteSettingsRepository, SiteSettingsRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<NotificationBackgroundService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpoolmanDbSyncService>();
builder.Services.AddSingleton<ISpoolmanDbSyncService>(sp => sp.GetRequiredService<SpoolmanDbSyncService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SpoolmanDbSyncService>());

builder.Services.AddScoped<ProjectAuthFilter>();
builder.Services.AddSingleton<LoginRateLimiter>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tokenVersionClaim = context.Principal?.FindFirst("token_version")?.Value;

                if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                {
                    context.Fail("Invalid token.");
                    return;
                }

                if (tokenVersionClaim != null && int.TryParse(tokenVersionClaim, out var tokenVersion))
                {
                    var userRepo = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                    var user = await userRepo.GetByIdAsync(userId);
                    if (user == null || user.TokenVersion != tokenVersion)
                    {
                        context.Fail("Token has been revoked.");
                        return;
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddResponseCompression(opts =>
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();

    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    if (await userRepo.GetCountAsync() == 0)
    {
        var admin = new AppUser
        {
            Username = "admin",
            Email = "admin@localhost",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
            IsPlatformAdmin = true,
            IsSuperAdmin = true,
            CreatedAt = DateTime.UtcNow
        };
        await userRepo.CreateAsync(admin);
    }
}

app.UseResponseCompression();
app.UseBlazorFrameworkFiles();

app.MapGet("/favicon.ico", async (ISiteSettingsRepository settings, HttpContext ctx) =>
{
    var data = await settings.GetAsync("branding_favicon");
    if (string.IsNullOrEmpty(data) || !data.Contains(','))
    {
        ctx.Response.Redirect("/favicon.png");
        return;
    }
    var semiIdx = data.IndexOf(';');
    var commaIdx = data.IndexOf(',');
    var mime = semiIdx > 5 ? data[5..semiIdx] : "image/x-icon";
    var bytes = Convert.FromBase64String(data[(commaIdx + 1)..]);
    ctx.Response.Headers.CacheControl = "public, max-age=3600";
    ctx.Response.ContentType = mime;
    await ctx.Response.Body.WriteAsync(bytes);
});

app.UseStaticFiles();

app.UseAuthentication();
app.UseMiddleware<UserActivityMiddleware>();
app.UseAuthorization();

app.MapControllers();

var webRoot = app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var indexHtmlPath = Path.Combine(webRoot, "index.html");
app.MapFallback(async (HttpContext ctx) =>
{
    var html = await File.ReadAllTextAsync(indexHtmlPath);
    var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    html = html.Replace("__ORIGIN__", origin);
    ctx.Response.ContentType = "text/html; charset=utf-8";
    ctx.Response.Headers.CacheControl = "no-cache";
    await ctx.Response.WriteAsync(html);
});

app.Run();
