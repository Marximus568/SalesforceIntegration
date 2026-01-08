using SalesforceIntegration.Domain.Enums;

namespace SalesforceIntegration.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa una cuenta comercial.
/// Esta entidad NO conoce de Salesforce, CRM ni ningún sistema externo.
/// Representa el concepto puro de negocio.
/// </summary>
public class Account
{
    /// <summary>
    /// Identificador único interno de la cuenta.
    /// Este ID es generado por nuestro sistema, NO es el ID de Salesforce.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identificador externo (de Salesforce u otro CRM).
    /// Permite correlacionar nuestra entidad con el registro en el sistema origen.
    /// </summary>
    public string ExternalId { get; private set; }

    /// <summary>
    /// Nombre comercial de la cuenta.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Tipo de cuenta según clasificación de negocio.
    /// </summary>
    public AccountType Type { get; private set; }

    /// <summary>
    /// Industria o sector al que pertenece la cuenta.
    /// </summary>
    public string Industry { get; private set; }

    /// <summary>
    /// Ingresos anuales reportados.
    /// </summary>
    public decimal? AnnualRevenue { get; private set; }

    /// <summary>
    /// Número de empleados.
    /// </summary>
    public int? NumberOfEmployees { get; private set; }

    /// <summary>
    /// Ciudad de facturación.
    /// </summary>
    public string BillingCity { get; private set; }

    /// <summary>
    /// País de facturación.
    /// </summary>
    public string BillingCountry { get; private set; }

    /// <summary>
    /// Indica si la cuenta está activa en nuestro sistema.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Fecha de creación en nuestro sistema.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Fecha de última modificación en el sistema origen (Salesforce).
    /// Usado para sincronización incremental.
    /// </summary>
    public DateTime LastModifiedDate { get; private set; }

    /// <summary>
    /// Constructor privado para EF Core y deserialización.
    /// </summary>
    private Account() { }

    /// <summary>
    /// Factory method para crear una nueva cuenta desde datos externos.
    /// Encapsula la lógica de creación y validación.
    /// </summary>
    public static Account CreateFromExternal(
        string externalId,
        string name,
        AccountType type,
        string industry,
        decimal? annualRevenue,
        int? numberOfEmployees,
        string billingCity,
        string billingCountry,
        DateTime lastModifiedDate)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("ExternalId no puede estar vacío", nameof(externalId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name no puede estar vacío", nameof(name));

        return new Account
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            Name = name,
            Type = type,
            Industry = industry ?? "Unknown",
            AnnualRevenue = annualRevenue,
            NumberOfEmployees = numberOfEmployees,
            BillingCity = billingCity,
            BillingCountry = billingCountry,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastModifiedDate = lastModifiedDate
        };
    }

    /// <summary>
    /// Actualiza los datos de la cuenta con información sincronizada.
    /// Mantiene el Id interno pero actualiza campos desde el sistema externo.
    /// </summary>
    public void UpdateFromExternal(
        string name,
        AccountType type,
        string industry,
        decimal? annualRevenue,
        int? numberOfEmployees,
        string billingCity,
        string billingCountry,
        DateTime lastModifiedDate)
    {
        Name = name;
        Type = type;
        Industry = industry ?? "Unknown";
        AnnualRevenue = annualRevenue;
        NumberOfEmployees = numberOfEmployees;
        BillingCity = billingCity;
        BillingCountry = billingCountry;
        LastModifiedDate = lastModifiedDate;
    }

    /// <summary>
    /// Marca la cuenta como inactiva.
    /// No elimina físicamente, permite auditoría histórica.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}