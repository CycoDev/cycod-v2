using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CycoTui.Sample.Auth;

internal class AzureAuthRecord
{
    public string Provider { get; set; } = "azure";
    public string Tenant { get; set; } = string.Empty;
    public string? Policy { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public bool UseLegacyB2c { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? AccessToken { get; set; }
    public string? IdToken { get; set; }
    public string? UserName { get; set; }
    public string? ObjectId { get; set; }
    public string? Email { get; set; }

    public static AzureAuthRecord CreateFrom(Microsoft.Identity.Client.AuthenticationResult r, string tenant, string? policy, string clientId, IEnumerable<string> scopes, bool useLegacyB2c)
    {
        var record = new AzureAuthRecord
        {
            Tenant = tenant,
            Policy = policy,
            ClientId = clientId,
            Scopes = scopes.ToList(),
            UseLegacyB2c = useLegacyB2c,
            ExpiresAt = r.ExpiresOn,
            AccessToken = r.AccessToken,
            IdToken = r.IdToken,
            UserName = r.Account?.Username,
            Provider = useLegacyB2c ? "azure-b2c" : "azure-external-id"
        };

        try
        {
            var parts = r.IdToken?.Split('.');
            if (parts?.Length == 3)
            {
                var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1])));
                record.ObjectId = JsonHelpers.GetJsonPropertyValue(payloadJson, "oid");
                var emails = JsonHelpers.GetJsonPropertyValue(payloadJson, "emails");
                record.Email = emails?.Split(',').FirstOrDefault() ?? JsonHelpers.GetJsonPropertyValue(payloadJson, "email");
            }
        }
        catch { }

        return record;
    }

    private static string PadBase64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return s + new string('=', (4 - s.Length % 4) % 4);
    }
}
