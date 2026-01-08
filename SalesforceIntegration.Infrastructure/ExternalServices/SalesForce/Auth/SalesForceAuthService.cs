using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Configuration;

namespace SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Auth;

/// <summary>
/// Contrato para el servicio de autenticación OAuth2 de Salesforce.
/// </summary>
public interface ISalesforceAuthService
{
    /// <summary>
    /// Obtiene un token de acceso válido.
    /// Si el token está cacheado y no ha expirado, lo retorna.
    /// Si expiró o no existe, solicita uno nuevo a Salesforce.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida el token cacheado forzando renovación en el próximo request.
    /// Usado cuando recibimos 401 Unauthorized indicando token expirado.
    /// </summary>
    void InvalidateToken();
}

/// <summary>
/// Servicio que gestiona autenticación OAuth2 con Salesforce.
/// Implementa Client Credentials Flow con cacheo de tokens.
/// Previene solicitar token en cada request, reduciendo latencia y evitando rate limits.
/// </summary>
public class SalesforceAuthService : ISalesforceAuthService
{
    private const string TokenCacheKey = "Salesforce_AccessToken";
    private const int TokenExpirationBufferMinutes = 5; // Renovar 5 min antes de expirar

    private readonly HttpClient _httpClient;
    private readonly SalesforceConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SalesforceAuthService> _logger;

    // Lock para prevenir múltiples requests simultáneos de token
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public SalesforceAuthService(
        HttpClient httpClient,
        SalesforceConfiguration configuration,
        IMemoryCache cache,
        ILogger<SalesforceAuthService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtiene token de acceso con cacheo automático.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Intentar obtener del cache
        if (_cache.TryGetValue<TokenCacheEntry>(TokenCacheKey, out var cachedEntry))
        {
            // Verificar si el token sigue siendo válido (con buffer de seguridad)
            if (cachedEntry!.ExpiresAt > DateTime.UtcNow.AddMinutes(TokenExpirationBufferMinutes))
            {
                _logger.LogDebug("Token recuperado desde cache. Expira en: {ExpiresAt}", cachedEntry.ExpiresAt);
                return cachedEntry.AccessToken;
            }

            _logger.LogInformation("Token cacheado próximo a expirar. Renovando...");
        }

        // Token no existe o está próximo a expirar: solicitar nuevo
        return await RequestNewTokenAsync(cancellationToken);
    }

    /// <summary>
    /// Solicita un nuevo token a Salesforce.
    /// Usa SemaphoreSlim para prevenir múltiples requests simultáneos.
    /// </summary>
    private async Task<string> RequestNewTokenAsync(CancellationToken cancellationToken)
    {
        // Esperar a obtener el lock
        await _tokenLock.WaitAsync(cancellationToken);

        try
        {
            // Double-check: otro thread pudo haber obtenido el token mientras esperábamos
            if (_cache.TryGetValue<TokenCacheEntry>(TokenCacheKey, out var cachedEntry))
            {
                if (cachedEntry!.ExpiresAt > DateTime.UtcNow.AddMinutes(TokenExpirationBufferMinutes))
                {
                    _logger.LogDebug("Token ya renovado por otro thread");
                    return cachedEntry.AccessToken;
                }
            }

            _logger.LogInformation("Solicitando nuevo token OAuth2 a Salesforce");

            // Construir request OAuth2 Client Credentials Flow
            var requestBody = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", _configuration.ClientId },
                { "client_secret", _configuration.ClientSecret },
                { "username", _configuration.Username },
                { "password", _configuration.GetFullPassword() } // Password + SecurityToken
            };

            var content = new FormUrlEncodedContent(requestBody);

            // Realizar request
            var response = await _httpClient.PostAsync(
                _configuration.TokenEndpoint,
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Error obteniendo token OAuth2. Status: {Status}, Body: {Body}",
                    response.StatusCode,
                    errorBody);

                throw new SalesforceAuthenticationException(
                    $"Fallo autenticación OAuth2: {response.StatusCode}");
            }

            // Deserializar respuesta
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(responseBody);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                throw new SalesforceAuthenticationException("Respuesta OAuth2 inválida: access_token vacío");
            }

            // Calcular fecha de expiración (Salesforce NO retorna expires_in, asumir 2 horas)
            var expiresAt = DateTime.UtcNow.AddHours(2);

            // Cachear token
            var cacheEntry = new TokenCacheEntry
            {
                AccessToken = tokenResponse.AccessToken,
                InstanceUrl = tokenResponse.InstanceUrl ?? _configuration.InstanceUrl,
                ExpiresAt = expiresAt
            };

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt
            };

            _cache.Set(TokenCacheKey, cacheEntry, cacheOptions);

            _logger.LogInformation("Token OAuth2 obtenido y cacheado. Expira: {ExpiresAt}", expiresAt);

            return cacheEntry.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Invalida el token cacheado.
    /// Debe llamarse cuando recibimos 401 Unauthorized.
    /// </summary>
    public void InvalidateToken()
    {
        _logger.LogWarning("Token invalidado manualmente");
        _cache.Remove(TokenCacheKey);
    }
}

/// <summary>
/// Respuesta de Salesforce al endpoint OAuth2 token.
/// </summary>
internal class OAuthTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("instance_url")]
    public string? InstanceUrl { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [System.Text.Json.Serialization.JsonPropertyName("issued_at")]
    public string? IssuedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("signature")]
    public string? Signature { get; set; }
}

/// <summary>
/// Entrada de cache para el token OAuth2.
/// </summary>
internal class TokenCacheEntry
{
    public string AccessToken { get; set; } = string.Empty;
    public string InstanceUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}