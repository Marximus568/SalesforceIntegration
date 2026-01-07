using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce;
using SalesforceIntegration.Infrastructure.ExternalServices.Salesforce.Configuration;

namespace SalesforceIntegration.Infrastructure.Resilience;

/// <summary>
/// Fábrica de políticas de resiliencia Polly para integraciones HTTP.
/// Define estrategias de retry, circuit breaker y manejo de rate limits.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Crea una política de retry con backoff exponencial.
    /// Reintenta automáticamente errores transitorios (5xx, timeouts).
    /// Espera incrementalmente: 2s → 4s → 8s entre reintentos.
    /// </summary>
    /// <param name="config">Configuración de Salesforce (contiene MaxRetries).</param>
    /// <param name="logger">Logger para registrar reintentos.</param>
    public static AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy(
        SalesforceConfiguration config,
        ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Maneja 5xx y timeouts automáticamente
            .Or<HttpRequestException>() // Maneja errores de red (DNS, conexión)
            .OrResult(response => response.StatusCode == (HttpStatusCode)429) // Rate limit
            .WaitAndRetryAsync(
                retryCount: config.MaxRetries,
                sleepDurationProvider: retryAttempt =>
                {
                    // Backoff exponencial: 2^retry * 1 segundo
                    var exponentialDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));

                    // Agregar jitter (aleatoriedad) para prevenir thundering herd
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

                    return exponentialDelay + jitter;
                },
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "Exception";

                    logger.LogWarning(
                        "Reintento {RetryAttempt}/{MaxRetries} después de {Delay}ms. Razón: {StatusCode}",
                        retryAttempt,
                        config.MaxRetries,
                        timespan.TotalMilliseconds,
                        statusCode);

                    // Si es 429, leer header Retry-After
                    if (outcome.Result?.StatusCode == (HttpStatusCode)429)
                    {
                        if (outcome.Result.Headers.TryGetValues("Retry-After", out var values))
                        {
                            var retryAfter = values.FirstOrDefault();
                            logger.LogWarning(
                                "Rate limit detectado. Salesforce indica esperar: {RetryAfter} segundos",
                                retryAfter);
                        }
                    }
                });
    }

    /// <summary>
    /// Crea una política de Circuit Breaker.
    /// Tras N fallos consecutivos, "abre el circuito" deteniendo requests por X segundos.
    /// Esto previene bombardear una API que está caída.
    /// </summary>
    /// <param name="config">Configuración de Salesforce.</param>
    /// <param name="logger">Logger para eventos del circuit breaker.</param>
    public static AsyncCircuitBreakerPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(
        SalesforceConfiguration config,
        ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => (int)response.StatusCode >= 500) // 5xx indica servidor caído
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5, // Abrir tras 5 fallos consecutivos
                durationOfBreak: TimeSpan.FromSeconds(config.CircuitBreakerDurationSeconds),
                onBreak: (outcome, duration) =>
                {
                    logger.LogError(
                        "Circuit Breaker ABIERTO por {Duration} segundos tras {Count} fallos consecutivos",
                        duration.TotalSeconds,
                        5);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit Breaker CERRADO. Conexión restaurada");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit Breaker SEMI-ABIERTO. Probando conexión...");
                });
    }

    /// <summary>
    /// Crea una política personalizada para manejo específico de Rate Limit (429).
    /// Lee el header Retry-After y espera exactamente ese tiempo antes de reintentar.
    /// </summary>
    public static AsyncPolicy<HttpResponseMessage> CreateRateLimitPolicy(ILogger logger)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(response => response.StatusCode == (HttpStatusCode)429)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (retryAttempt, result, context) =>
                {
                    // Intentar leer header Retry-After
                    if (result.Result.Headers.TryGetValues("Retry-After", out var values))
                    {
                        var retryAfterValue = values.FirstOrDefault();

                        // Retry-After puede ser segundos o fecha HTTP
                        if (int.TryParse(retryAfterValue, out var seconds))
                        {
                            logger.LogWarning(
                                "Rate limit: esperando {Seconds} segundos según Retry-After header",
                                seconds);
                            return TimeSpan.FromSeconds(seconds);
                        }

                        // Si es fecha, calcular diferencia
                        if (DateTimeOffset.TryParse(retryAfterValue, out var retryAfterDate))
                        {
                            var delay = retryAfterDate - DateTimeOffset.UtcNow;
                            if (delay.TotalSeconds > 0)
                            {
                                logger.LogWarning(
                                    "Rate limit: esperando hasta {RetryAfterDate}",
                                    retryAfterDate);
                                return delay;
                            }
                        }
                    }

                    // Fallback: usar backoff exponencial si no hay header
                    var fallbackDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    logger.LogWarning(
                        "Rate limit sin Retry-After header. Usando fallback: {Delay}s",
                        fallbackDelay.TotalSeconds);
                    return fallbackDelay;
                },
                onRetryAsync: async (outcome, timespan, retryAttempt, context) =>
                {
                    logger.LogInformation(
                        "Esperando {Delay}ms antes del reintento {RetryAttempt} por rate limit",
                        timespan.TotalMilliseconds,
                        retryAttempt);

                    await Task.CompletedTask;
                });
    }

    /// <summary>
    /// Combina todas las políticas en una sola usando PolicyWrap.
    /// Orden de ejecución: RateLimit → Retry → CircuitBreaker.
    /// El orden importa: queremos manejar rate limits ANTES de contar como fallo para el circuit breaker.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy(
        SalesforceConfiguration config,
        ILogger logger)
    {
        var rateLimitPolicy = CreateRateLimitPolicy(logger);
        var retryPolicy = CreateRetryPolicy(config, logger);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy(config, logger);

        // PolicyWrap ejecuta de adentro hacia afuera: CircuitBreaker → Retry → RateLimit
        return Policy.WrapAsync(rateLimitPolicy, retryPolicy, circuitBreakerPolicy);
    }
}