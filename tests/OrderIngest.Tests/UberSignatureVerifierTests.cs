using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OrderIngest.Business;

namespace OrderIngest.Tests;

public class UberSignatureVerifierTests
{
    private const string Secret = "test-secret";

    private static readonly UberSignatureVerifier Verifier =
        new(Options.Create(new UberOptions { ClientSecret = Secret }));

    private static string Sign(string body, string secret = Secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(body)));

    [Fact]
    public void ValidSignature_Passes()
    {
        var body = TestFixtures.Read("uber-notification.json");
        Assert.True(Verifier.IsValid(body, Sign(body)));
    }

    [Fact]
    public void UppercaseHexSignature_Passes()
    {
        var body = TestFixtures.Read("uber-notification.json");
        Assert.True(Verifier.IsValid(body, Sign(body).ToUpperInvariant()));
    }

    [Fact]
    public void TamperedBody_Fails()
    {
        var body = TestFixtures.Read("uber-notification.json");
        Assert.False(Verifier.IsValid(body + " ", Sign(body)));
    }

    [Fact]
    public void SignatureFromWrongSecret_Fails()
    {
        var body = TestFixtures.Read("uber-notification.json");
        Assert.False(Verifier.IsValid(body, Sign(body, "other-secret")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("deadbeef")]
    public void MissingOrBogusSignature_Fails(string? header) =>
        Assert.False(Verifier.IsValid("{}", header));
}
