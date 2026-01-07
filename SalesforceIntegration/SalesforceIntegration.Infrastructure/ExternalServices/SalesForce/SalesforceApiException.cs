using System.Net;

namespace SalesforceIntegration.Infrastructure.ExternalServices.Salesforce;

/// <summary>
/// Excepción base para errores de Salesforce API.
/// </summary>
public class SalesforceApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? SalesforceErrorCode { get; }
    public string? ResponseBody { get; }

    public SalesforceApiException(
        string message,
        HttpStatusCode? statusCode = null,
        string? salesforceErrorCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        SalesforceErrorCode = salesforceErrorCode;
        ResponseBody = responseBody;
    }
}

/// <summary>
/// Excepción lanzada cuando falla la autenticación OAuth2.
/// StatusCode: 401 Unauthorized.
/// Indica que el token expiró o las credenciales son inválidas.
/// </summary>
public class SalesforceAuthenticationException : SalesforceApiException
{
    public SalesforceAuthenticationException(
        string message,
        Exception? innerException = null)
        : base(message, HttpStatusCode.Unauthorized, null, null, innerException)
    {
    }
}

/// <summary>
/// Excepción lanzada cuando se excede el rate limit de Salesforce.
/// StatusCode: 429 Too Many Requests.
/// Salesforce retorna un header "Retry-After" indicando cuándo reintentar.
/// </summary>
public class SalesforceRateLimitException : SalesforceApiException
{
    /// <summary>
    /// Tiempo en segundos que debe esperarse antes de reintentar.
    /// Leído desde el header "Retry-After" de la respuesta.
    /// </summary>
    public int RetryAfterSeconds { get; }

    public SalesforceRateLimitException(
        string message,
        int retryAfterSeconds,
        string? responseBody = null)
        : base(message, (HttpStatusCode)429, "REQUEST_LIMIT_EXCEEDED", responseBody)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

/// <summary>
/// Excepción lanzada cuando Salesforce retorna error de servidor (5xx).
/// Estos errores son típicamente transitorios y reintentar puede funcionar.
/// </summary>
public class SalesforceServerException : SalesforceApiException
{
    public SalesforceServerException(
        string message,
        HttpStatusCode statusCode,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, statusCode, null, responseBody, innerException)
    {
    }
}

/// <summary>
/// Excepción lanzada cuando el query SOQL es inválido o hay error de negocio.
/// StatusCode: 400 Bad Request.
/// Indica error en el request que no se resuelve reintentando.
/// </summary>
public class SalesforceQueryException : SalesforceApiException
{
    public SalesforceQueryException(
        string message,
        string? salesforceErrorCode = null,
        string? responseBody = null)
        : base(message, HttpStatusCode.BadRequest, salesforceErrorCode, responseBody)
    {
    }
}