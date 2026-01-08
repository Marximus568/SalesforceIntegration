using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesforceIntegration.Application.Commands;

namespace SalesforceIntegration.Infrastructure.BackgroundServices;

/// <summary>
/// Servicio background que ejecuta sincronización periódica con Salesforce.
/// Se ejecuta continuamente cada X minutos mientras la aplicación esté activa.
/// 
/// CUÁNDO USAR BackgroundService vs Azure Function:
/// - BackgroundService: Sincronizaciones frecuentes (cada 5-15 min), bajo costo
/// - Azure Function: Sincronizaciones espaciadas (cada hora/día), serverless
/// 
/// Este servicio requiere que la aplicación esté siempre activa (Always On en App Service).
/// </summary>
public class SalesforceSyncBackgroundService : BackgroundService
{
    private readonly ILogger<SalesforceSyncBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _syncInterval;

    // Tracking de última sincronización exitosa
    private DateTime _lastSuccessfulSync;

    public SalesforceSyncBackgroundService(
        ILogger<SalesforceSyncBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Intervalo de sincronización: cada 5 minutos
        // En producción, configurar desde appsettings.json
        _syncInterval = TimeSpan.FromMinutes(5);

        // Inicializar última sincronización hace 24 horas
        // Primera ejecución traerá últimas 24h de modificaciones
        _lastSuccessfulSync = DateTime.UtcNow.AddHours(-24);
    }

    /// <summary>
    /// Método principal que se ejecuta al iniciar el servicio.
    /// Loop infinito que ejecuta sincronización cada X minutos.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SalesforceSyncBackgroundService iniciado. Intervalo de sincronización: {Interval} minutos",
            _syncInterval.TotalMinutes);

        // Esperar 30 segundos antes de la primera sincronización
        // Permite que otros servicios (DbContext, HttpClient) se inicialicen completamente
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteSyncCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // NUNCA dejar que una excepción detenga el BackgroundService
                _logger.LogError(
                    ex,
                    "Error crítico en ciclo de sincronización. El servicio continuará ejecutándose");
            }

            // Esperar hasta el próximo ciclo
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // La aplicación se está deteniendo
                _logger.LogInformation("Sincronización cancelada: aplicación deteniéndose");
            }
        }

        _logger.LogInformation("SalesforceSyncBackgroundService detenido");
    }

    /// <summary>
    /// Ejecuta un ciclo completo de sincronización.
    /// Usa Dependency Injection con scope para resolver dependencias transient/scoped.
    /// </summary>
    private async Task ExecuteSyncCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Iniciando ciclo de sincronización. Última sincronización exitosa: {LastSync}",
            _lastSuccessfulSync);

        // IMPORTANTE: BackgroundService es singleton, pero los comandos y clientes son scoped/transient
        // Debemos crear un scope para resolver dependencias correctamente
        using var scope = _serviceProvider.CreateScope();
        
        // Resolver el comando desde el scope
        var syncCommand = scope.ServiceProvider.GetRequiredService<SyncAccountsCommand>();

        try
        {
            // Ejecutar sincronización desde la última fecha exitosa
            var result = await syncCommand.ExecuteAsync(
                _lastSuccessfulSync,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Sincronización completada exitosamente. " +
                    "Registros: {Total}, Mapeados: {Mapped}, Procesados: {Processed}, Duración: {Duration}ms",
                    result.TotalRecordsFromSalesforce,
                    result.SuccessfullyMapped,
                    result.ProcessedAccounts,
                    result.Duration.TotalMilliseconds);

                // Actualizar tracking de última sincronización exitosa
                _lastSuccessfulSync = DateTime.UtcNow;
            }
            else
            {
                _logger.LogError(
                    "Sincronización falló. Error: {Error}",
                    result.ErrorMessage);

                // NO actualizar _lastSuccessfulSync
                // Próximo ciclo volverá a intentar desde la misma fecha
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Excepción durante sincronización desde {LastSync}",
                _lastSuccessfulSync);

            // NO actualizar _lastSuccessfulSync
            // Próximo ciclo reintentará desde la misma fecha
        }
    }

    /// <summary>
    /// Se ejecuta cuando la aplicación se está deteniendo.
    /// Permite cleanup graceful.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deteniendo SalesforceSyncBackgroundService...");

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("SalesforceSyncBackgroundService detenido completamente");
    }
}