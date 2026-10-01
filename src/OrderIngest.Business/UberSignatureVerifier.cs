using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OrderIngest.Business;

public class UberSignatureVerifier(IOptions<UberOptions> options)
{
    /// <summary>
    /// Per Uber's docs, X-Uber-Signature is "a lowercased hexadecimal HMAC
    /// signature of the webhook HTTP request body, using the client secret
    /// as a key and SHA256 as the hash function".
    /// </summary>
    public bool IsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        var computed = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(options.Value.ClientSecret),
            Encoding.UTF8.GetBytes(rawBody));
        var computedHex = Convert.ToHexStringLower(computed);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant()));
    }
}
