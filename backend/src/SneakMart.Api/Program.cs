using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SneakMart.Api.Data;
using SneakMart.Api.Dtos;
using SneakMart.Api.Infrastructure;
using SneakMart.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration (secrets come from environment variables, never from committed files)
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set the ConnectionStrings__Default environment variable.");

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Set the Jwt__Key environment variable (at least 32 characters).");

// ---- services
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));
builder.Services.AddScoped<CatalogService>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(o =>
{
    // Model-binding / [Required] failures use the same error shape as everything else (400).
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .SelectMany(kv => kv.Value!.Errors.Select(e => new FieldError(
                Naming.ToCamel(kv.Key.TrimStart('$', '.')),
                string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)))
            .ToList();
        return new BadRequestObjectResult(new ErrorResponse(400, "VALIDATION_ERROR", "One or more fields are invalid.", errors));
    };
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsJsonAsync(new ErrorResponse(401, "UNAUTHENTICATED", "Authentication is required."));
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsJsonAsync(new ErrorResponse(403, "FORBIDDEN", "Administrator role required."));
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// ---- seed command: dotnet run --project src/SneakMart.Api -- --seed
if (args.Contains("--seed"))
{
    var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(adminPassword))
        throw new InvalidOperationException("Set the SEED_ADMIN_PASSWORD environment variable.");

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.RunAsync(db, adminPassword);
    Console.WriteLine($"Seed complete. Admin login: {SeedData.AdminEmail}");
    return;
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Required so the test project's WebApplicationFactory<Program> can see the entry point.
public partial class Program { }
