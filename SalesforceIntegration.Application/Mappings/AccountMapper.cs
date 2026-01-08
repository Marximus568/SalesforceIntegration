using SalesforceIntegration.Application.DTOs;
using SalesforceIntegration.Domain.Entities;
using SalesforceIntegration.Domain.Enums;

namespace SalesforceIntegration.Application.Mappings;

/// <summary>
/// Mapper que transforma DTOs de Salesforce en entidades de dominio.
/// Encapsula toda la lógica de conversión, validación y mapeo de tipos.
/// Si Salesforce cambia su estructura, solo modificamos este archivo.
/// </summary>
public static class AccountMapper
{
    /// <summary>
    /// Convierte un DTO de Salesforce en una entidad de dominio Account.
    /// Aplica validaciones y transformaciones necesarias.
    /// </summary>
    /// <param name="dto">DTO recibido desde Salesforce API.</param>
    /// <returns>Entidad de dominio Account.</returns>
    /// <exception cref="ArgumentNullException">Si el DTO es nulo.</exception>
    /// <exception cref="ArgumentException">Si el DTO contiene datos inválidos.</exception>
    public static Account MapToDomain(SalesforceAccountDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new ArgumentException("Salesforce Account Id no puede estar vacío");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Salesforce Account Name no puede estar vacío");

        var accountType = MapAccountType(dto.Type);

        return Account.CreateFromExternal(
            externalId: dto.Id,
            name: dto.Name,
            type: accountType,
            industry: dto.Industry,
            annualRevenue: dto.AnnualRevenue,
            numberOfEmployees: dto.NumberOfEmployees,
            billingCity: dto.BillingCity,
            billingCountry: dto.BillingCountry,
            lastModifiedDate: dto.LastModifiedDate
        );
    }

    /// <summary>
    /// Convierte una colección de DTOs de Salesforce en entidades de dominio.
    /// Filtra registros eliminados y registros con datos inválidos.
    /// </summary>
    /// <param name="dtos">Colección de DTOs desde Salesforce.</param>
    /// <returns>Colección de entidades de dominio válidas.</returns>
    public static IEnumerable<Account> MapToDomain(IEnumerable<SalesforceAccountDto> dtos)
    {
        if (dtos == null)
            return Enumerable.Empty<Account>();

        var accounts = new List<Account>();

        foreach (var dto in dtos)
        {
            try
            {
                // Filtrar registros eliminados en Salesforce
                if (dto.IsDeleted)
                    continue;

                var account = MapToDomain(dto);
                accounts.Add(account);
            }
            catch (Exception ex)
            {
                // Log del error pero continuar procesando otros registros
                // En producción: usar ILogger y correlacionar con dto.Id
                Console.WriteLine($"Error mapeando cuenta {dto.Id}: {ex.Message}");
            }
        }

        return accounts;
    }

    /// <summary>
    /// Mapea el tipo de cuenta de Salesforce a nuestro enum interno.
    /// Implementa mapeo defensivo: valores desconocidos se mapean a AccountType.Other.
    /// </summary>
    /// <param name="salesforceType">Valor del campo Type de Salesforce.</param>
    /// <returns>Enum AccountType correspondiente.</returns>
    private static AccountType MapAccountType(string? salesforceType)
    {
        if (string.IsNullOrWhiteSpace(salesforceType))
            return AccountType.Other;

        return salesforceType.Trim().ToUpperInvariant() switch
        {
            "PROSPECT" => AccountType.Prospect,
            "CUSTOMER" => AccountType.Customer,
            "PARTNER" => AccountType.Partner,
            "COMPETITOR" => AccountType.Competitor,
            "CHANNEL PARTNER" => AccountType.Partner, // Mapeo de variante
            "RESELLER" => AccountType.Partner,       // Mapeo de variante
            _ => AccountType.Other // Valor desconocido: mapeo defensivo
        };
    }

    /// <summary>
    /// Actualiza una entidad existente con datos desde Salesforce.
    /// Usado cuando el registro ya existe en nuestra BD pero necesita sincronizarse.
    /// </summary>
    /// <param name="existing">Entidad existente en nuestra BD.</param>
    /// <param name="dto">DTO con datos actualizados desde Salesforce.</param>
    public static void UpdateFromDto(Account existing, SalesforceAccountDto dto)
    {
        if (existing == null)
            throw new ArgumentNullException(nameof(existing));

        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        // Verificar que estamos actualizando la cuenta correcta
        if (existing.ExternalId != dto.Id)
            throw new InvalidOperationException(
                $"ExternalId mismatch: {existing.ExternalId} != {dto.Id}");

        var accountType = MapAccountType(dto.Type);

        existing.UpdateFromExternal(
            name: dto.Name,
            type: accountType,
            industry: dto.Industry,
            annualRevenue: dto.AnnualRevenue,
            numberOfEmployees: dto.NumberOfEmployees,
            billingCity: dto.BillingCity,
            billingCountry: dto.BillingCountry,
            lastModifiedDate: dto.LastModifiedDate
        );

        // Si Salesforce marcó el registro como eliminado, desactivar nuestra entidad
        if (dto.IsDeleted)
        {
            existing.Deactivate();
        }
    }
}