using System.Text.Json.Serialization;

namespace SalesforceIntegration.Application.DTOs;

/// <summary>
/// DTO que representa la estructura exacta del objeto Account retornado por Salesforce REST API.
/// Los nombres de propiedades coinciden EXACTAMENTE con los campos de Salesforce.
/// Este DTO nunca debe usarse fuera de la capa de infraestructura.
/// </summary>
public class SalesforceAccountDto
{
    /// <summary>
    /// ID único de la cuenta en Salesforce (18 caracteres).
    /// Formato: 001xx000003DGb2AAG
    /// </summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de la cuenta.
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de cuenta según clasificación de Salesforce.
    /// Valores posibles: "Prospect", "Customer", "Partner", "Competitor", etc.
    /// </summary>
    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    /// <summary>
    /// Industria o sector.
    /// </summary>
    [JsonPropertyName("Industry")]
    public string? Industry { get; set; }

    /// <summary>
    /// Ingresos anuales.
    /// </summary>
    [JsonPropertyName("AnnualRevenue")]
    public decimal? AnnualRevenue { get; set; }

    /// <summary>
    /// Número de empleados.
    /// </summary>
    [JsonPropertyName("NumberOfEmployees")]
    public int? NumberOfEmployees { get; set; }

    /// <summary>
    /// Ciudad de facturación.
    /// </summary>
    [JsonPropertyName("BillingCity")]
    public string? BillingCity { get; set; }

    /// <summary>
    /// País de facturación.
    /// </summary>
    [JsonPropertyName("BillingCountry")]
    public string? BillingCountry { get; set; }

    /// <summary>
    /// Fecha de última modificación en Salesforce (formato ISO 8601).
    /// Ejemplo: "2024-01-15T10:30:45.000+0000"
    /// Campo crítico para sincronización incremental.
    /// </summary>
    [JsonPropertyName("LastModifiedDate")]
    public DateTime LastModifiedDate { get; set; }

    /// <summary>
    /// Fecha de creación en Salesforce.
    /// </summary>
    [JsonPropertyName("CreatedDate")]
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Indica si el registro está activo en Salesforce.
    /// Salesforce usa soft delete, este campo indica registros eliminados.
    /// </summary>
    [JsonPropertyName("IsDeleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>
/// DTO que envuelve la respuesta de query SOQL de Salesforce.
/// Salesforce retorna los registros dentro de un objeto con metadata.
/// </summary>
public class SalesforceQueryResponse<T>
{
    /// <summary>
    /// Total de registros que coinciden con el query (puede ser mayor que los retornados).
    /// </summary>
    [JsonPropertyName("totalSize")]
    public int TotalSize { get; set; }

    /// <summary>
    /// Indica si hay más registros disponibles (paginación).
    /// </summary>
    [JsonPropertyName("done")]
    public bool Done { get; set; }

    /// <summary>
    /// URL del siguiente lote de registros si Done = false.
    /// </summary>
    [JsonPropertyName("nextRecordsUrl")]
    public string? NextRecordsUrl { get; set; }

    /// <summary>
    /// Lista de registros retornados en este lote.
    /// </summary>
    [JsonPropertyName("records")]
    public List<T> Records { get; set; } = new();
}