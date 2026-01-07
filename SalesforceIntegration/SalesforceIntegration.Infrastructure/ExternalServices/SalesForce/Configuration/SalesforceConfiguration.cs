using System.ComponentModel.DataAnnotations;

namespace SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Configuration;

/// <summary>
/// Configuración de conexión a Salesforce API.
/// Se mapea desde appsettings.json o variables de entorno.
/// Las validaciones previenen errores en runtime por configuración inválida.
/// </summary>
public class SalesforceConfiguration
{
    /// <summary>
    /// Sección en appsettings.json donde se encuentra la configuración.
    /// Ejemplo: "Salesforce": { "InstanceUrl": "...", ... }
    /// </summary>
    public const string SectionName = "Salesforce";

    /// <summary>
    /// URL de la instancia de Salesforce.
    /// Producción: https://login.salesforce.com
    /// Sandbox: https://test.salesforce.com
    /// </summary>
    [Required(ErrorMessage = "InstanceUrl es obligatorio")]
    [Url(ErrorMessage = "InstanceUrl debe ser una URL válida")]
    public string InstanceUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL del endpoint de OAuth2 token.
    /// Típicamente: {InstanceUrl}/services/oauth2/token
    /// </summary>
    [Required(ErrorMessage = "TokenEndpoint es obligatorio")]
    [Url(ErrorMessage = "TokenEndpoint debe ser una URL válida")]
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Client ID de la aplicación conectada (Connected App).
    /// Se obtiene al crear una Connected App en Salesforce Setup.
    /// </summary>
    [Required(ErrorMessage = "ClientId es obligatorio")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client Secret de la aplicación conectada.
    /// NUNCA debe exponerse en código fuente.
    /// Usar Azure Key Vault o variables de entorno en producción.
    /// </summary>
    [Required(ErrorMessage = "ClientSecret es obligatorio")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Usuario de Salesforce para autenticación.
    /// Debe ser un usuario de integración dedicado, NO un usuario humano.
    /// </summary>
    [Required(ErrorMessage = "Username es obligatorio")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña del usuario.
    /// NUNCA debe exponerse en código fuente.
    /// </summary>
    [Required(ErrorMessage = "Password es obligatorio")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Security Token del usuario.
    /// Salesforce lo requiere cuando se conecta desde IPs no confiables.
    /// Se concatena con la contraseña: Password + SecurityToken.
    /// </summary>
    public string? SecurityToken { get; set; }

    /// <summary>
    /// Versión de la API de Salesforce a usar.
    /// Ejemplo: "v58.0"
    /// </summary>
    [Required(ErrorMessage = "ApiVersion es obligatorio")]
    [RegularExpression(@"^v\d+\.\d+$", ErrorMessage = "ApiVersion debe tener formato vXX.X (ejemplo: v58.0)")]
    public string ApiVersion { get; set; } = "v58.0";

    /// <summary>
    /// Timeout para requests HTTP en segundos.
    /// Default: 30 segundos.
    /// Queries complejas pueden requerir más tiempo.
    /// </summary>
    [Range(5, 300, ErrorMessage = "Timeout debe estar entre 5 y 300 segundos")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Máximo de reintentos para errores transitorios.
    /// Usado por Polly policies.
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries debe estar entre 0 y 10")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Duración en segundos para abrir el circuit breaker tras fallos consecutivos.
    /// </summary>
    [Range(10, 300, ErrorMessage = "CircuitBreakerDurationSeconds debe estar entre 10 y 300")]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Construye la URL base de la API.
    /// Ejemplo: https://yourinstance.salesforce.com/services/data/v58.0
    /// </summary>
    public string GetApiBaseUrl()
    {
        return $"{InstanceUrl.TrimEnd('/')}/services/data/{ApiVersion}";
    }

    /// <summary>
    /// Obtiene la contraseña completa concatenando Password + SecurityToken.
    /// Salesforce requiere esta concatenación cuando no hay IP whitelisting.
    /// </summary>
    public string GetFullPassword()
    {
        return string.IsNullOrEmpty(SecurityToken)
            ? Password
            : Password + SecurityToken;
    }

    /// <summary>
    /// Valida que la configuración tenga todos los valores requeridos.
    /// Lanza ValidationException si hay errores.
    /// </summary>
    public void Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, validateAllProperties: true))
        {
            var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Configuración de Salesforce inválida: {errors}");
        }
    }
}