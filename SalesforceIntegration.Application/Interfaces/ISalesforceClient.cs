using SalesforceIntegration.Application.DTOs;

namespace SalesforceIntegration.Application.Interfaces;

/// <summary>
/// Contrato de comunicación con Salesforce API.
/// Abstrae los detalles de HTTP, autenticación y resiliencia.
/// Los consumidores de esta interfaz no conocen que detrás hay un HttpClient.
/// </summary>
public interface ISalesforceClient
{
    /// <summary>
    /// Obtiene cuentas modificadas después de una fecha específica.
    /// Implementa paginación automática si Salesforce retorna más de 2000 registros.
    /// </summary>
    /// <param name="modifiedSince">Fecha desde la cual buscar modificaciones.</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    /// <returns>Lista de DTOs de cuentas desde Salesforce.</returns>
    /// <exception cref="SalesforceApiException">Cuando la API retorna error no recuperable.</exception>
    /// <exception cref="SalesforceAuthenticationException">Cuando falla la autenticación OAuth2.</exception>
    Task<IEnumerable<SalesforceAccountDto>> GetAccountsModifiedSinceAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una cuenta específica por su ID de Salesforce.
    /// </summary>
    /// <param name="salesforceId">ID de la cuenta en Salesforce (formato: 001xxxxxxxxxxxxxxx).</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    /// <returns>DTO de la cuenta o null si no existe.</returns>
    Task<SalesforceAccountDto?> GetAccountByIdAsync(
        string salesforceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica el estado de conexión con Salesforce.
    /// Útil para health checks y monitoreo.
    /// </summary>
    /// <returns>True si la conexión es exitosa.</returns>
    Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default);
}