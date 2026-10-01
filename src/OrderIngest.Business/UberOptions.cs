namespace OrderIngest.Business;

public class UberOptions
{
    public const string SectionName = "Uber";

    /// <summary>HMAC key for X-Uber-Signature verification. Dev-only value lives in appsettings.Development.json.</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>"Fixture" (default, serves fixtures/uber-get-order.json) or "Http" (real GET against resource_href).</summary>
    public string OrderClientMode { get; set; } = "Fixture";

    /// <summary>Path to the fixture served by FixtureUberOrderClient.</summary>
    public string FixturePath { get; set; } = "";

    /// <summary>OAuth bearer token for the real client. Unused in Fixture mode.</summary>
    public string AccessToken { get; set; } = "";
}
