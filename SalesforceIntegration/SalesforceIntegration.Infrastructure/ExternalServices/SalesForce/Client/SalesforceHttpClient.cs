using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using SalesforceIntegration.Application.DTOs;
using SalesforceIntegration.Application.Interfaces;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Auth;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Configuration;

namespace SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Client;

/// <summary>
/// Cliente HTTP tipado para comunicación con Salesforce REST API.
/// Implementa:
/// - Construcción de queries SOQL
/// - Autenticación automática con tokens OAuth2
/// - Paginación transparente de resultados
/// - Manejo de errores específicos de Salesforce
/// - Resiliencia mediante Polly (configurado externamente en IHttpClientFactory)
/// </summary>
public class SalesforceHttpClient : ISalesforceClient
{
    private readonly HttpClient _httpClient;
    private readonly ISalesforceAuthService _authService;
    private readonly SalesforceConfiguration _configuration;
    private readonly ILogger<SalesforceHttpClient> _logger;

    public SalesforceHttpClient(
        HttpClient httpClient,
        ISalesforceAuthService authService,
        SalesforceConfiguration configuration,
        ILogger<SalesforceHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configurar base address del HttpClient
        // Esto permite usar rutas relativas en los requests
        _httpClient.BaseAddress = new Uri(_configuration.GetApiBaseUrl());
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
    }

    /// <summary>
    /// Obtiene cuentas modificadas desde una fecha específica.
    /// Implementa paginación automática si hay más de 2000 registros.
    /// </summary>
    public async Task<IEnumerable<SalesforceAccountDto>> GetAccountsModifiedSinceAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Consultando cuentas modificadas desde {ModifiedSince}",
            modifiedSince);

        // Construir query SOQL (Salesforce Object Query Language)
        // Similar a SQL pero con sintaxis específica de Salesforce
        var soql = BuildAccountsQuery(modifiedSince);

        _logger.LogDebug("Query SOQL: {Soql}", soql);

        // Obtener todos los registros manejando paginación automáticamente
        var allAccounts = new List<SalesforceAccountDto>();
        string? nextRecordsUrl = null;
        int pageNumber = 1;

        do
        {
            SalesforceQueryResponse<SalesforceAccountDto> response;

            if (string.IsNullOrEmpty(nextRecordsUrl))
            {
                // Primera página: usar query SOQL
                response = await ExecuteQueryAsync<SalesforceAccountDto>(
                    soql,
                    cancellationToken);
            }
            else
            {
                // Páginas siguientes: usar nextRecordsUrl
                response = await FetchNextPageAsync<SalesforceAccountDto>(
                    nextRecordsUrl,
                    cancellationToken);
            }

            allAccounts.AddRange(response.Records);

            _logger.LogInformation(
                "Página {PageNumber}: {Count} registros obtenidos. Total acumulado: {Total}",
                pageNumber,
                response.Records.Count,
                allAccounts.Count);

            nextRecordsUrl = response.Done ? null : response.NextRecordsUrl;
            pageNumber++;

        } while (!string.IsNullOrEmpty(nextRecordsUrl));

        _logger.LogInformation(
            "Consulta completada. Total de cuentas obtenidas: {Total}",
            allAccounts.Count);

        return allAccounts;
    }

    /// <summary>
    /// Obtiene una cuenta específica por su ID de Salesforce.
    /// </summary>
    public async Task<SalesforceAccountDto?> GetAccountByIdAsync(
        string salesforceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(salesforceId))
            throw new ArgumentException("Salesforce ID no puede estar vacío", nameof(salesforceId));

        _logger.LogInformation("Consultando cuenta con ID: {SalesforceId}", salesforceId);

        try
        {
            // REST endpoint específico para un objeto
            // GET /sobjects/Account/{id}
            var requestUri = $"sobjects/Account/{salesforceId}";

            var response = await ExecuteAuthenticatedRequestAsync(
                HttpMethod.Get,
                requestUri,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Cuenta {SalesforceId} no encontrada", salesforceId);
                return null;
            }

            await EnsureSuccessStatusCodeAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var account = JsonSerializer.Deserialize<SalesforceAccountDto>(content);

            return account;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo cuenta {SalesforceId}", salesforceId);
            throw;
        }
    }

    /// <summary>
    /// Verifica la conexión con Salesforce.
    /// Realiza un query simple para validar autenticación y conectividad.
    /// </summary>
    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verificando conexión con Salesforce");

            // Query mínimo para verificar conectividad
            var soql = "SELECT Id FROM Account LIMIT 1";

            await ExecuteQueryAsync<SalesforceAccountDto>(soql, cancellationToken);

            _logger.LogInformation("Conexión con Salesforce verificada exitosamente");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo verificación de conexión con Salesforce");
            return false;
        }
    }

    /// <summary>
    /// Ejecuta un query SOQL y retorna la respuesta tipada.
    /// Maneja autenticación automática y reintentos por token expirado.
    /// </summary>
    private async Task<SalesforceQueryResponse<T>> ExecuteQueryAsync<T>(
        string soql,
        CancellationToken cancellationToken)
    {
        // Codificar query para URL
        var encodedQuery = HttpUtility.UrlEncode(soql);
        var requestUri = $"query?q={encodedQuery}";

        var response = await ExecuteAuthenticatedRequestAsync(
            HttpMethod.Get,
            requestUri,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var queryResponse = JsonSerializer.Deserialize<SalesforceQueryResponse<T>>(content);

        if (queryResponse == null)
        {
            throw new SalesforceApiException("Respuesta de query SOQL inválida: JSON nulo");
        }

        return queryResponse;
    }

    /// <summary>
    /// Obtiene la siguiente página de resultados usando nextRecordsUrl.
    /// </summary>
    private async Task<SalesforceQueryResponse<T>> FetchNextPageAsync<T>(
        string nextRecordsUrl,
        CancellationToken cancellationToken)
    {
        // nextRecordsUrl es una ruta relativa: /services/data/v58.0/query/01gxx...
        // Remover el prefijo base para usar ruta relativa
        var baseUrl = _configuration.GetApiBaseUrl();
        var relativePath = nextRecordsUrl.Replace(baseUrl, "").TrimStart('/');

        var response = await ExecuteAuthenticatedRequestAsync(
            HttpMethod.Get,
            relativePath,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var queryResponse = JsonSerializer.Deserialize<SalesforceQueryResponse<T>>(content);

        if (queryResponse == null)
        {
            throw new SalesforceApiException("Respuesta de paginación inválida: JSON nulo");
        }

        return queryResponse;
    }

    /// <summary>
    /// Ejecuta un request HTTP con autenticación OAuth2.
    /// Si recibe 401, renueva el token y reintenta una vez.
    /// </summary>
    private async Task<HttpResponseMessage> ExecuteAuthenticatedRequestAsync(
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken,
        bool isRetry = false)
    {
        // Obtener token de acceso (cacheado automáticamente)
        var accessToken = await _authService.GetAccessTokenAsync(cancellationToken);

        // Crear request con header de autorización
        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Ejecutar request (Polly policies aplicadas automáticamente)
        var response = await _httpClient.SendAsync(request, cancellationToken);

        // Manejo específico de 401 Unauthorized
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _logger.LogWarning("Token expirado (401). Invalidando cache y reintentando...");

            // Invalidar token cacheado para forzar renovación
            _authService.InvalidateToken();

            // Reintentar una sola vez con token renovado
            return await ExecuteAuthenticatedRequestAsync(
                method,
                requestUri,
                cancellationToken,
                isRetry: true);
        }

        return response;
    }

    /// <summary>
    /// Valida que la respuesta HTTP sea exitosa.
    /// Lanza excepciones específicas según el código de estado.
    /// </summary>
    private async Task EnsureSuccessStatusCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        // Parsear errores de Salesforce (formato JSON)
        SalesforceError? error = null;
        try
        {
            var errors = JsonSerializer.Deserialize<List<SalesforceError>>(content);
            error = errors?.FirstOrDefault();
        }
        catch
        {
            // Si no es JSON válido, usar el content raw
        }

        var errorMessage = error?.Message ?? $"Error HTTP {response.StatusCode}";
        var errorCode = error?.ErrorCode;

        _logger.LogError(
            "Request falló. Status: {Status}, ErrorCode: {ErrorCode}, Message: {Message}",
            response.StatusCode,
            errorCode,
            errorMessage);

        // Lanzar excepciones específicas según status code
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new SalesforceAuthenticationException(
                    $"Autenticación fallida: {errorMessage}");

            case (HttpStatusCode)429:
                var retryAfter = GetRetryAfterSeconds(response);
                throw new SalesforceRateLimitException(
                    $"Rate limit excedido: {errorMessage}",
                    retryAfter,
                    content);

            case HttpStatusCode.BadRequest:
                throw new SalesforceQueryException(
                    $"Query inválido: {errorMessage}",
                    errorCode,
                    content);

            case >= HttpStatusCode.InternalServerError:
                throw new SalesforceServerException(
                    $"Error del servidor Salesforce: {errorMessage}",
                    response.StatusCode,
                    content);

            default:
                throw new SalesforceApiException(
                    $"Error inesperado: {errorMessage}",
                    response.StatusCode,
                    errorCode,
                    content);
        }
    }

    /// <summary>
    /// Extrae el valor del header Retry-After.
    /// </summary>
    private int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var retryAfterValue = values.FirstOrDefault();
            if (int.TryParse(retryAfterValue, out var seconds))
            {
                return seconds;
            }
        }

        // Default: 60 segundos si no hay header
        return 60;
    }

    /// <summary>
    /// Construye el query SOQL para obtener cuentas modificadas.
    /// </summary>
    private string BuildAccountsQuery(DateTime modifiedSince)
    {
        // Formatear fecha en formato ISO 8601 requerido por Salesforce
        var modifiedSinceStr = modifiedSince.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        // Query SOQL: similar a SQL pero con sintaxis de Salesforce
        var soql = $@"
            SELECT 
                Id,
                Name,
                Type,
                Industry,
                AnnualRevenue,
                NumberOfEmployees,
                BillingCity,
                BillingCountry,
                LastModifiedDate,
                CreatedDate,
                IsDeleted
            FROM Account
            WHERE LastModifiedDate >= {modifiedSinceStr}
            ORDER BY LastModifiedDate ASC";

        // Remover saltos de línea y espacios extras
        return string.Join(" ", soql.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }
}

/// <summary>
/// Estructura de error retornada por Salesforce API.
/// Salesforce retorna errores en formato JSON array.
/// </summary>
internal class SalesforceError
{
    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("fields")]
    public List<string>? Fields { get; set; }
}