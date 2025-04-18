using Tsintra.Application.Services;
using Tsintra.Domain.Interfaces;
using Tsintra.Application;
using Microsoft.Extensions.Options;
using Tsintra.Infrastructure;
using PersistenceDI = Tsintra.Persistence.DependencyInjection;
using Tsintra.Persistence;
using Tsintra.Persistence.Repositories;
using Tsintra.Integrations.Prom;
using Tsintra.Integrations;
using Tsintra.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Tsintra.Api.Crm.Services;
using Tsintra.Api.Crm.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
// using Tsintra.MarketplaceAgent;

var builder = WebApplication.CreateBuilder(args);

// Виведення імені сервісу
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("========================================");
Console.WriteLine("Starting Tsintra CRM API Service");
Console.WriteLine("========================================");
Console.ResetColor();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => 
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Tsintra CRM API", Version = "v1" });
    
    // Додаємо можливість авторизації в Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Register Auth service with HttpClient
builder.Services.AddHttpClient<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Додаємо сервіс автентифікації
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Не потрібно валідувати токен, бо ми робимо це в AuthMiddleware через Auth API
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = false,
        RequireExpirationTime = false,
        RequireSignedTokens = false
    };
    
    // Перехоплюємо валідацію токена, щоб використовувати наш AuthService
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Дозволяємо читати токен з cookie
            if (string.IsNullOrEmpty(context.Token) && context.Request.Cookies.ContainsKey("jwt"))
            {
                context.Token = context.Request.Cookies["jwt"];
            }
            return Task.CompletedTask;
        }
    };
});

// Register Prom service
builder.Services.AddHttpClient<IPromService, PromService>();
builder.Services.AddScoped<IPromService, PromService>();

// Register cache services
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// Register Shared Memory Service
builder.Services.AddScoped<ISharedMemoryService, SharedMemoryService>();

// Register StatisticsService if needed
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

// Register CRM related services
builder.Services.AddScoped<ICrmService, CrmService>();
builder.Services.AddScoped<IMarketplaceIntegration, PromMarketplaceIntegration>();

// Додати Infrastructure сервіси
builder.Services.AddInfrastructure(builder.Configuration);

// Додати integrations
builder.Services.AddIntegrations(builder.Configuration);

// Додати Application сервіси
builder.Services.AddApp(builder.Configuration);

// Temporarily skip MarketplaceAgent services
// builder.Services.AddMarketplaceAgentServices(builder.Configuration);

// Додати Persistence і ініціалізувати підключення до БД
// Додати Persistence
builder.Services.AddPersistence(builder.Configuration);

// --- Add CORS --- 
const string CorsPolicy = "AllowFrontendApps";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: CorsPolicy,
                    policy =>
                    {
                        policy.WithOrigins(
                              "http://localhost:5174",
                              "http://localhost:5173"
                          )
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // Add this to allow credentials (cookies)
                    });
});
// --- End CORS ---

// Add Heath Check
builder.Services.AddHealthChecks();

var app = builder.Build();

// Ініціалізувати базу даних - створити таблиці, якщо вони не існують
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Initializing database...");
    
    try
    {
        // Temporarily skip database initialization for testing
        // await PersistenceDI.InitializeDatabaseAsync(app.Services);
        logger.LogInformation("Database initialization skipped for testing");
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tsintra CRM API v1");
        c.RoutePrefix = "swagger";
    });
    
    // Виведемо URL, на якому можна відкрити Swagger
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Swagger UI is available at: {app.Urls?.FirstOrDefault() ?? "http://localhost:PORT"}/swagger");
    Console.ResetColor();
}

app.UseHttpsRedirection();

// --- Use CORS --- 
app.UseCors(CorsPolicy);
// --- End Use CORS ---

// Додати стандартні middleware для автентифікації та авторизації
app.UseAuthentication();
app.UseAuthorization();

// Auth middleware to validate tokens with Auth server (uses IAuthService)
app.UseAuthMiddleware();

// Health check endpoint
app.MapHealthChecks("/api/health");

app.MapControllers();

// Виведемо доступні URL для сервісу
Console.ForegroundColor = ConsoleColor.Yellow;
if (app.Urls != null && app.Urls.Any())
{
    Console.WriteLine("CRM API Service is running on:");
    foreach (var url in app.Urls)
    {
        Console.WriteLine($" - {url}");
    }
}
else
{
    Console.WriteLine("CRM API Service is running (URL not available)");
}
Console.ResetColor();

app.Run();
