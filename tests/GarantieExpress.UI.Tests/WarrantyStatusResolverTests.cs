using GarantieExpress.Domain;
using Xunit;

namespace GarantieExpress.UI.Tests;

public sealed class WarrantyStatusResolverTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    [Fact]
    public void Active_warranty_expires_only_after_its_valid_until_date()
    {
        Assert.Equal(
            WarrantyEffectiveStatus.Active,
            WarrantyStatusResolver.Resolve(WarrantyStatus.Active, Today.AddDays(1), Today));
        Assert.Equal(
            WarrantyEffectiveStatus.Active,
            WarrantyStatusResolver.Resolve(WarrantyStatus.Active, Today, Today));
        Assert.Equal(
            WarrantyEffectiveStatus.Expired,
            WarrantyStatusResolver.Resolve(WarrantyStatus.Active, Today.AddDays(-1), Today));
    }

    [Fact]
    public void Voided_state_takes_precedence_over_expiration()
    {
        Assert.Equal(
            WarrantyEffectiveStatus.Voided,
            WarrantyStatusResolver.Resolve(WarrantyStatus.Voided, Today.AddDays(-1), Today));
    }

    [Fact]
    public void Warranty_without_a_valid_until_date_remains_active()
    {
        Assert.Equal(
            WarrantyEffectiveStatus.Active,
            WarrantyStatusResolver.Resolve(WarrantyStatus.Active, validUntil: null, Today));
    }
}
