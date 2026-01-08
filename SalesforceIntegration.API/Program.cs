using Microsoft.Extensions.Options;
using SalesforceIntegration.Application.Commands;
using SalesforceIntegration.Application.Interfaces;
using SalesforceIntegration.Infrastructure.BackgroundServices;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Auth;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Client;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Configuration;
using SalesforceIntegration.Infrastructure.Resilience;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// CONFIGURACIÓN
// ========================================

// Registrar configuración de Salesforce desde appsettings.json
builder.Services.Configure<SalesforceConfiguration>(
    builder.Configuration.GetSection(SalesforceConfiguration.SectionName));

// Registrar como singleton con validación
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IOptions<SalesforceConfiguration>>().Value;
    
    // Validar configuración al arrancar (fail-fast)
    config.Validate();
    
    return config;
});

// ========================================
// SERVICIOS DE APLICACIÓN
// ========================================

// Registrar comandos como scoped (nueva instancia por request/scope)
builder.Services.AddScoped<SyncAccountsCommand>();

// Memory Cache para tokens OAuth2
builder.Services.AddMemoryCache();

// ========================================
// SERVICIOS DE INFRAESTRUCTURA
// ========================================

// 1. HttpClient para OAuth2 (SIN resiliencia, no queremos reintentar credenciales inválidas)
builder.Services.AddHttpClient<ISalesforceAuthService, SalesforceAuthService>((sp, client) =>
{
    var config = sp.GetRequiredService<SalesforceConfiguration>();
    client.Timeout = TimeSpan.FromSeconds(30);
    // NO configurar BaseAddress aquí, se usa TokenEndpoint dinámicamente
});

// 2. HttpClient tipado para Salesforce API (CON Polly policies completas)
builder.Services.AddHttpClient<ISalesforceClient, SalesforceHttpClient>((sp, client) =>
{
    var config = sp.GetRequiredService<SalesforceConfiguration>();
    
    // Configuración base del HttpClient
    client.BaseAddress = new Uri(config.GetApiBaseUrl());
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    
    // Headers comunes
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler((sp, request) =>
{
    // Obtener dependencias desde DI
    var config = sp.GetRequiredService<SalesforceConfiguration>();
    var logger = sp.GetRequiredService<ILogger<SalesforceHttpClient>>();
    
    // Crear y retornar política combinada de Polly
    return PollyPolicies.CreateCombinedPolicy(config, logger);
});

// ========================================
// BACKGROUND SERVICES
// ========================================

// Registrar BackgroundService para sincronización periódica
builder.Services.AddHostedService<SalesforceSyncBackgroundService>();

// ========================================
// CONTROLADORES Y SWAGGER
// ========================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Salesforce Integration API",
        Version = "v1",
        Description = "Backend de integración con Salesforce REST API usando Clean Architecture y Polly para resiliencia"
    });
});

// Health Checks (monitoreo)
builder.Services.AddHealthChecks()
    .AddCheck<SalesforceHealthCheck>("Salesforce");

// Logging estructurado
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsProduction())
{
    // En producción: integrar con Application Insights
    // builder.Services.AddApplicationInsightsTelemetry();
}

// ========================================
// BUILD Y CONFIGURACIÓN DE MIDDLEWARE
// ========================================

var app = builder.Build();

// Configurar pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Endpoint de health check
app.MapHealthChecks("/health");

// Logging de inicio
app.Logger.LogInformation("Aplicación iniciada en ambiente: {Environment}", app.Environment.EnvironmentName);
app.Logger.LogInformation("Salesforce API Base URL: {BaseUrl}", 
    app.Services.GetRequiredService<SalesforceConfiguration>().GetApiBaseUrl());

// ========================================
// EJECUTAR APLICACIÓN
// ========================================

app.Run();

// ========================================
// HEALTH CHECK PERSONALIZADO
// ========================================

/// <summary>
/// Health check que verifica conectividad con Salesforce.
/// Ejecutado por Azure App Service, Kubernetes, Load Balancers.
/// </summary>
public class SalesforceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly ISalesforceClient _salesforceClient;
    private readonly ILogger<SalesforceHealthCheck> _logger;

    public SalesforceHealthCheck(
        ISalesforceClient salesforceClient,
        ILogger<SalesforceHealthCheck> logger)
    {
        _salesforceClient = salesforceClient;
        _logger = logger;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await _salesforceClient.CheckConnectionAsync(cancellationToken);

            if (isConnected)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    "Conexión con Salesforce exitosa");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                "Conexión con Salesforce falló pero la aplicación puede continuar");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check de Salesforce falló");

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Salesforce no disponible",
                ex);
        }
    }
}