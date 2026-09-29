using PlustekBCR.Services;
using Xunit;

namespace PlustekBCR.Tests;

public class ContactActionServiceTests
{
    [Fact]
    public void EmailUri_UsesMailToAndEscapesAddress()
    {
        var success = ContactActionUriBuilder.TryCreateEmailUri("person@example.com", out var uri);

        Assert.True(success);
        Assert.Equal("mailto:person@example.com", uri!.OriginalString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("person@example.com\r\nsubject=unsafe")]
    public void EmailUri_RejectsInvalidValues(string value)
    {
        Assert.False(ContactActionUriBuilder.TryCreateEmailUri(value, out _));
    }

    [Fact]
    public void PhoneUri_UsesTelScheme()
    {
        var success = ContactActionUriBuilder.TryCreatePhoneUri("+1 (425) 555-0100", out var uri);

        Assert.True(success);
        Assert.StartsWith("tel:", uri!.OriginalString);
    }

    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("https://example.com/path", "https://example.com/path")]
    public void WebsiteUri_NormalizesHttpAndHttps(string value, string expected)
    {
        var success = ContactActionUriBuilder.TryCreateWebsiteUri(value, out var uri);

        Assert.True(success);
        Assert.Equal(expected, uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///c:/windows/system.ini")]
    [InlineData("")]
    public void WebsiteUri_RejectsUnsafeOrEmptySchemes(string value)
    {
        Assert.False(ContactActionUriBuilder.TryCreateWebsiteUri(value, out _));
    }
}
