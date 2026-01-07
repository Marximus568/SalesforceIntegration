namespace SalesforceIntegration.Domain.Enums;

/// <summary>
/// Tipo de cuenta según clasificación de negocio.
/// Estos valores representan el concepto interno de tipo de cuenta,
/// independiente de cómo Salesforce los nombre.
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Cliente potencial que aún no ha realizado compras.
    /// </summary>
    Prospect = 0,

    /// <summary>
    /// Cliente activo con compras realizadas.
    /// </summary>
    Customer = 1,

    /// <summary>
    /// Socio comercial o integrador.
    /// </summary>
    Partner = 2,

    /// <summary>
    /// Competidor identificado en el mercado.
    /// </summary>
    Competitor = 3,

    /// <summary>
    /// Tipo no clasificado o desconocido.
    /// </summary>
    Other = 99
}