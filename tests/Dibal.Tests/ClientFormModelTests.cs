using System.ComponentModel.DataAnnotations;
using Dibal.Domain.Enums;
using Dibal.Web.Components.Pages.Clients;
using Xunit;

namespace Dibal.Tests;

public class ClientFormModelTests
{
    // Regression test for a real bug: HTML forms post "" for a blank optional
    // field, never a true null, and EmailAddressAttribute only special-cases
    // null — it fails on "". Without normalisation this blocked every
    // create/edit where the (optional) email field was left blank.
    [Fact]
    public void Blank_email_passes_validation()
    {
        var model = new ClientFormModel
        {
            BusinessName = "Acme Ltd",
            CustomerType = CustomerType.Reseller,
            Email = "",
        };

        var results = Validate(model);

        Assert.Empty(results);
        Assert.Null(model.Email);
    }

    [Fact]
    public void Invalid_email_still_fails_validation()
    {
        var model = new ClientFormModel
        {
            BusinessName = "Acme Ltd",
            CustomerType = CustomerType.Reseller,
            Email = "not-an-email",
        };

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ClientFormModel.Email)));
    }

    private static List<ValidationResult> Validate(ClientFormModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
