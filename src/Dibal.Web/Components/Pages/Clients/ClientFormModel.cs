using System.ComponentModel.DataAnnotations;
using Dibal.Domain.Entities;
using Dibal.Domain.Enums;

namespace Dibal.Web.Components.Pages.Clients;

public class ClientFormModel
{
    [Required, StringLength(200)]
    public string BusinessName { get; set; } = "";

    [StringLength(200)]
    public string? ClientName { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? County { get; set; }

    [StringLength(20)]
    public string? Postcode { get; set; }

    // A blank optional field posts as "" (HTML forms never send a true null),
    // and EmailAddressAttribute fails "" — it only special-cases null. Without
    // this normalisation, leaving Email blank would wrongly block every
    // create/edit, since email is optional per docs/02-schema.sql.
    private string? _email;

    [EmailAddress, StringLength(200)]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [StringLength(50)]
    public string? Phone { get; set; }

    [Required]
    public CustomerType CustomerType { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public static ClientFormModel FromEntity(Client client) => new()
    {
        BusinessName = client.BusinessName,
        ClientName = client.ClientName,
        AddressLine1 = client.AddressLine1,
        AddressLine2 = client.AddressLine2,
        City = client.City,
        County = client.County,
        Postcode = client.Postcode,
        Email = client.Email,
        Phone = client.Phone,
        CustomerType = client.CustomerType,
        Notes = client.Notes,
    };

    public void ApplyTo(Client client)
    {
        client.BusinessName = BusinessName;
        client.ClientName = ClientName;
        client.AddressLine1 = AddressLine1;
        client.AddressLine2 = AddressLine2;
        client.City = City;
        client.County = County;
        client.Postcode = Postcode;
        client.Email = Email;
        client.Phone = Phone;
        client.CustomerType = CustomerType;
        client.Notes = Notes;
    }
}
