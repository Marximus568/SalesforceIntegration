using Microsoft.Extensions.Logging;
using SalesforceIntegration.Application.Interfaces;
using SalesforceIntegration.Application.Mappings;
using SalesforceIntegration.Domain.Entities;

namespace SalesforceIntegration.Application.Commands;

/// <summary>
/// Comando que implementa el caso de uso "Sincronizar cuentas desde Salesforce".
/// Orquesta la obtención de datos, transformación y persistencia.
/// No contiene lógica de HTTP ni persistencia, solo coordina servicios.
/// </summary>
public class SyncAccountsCommand
{
    private readonly ISalesforceClient _salesforceClient;
    private readonly ILogger<SyncAccountsCommand> _logger;

    public SyncAccountsCommand(
        ISalesforceClient salesforceClient,
        ILogger<SyncAccountsCommand> logger)
    {
        _salesforceClient = salesforceClient ?? throw new ArgumentNullException(nameof(salesforceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ejecuta la sincronización de cuentas modificadas desde una fecha específica.
    /// </summary>
    /// <param name="modifiedSince">Fecha desde la cual sincronizar modificaciones.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado de la sincronización con estadísticas.</returns>
    public async Task<SyncResult> ExecuteAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Iniciando sincronización de cuentas modificadas desde {ModifiedSince}",
            modifiedSince);

        var result = new SyncResult
        {
            StartTime = DateTime.UtcNow,
            ModifiedSince = modifiedSince
        };

        try
        {
            // 1. Obtener cuentas desde Salesforce con resiliencia automática
            var salesforceDtos = await _salesforceClient.GetAccountsModifiedSinceAsync(
                modifiedSince,
                cancellationToken);

            result.TotalRecordsFromSalesforce = salesforceDtos.Count();

            _logger.LogInformation(
                "Obtenidos {Count} registros desde Salesforce",
                result.TotalRecordsFromSalesforce);

            // 2. Transformar DTOs a entidades de dominio
            // El mapper filtra registros eliminados y maneja errores de mapeo
            var accounts = AccountMapper.MapToDomain(salesforceDtos).ToList();

            result.SuccessfullyMapped = accounts.Count;
            result.FailedMappings = result.TotalRecordsFromSalesforce - result.SuccessfullyMapped;

            _logger.LogInformation(
                "Mapeadas {Successful} cuentas correctamente. {Failed} fallaron",
                result.SuccessfullyMapped,
                result.FailedMappings);

            // 3. Procesar cuentas (persistir, enviar a Data Lake, etc.)
            result.ProcessedAccounts = await ProcessAccountsAsync(accounts, cancellationToken);

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Success = true;

            _logger.LogInformation(
                "Sincronización completada. {Processed} cuentas procesadas en {Duration}ms",
                result.ProcessedAccounts,
                result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Success = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(
                ex,
                "Error durante sincronización de cuentas. Duración: {Duration}ms",
                result.Duration.TotalMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Procesa las cuentas mapeadas.
    /// Implementación actual: solo loguea, pero aquí iría la persistencia o envío a Data Lake.
    /// </summary>
    /// <param name="accounts">Cuentas a procesar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Cantidad de cuentas procesadas.</returns>
    private async Task<int> ProcessAccountsAsync(
        IEnumerable<Account> accounts,
        CancellationToken cancellationToken)
    {
        // En producción, aquí iría:
        // - Upsert en base de datos local (IAccountRepository)
        // - Envío a Azure Event Hub para Data Lake
        // - Publicación de eventos de dominio
        // - Actualización de cache

        int processed = 0;

        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulación de procesamiento
            _logger.LogDebug(
                "Procesando cuenta: {ExternalId} - {Name}",
                account.ExternalId,
                account.Name);

            processed++;

            // Simular latencia de persistencia
            await Task.Delay(10, cancellationToken);
        }

        return processed;
    }
}

/// <summary>
/// Resultado de la ejecución de sincronización.
/// Contiene estadísticas y métricas para monitoreo.
/// </summary>
public class SyncResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime ModifiedSince { get; set; }
    public int TotalRecordsFromSalesforce { get; set; }
    public int SuccessfullyMapped { get; set; }
    public int FailedMappings { get; set; }
    public int ProcessedAccounts { get; set; }

    /// <summary>
    /// Calcula el porcentaje de éxito del mapeo.
    /// </summary>
    public double MappingSuccessRate =>
        TotalRecordsFromSalesforce > 0
            ? (double)SuccessfullyMapped / TotalRecordsFromSalesforce * 100
            : 0;
}