using PlustekBCR.Helpers;
using PlustekBCR.Models;
using Xunit;

namespace PlustekBCR.Tests;

public class ContactFieldRowConverterTests
{
    private readonly ContactFieldRowConverter _converter = new();

    [Theory]
    [InlineData("Telephone", 0)]
    [InlineData("Mobile", 1)]
    [InlineData("Email", 2)]
    [InlineData("Website", 3)]
    [InlineData("Fax", 4)]
    [InlineData("Address", 5)]
    public void UsContactRows_PutAddressLast(string field, int expectedRow)
    {
        Assert.Equal(expectedRow, Convert(MarketCode.US, field));
    }

    [Theory]
    [InlineData("Address", 0)]
    [InlineData("Telephone", 1)]
    [InlineData("Mobile", 2)]
    [InlineData("Email", 3)]
    [InlineData("Website", 4)]
    [InlineData("Fax", 5)]
    public void JpDetailRows_PreserveExistingOrder(string field, int expectedRow)
    {
        Assert.Equal(expectedRow, Convert(MarketCode.JP, field));
    }

    [Theory]
    [InlineData(MarketCode.US, "SidebarWebsite", 3)]
    [InlineData(MarketCode.US, "SidebarAddress", 4)]
    [InlineData(MarketCode.JP, "SidebarAddress", 0)]
    [InlineData(MarketCode.JP, "SidebarWebsite", 4)]
    public void SidebarRows_ExcludeFaxWithoutLeavingAGap(MarketCode market, string field, int expectedRow)
    {
        Assert.Equal(expectedRow, Convert(market, field));
    }

    private int Convert(MarketCode market, string field) =>
        (int)_converter.Convert(market, typeof(int), field, string.Empty);
}
