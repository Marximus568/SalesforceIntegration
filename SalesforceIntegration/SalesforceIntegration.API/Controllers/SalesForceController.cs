using Microsoft.AspNetCore.Mvc;
using SalesforceIntegration.Application.Commands;
using SalesforceIntegration.Application.Interfaces;

namespace SalesforceIntegration.API.Controllers;

/// <summary>
/// Controlador para operaciones de integración con Salesforce.
/// Expone endpoints para sincronización manual y consultas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SalesforceController : ControllerBase
{
    private readonly ISalesforceClient _salesforceClient;
    private readonly SyncAccountsCommand _syncCommand;
    private readonly ILogger<SalesforceController> _logger;

    public SalesforceController(
        ISalesforceClient salesforceClient,
        SyncAccountsCommand syncCommand,
        ILogger<SalesforceController> logger)
    {
        _salesforceClient = salesforceClient ?? throw new ArgumentNullException(nameof(salesforceClient));
        _syncCommand = syncCommand ?? throw new ArgumentNullException(nameof(syncCommand));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ejecuta sincronización manual de cuentas modificadas.
    /// </summary>
    /// <param name="hoursBack">Horas hacia atrás para sincronizar (default: 24)</param>
    /// <returns>Resultado de la sincronización con estadísticas</returns>
    /// <response code="200">Sincronización completada exitosamente</response>
    /// <response code="400">Parámetros inválidos</response>
    /// <response code="500">Error durante sincronización</response>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncAccounts(
        [FromQuery] int hoursBack = 24,
        CancellationToken cancellationToken = default)
    {
        if (hoursBack < 1 || hoursBack > 720) // Max 30 días
        {
            return BadRequest(new { error = "hoursBack debe estar entre 1 y 720 horas (30 días)" });
        }

        _logger.LogInformation(
            "Sincronización manual solicitada. Horas hacia atrás: {HoursBack}",
            hoursBack);

        try
        {
            var modifiedSince = DateTime.UtcNow.AddHours(-hoursBack);
            
            var result = await _syncCommand.ExecuteAsync(modifiedSince, cancellationToken);

            var dto = new SyncResultDto
            {
                Success = result.Success,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                DurationMs = result.Duration.TotalMilliseconds,
                ModifiedSince = result.ModifiedSince,
                TotalRecordsFromSalesforce = result.TotalRecordsFromSalesforce,
                SuccessfullyMapped = result.SuccessfullyMapped,
                FailedMappings = result.FailedMappings,
                ProcessedAccounts = result.ProcessedAccounts,
                MappingSuccessRate = result.MappingSuccessRate,
                ErrorMessage = result.ErrorMessage
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante sincronización manual");
            return StatusCode(500, new { error = "Error durante sincronización", details = ex.Message });
        }
    }

    /// <summary>
    /// Consulta una cuenta específica de Salesforce por ID.
    /// </summary>
    /// <param name="salesforceId">ID de la cuenta en Salesforce</param>
    /// <returns>Datos de la cuenta</returns>
    /// <response code="200">Cuenta encontrada</response>
    /// <response code="404">Cuenta no encontrada</response>
    /// <response code="500">Error consultando Salesforce</response>
    [HttpGet("accounts/{salesforceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAccount(
        string salesforceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Consultando cuenta: {SalesforceId}", salesforceId);

            var account = await _salesforceClient.GetAccountByIdAsync(salesforceId, cancellationToken);

            if (account == null)
            {
                return NotFound(new { error = $"Cuenta {salesforceId} no encontrada" });
            }

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando cuenta {SalesforceId}", salesforceId);
            return StatusCode(500, new { error = "Error consultando Salesforce", details = ex.Message });
        }
    }

    /// <summary>
    /// Verifica conectividad con Salesforce.
    /// </summary>
    /// <returns>Status de conexión</returns>
    /// <response code="200">Conexión exitosa</response>
    /// <response code="503">Salesforce no disponible</response>
    [HttpGet("connection/test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await _salesforceClient.CheckConnectionAsync(cancellationToken);

            if (isConnected)
            {
                return Ok(new { status = "connected", message = "Conexión con Salesforce exitosa" });
            }

            return StatusCode(503, new { status = "disconnected", message = "No se pudo conectar con Salesforce" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando conexión con Salesforce");
            return StatusCode(503, new { status = "error", message = ex.Message });
        }
    }
}

/// <summary>
/// DTO para resultado de sincronización expuesto por API.
/// </summary>
public class SyncResultDto
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationMs { get; set; }
    public DateTime ModifiedSince { get; set; }
    public int TotalRecordsFromSalesforce { get; set; }
    public int SuccessfullyMapped { get; set; }
    public int FailedMappings { get; set; }
    public int ProcessedAccounts { get; set; }
    public double MappingSuccessRate { get; set; }
    public string? ErrorMessage { get; set; }
}