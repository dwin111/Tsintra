using Tsintra.Api.Auth.Services;
using Tsintra.Api.Auth.Interfaces;
using Tsintra.Persistence;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;
using Tsintra.Domain.Interfaces;
using Tsintra.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Tsintra.Api.Auth.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Виведення імені сервісу
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("========================================");
Console.WriteLine("Starting Tsintra Auth API Service");
Console.WriteLine("========================================");
Console.ResetColor();

builder.Services.AddOptions<GoogleAuthOptions>()
    .Bind(builder.Configuration.GetSection(GoogleAuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Add CORS --- 
const string ApiCorsPolicy = "AllowMainApi";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: ApiCorsPolicy,
                      policy =>
                      {
                          policy.WithOrigins(
                                "http://localhost:5000",  // Main API
                                "https://localhost:7288", // Main API with HTTPS
                                "http://localhost:5174",  // React client
                                "http://localhost:5173"   // React client dev server
                            )
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials(); // Required for cookies
                      });
});
// --- End CORS ---


builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScoped<IAuthService, AuthService>();




builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    // Cookie settings for security and session management
    options.Cookie.HttpOnly = true; // Prevent client-side script access
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Only send over HTTPS
    options.Cookie.SameSite = SameSiteMode.None; // Change to None for OAuth redirects to work properly
    options.ExpireTimeSpan = TimeSpan.FromDays(14); // Set cookie expiration
    options.SlidingExpiration = true; // Renew cookie on activity
    options.LoginPath = "/api/auth/unauthorized"; // Redirect path if access denied (optional)
    options.AccessDeniedPath = "/api/auth/forbidden"; // Redirect path if forbidden (optional)
})
.AddGoogle(googleOptions =>
{
    var sp = builder.Services.BuildServiceProvider();
    var googleAuthConfig = sp.GetRequiredService<IOptions<GoogleAuthOptions>>().Value;

    // Add null checks for safety, although ValidateOnStart should prevent this
    if (string.IsNullOrEmpty(googleAuthConfig.ClientId))
    {
        throw new InvalidOperationException("GoogleAuthOptions:ClientId is not configured.");
    }
    if (string.IsNullOrEmpty(googleAuthConfig.ClientSecret))
    {
        throw new InvalidOperationException("GoogleAuthOptions:ClientSecret is not configured.");
    }

    googleOptions.ClientId = googleAuthConfig.ClientId;
    googleOptions.ClientSecret = googleAuthConfig.ClientSecret;
    googleOptions.CallbackPath = "/api/Auth/google-callback";

    // Add specific configuration for OAuth state cookie
    googleOptions.CorrelationCookie.SameSite = SameSiteMode.None;
    googleOptions.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    // Request specific scopes (optional, default includes profile and email)
    // googleOptions.Scope.Add("profile");
    // googleOptions.Scope.Add("email");
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]))
    };

    // Configure to read JWT from cookie
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["jwt"];
            return Task.CompletedTask;
        }
    };
});

// Register JWT service in DI container
builder.Services.AddScoped<Tsintra.Api.Auth.Interfaces.IJwtTokenService, Tsintra.Api.Auth.Services.JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tsintra Auth API v1");
        c.RoutePrefix = "swagger";
    });
    
    // Виведемо URL, на якому можна відкрити Swagger
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Swagger UI is available at: {app.Urls?.FirstOrDefault() ?? "http://localhost:PORT"}/swagger");
    Console.ResetColor();
}

app.UseHttpsRedirection();

// --- Use CORS --- 
// Важливо викликати UseCors перед UseAuthentication та UseAuthorization
app.UseCors(ApiCorsPolicy);
// --- End Use CORS ---

app.UseAuthentication(); // Place *before* UseAuthorization
app.UseAuthorization();

app.MapControllers();

// Add simple unauthorized/forbidden endpoints if LoginPath/AccessDeniedPath are set
app.MapGet("/api/auth/unauthorized", () => Results.Unauthorized()).ExcludeFromDescription();
app.MapGet("/api/auth/forbidden", () => Results.Forbid()).ExcludeFromDescription();

// Виведемо доступні URL для сервісу
Console.ForegroundColor = ConsoleColor.Yellow;
if (app.Urls != null && app.Urls.Any())
{
    Console.WriteLine("Auth API Service is running on:");
    foreach (var url in app.Urls)
    {
        Console.WriteLine($" - {url}");
    }
}
else
{
    Console.WriteLine("Auth API Service is running (URL not available)");
}
Console.ResetColor();

app.Run();
