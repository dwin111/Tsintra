using Tsintra.Application.Services;
using Tsintra.Domain.Interfaces;
using Tsintra.Application;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;
using Tsintra.Infrastructure;
using PersistenceDI = Tsintra.Persistence.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Tsintra.Persistence.Repositories;
using Tsintra.MarketplaceAgent.Interfaces;
using Tsintra.MarketplaceAgent.Agents;
using Tsintra.MarketplaceAgent.Services;
using Tsintra.Api.Services;
using Tsintra.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Виведення імені сервісу
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("========================================");
Console.WriteLine("Starting Tsintra Main API Service");
Console.WriteLine("========================================");
Console.ResetColor();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();
builder.Services.AddScoped<IProductDescriptionGenerator, ProductDescriptionGenerator>();
builder.Services.AddScoped<IProductDescriptionAgent, ProductDescriptionAgent>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAgentMemoryService, AgentMemoryService>();
builder.Services.AddScoped<IAgent, OpenAIAgent>();

// Add memory cache (required by tools and agents)
builder.Services.AddMemoryCache();

// Add Distributed Cache (required by AgentMemoryService)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = builder.Configuration["Redis:InstanceName"];
});

// Додати Infrastructure сервіси (these provide S3 storage service)
builder.Services.AddInfrastructure(builder.Configuration);

// Додати Application сервіси
builder.Services.AddApp(builder.Configuration);

// Додати додаткові реєстрації для ListingAgent та IProductGenerationTools вручну
builder.Services.AddScoped<ListingAgent>();
builder.Services.AddScoped<Tsintra.Domain.Interfaces.IProductGenerationTools>(sp => 
{
    // Service is already registered, just return the instance
    return sp.GetRequiredService<ListingAgent>();
});

// --- Add CORS --- 
const string ReactAppCorsPolicy = "AllowReactApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: ReactAppCorsPolicy,
                      policy =>
                      {
                          policy.WithOrigins(
                                "http://localhost:5174",
                                "http://localhost:5173",
                                "https://accounts.google.com",
                                "http://localhost:5025", // Auth API service
                                "https://localhost:7026"  // Auth API service with HTTPS
                            )
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials(); // Required for cookies
                      });
});
// --- End CORS ---

// --- Add Authentication ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = false,
        ClockSkew = TimeSpan.Zero
    };
    
    // Цей обробник буде використовуватися лише для підтримки існуючого middleware
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // JWT обробляється в нашому власному middleware
            return Task.CompletedTask;
        }
    };
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
});

// Додати авторизаційний сервіс для роботи з Auth API
builder.Services.AddHttpClient<IApiAuthService, ApiAuthService>();
// --- End Authentication ---

var app = builder.Build();

// Ініціалізувати базу даних - створити таблиці, якщо вони не існують
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Initializing database...");
    
    try
    {
        await PersistenceDI.InitializeDatabaseAsync(app.Services);
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tsintra Main API v1");
        c.RoutePrefix = "swagger";
    });
    
    // Виведемо URL, на якому можна відкрити Swagger
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Swagger UI is available at: {app.Urls?.FirstOrDefault() ?? "http://localhost:PORT"}/swagger");
    Console.ResetColor();
}

app.UseHttpsRedirection();

// --- Use CORS --- 
// Важливо викликати UseCors перед UseRouting/UseAuthentication/UseAuthorization
app.UseCors(ReactAppCorsPolicy);
// --- End Use CORS ---

app.UseRouting(); // Ensure UseRouting is called before UseAuthentication and UseAuthorization

// Додати middleware для перевірки JWT токенів з Auth API
app.UseJwtAuthMiddleware();

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
    Console.WriteLine("Main API Service is running on:");
    foreach (var url in app.Urls)
    {
        Console.WriteLine($" - {url}");
    }
}
else
{
    Console.WriteLine("Main API Service is running (URL not available)");
}
Console.ResetColor();

app.Run();
