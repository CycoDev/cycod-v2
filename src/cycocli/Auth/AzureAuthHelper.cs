using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace CycoTui.Sample.Auth;

internal class AzureAuthHelper
{
    private readonly string _tenant;
    private readonly string? _policy;
    private readonly string _clientId;
    private readonly string[] _scopes;
    private readonly bool _useLegacyB2c;
    private readonly IPublicClientApplication _app;
    private readonly string _authority;

    /// <summary>
    /// Validates Azure authentication configuration parameters
    /// </summary>
    public static (bool IsValid, string ErrorMessage) ValidateConfiguration(string tenant, string? policy, string clientId, IEnumerable<string> scopes, bool useLegacyB2c)
    {
        // Validate tenant
        if (string.IsNullOrWhiteSpace(tenant))
            return (false, "Tenant cannot be empty");

        // For tenant ID (GUID), validation is different
        if (Guid.TryParse(tenant, out _))
        {
            // Valid GUID tenant ID
        }
        else if (!tenant.EndsWith(".onmicrosoft.com") && !tenant.Contains("."))
        {
            return (false, "Tenant must be a GUID, 'name.onmicrosoft.com', or a custom domain");
        }

        // Validate policy (only for legacy B2C)
        if (useLegacyB2c)
        {
            if (string.IsNullOrWhiteSpace(policy))
                return (false, "Policy is required for legacy B2C mode");

            if (!policy.StartsWith("B2C_1_") && !policy.StartsWith("b2c_1_"))
                ConsoleHelpers.WriteWarning("Policy name should typically start with 'B2C_1_' (e.g., B2C_1_signin)");
        }

        // Validate client ID (should be a GUID)
        if (string.IsNullOrWhiteSpace(clientId))
            return (false, "Client ID cannot be empty");

        if (!Guid.TryParse(clientId, out _))
            return (false, "Client ID must be a valid GUID");

        // Validate scopes
        var scopeArray = scopes?.ToArray() ?? Array.Empty<string>();
        if (scopeArray.Length == 0)
            return (false, "At least one scope must be specified");

        return (true, string.Empty);
    }

    public AzureAuthHelper(string tenant, string? policy, string clientId, IEnumerable<string> scopes, bool useLegacyB2c)
    {
        _tenant = tenant;
        _policy = policy;
        _clientId = clientId;
        _scopes = scopes.ToArray();
        _useLegacyB2c = useLegacyB2c;

        // Build authority based on mode
        if (useLegacyB2c && !string.IsNullOrEmpty(policy))
        {
            // Legacy B2C: https://{tenant-name}.b2clogin.com/{tenant}/{policy}/v2.0
            var baseName = tenant.Replace(".onmicrosoft.com", "");
            _authority = $"https://{baseName}.b2clogin.com/{tenant}/{policy}/v2.0";
        }
        else
        {
            // Modern External ID / AAD: https://login.microsoftonline.com/{tenant}/v2.0
            _authority = $"https://login.microsoftonline.com/{tenant}/v2.0";
        }

        ConsoleHelpers.WriteLine($"Building MSAL PublicClientApplication...", ConsoleColor.DarkGray, overrideQuiet: true);
        ConsoleHelpers.WriteLine($"  Authority: {_authority}", ConsoleColor.DarkGray, overrideQuiet: true);
        ConsoleHelpers.WriteLine($"  Redirect URI: http://localhost", ConsoleColor.DarkGray, overrideQuiet: true);

        var builder = PublicClientApplicationBuilder
            .Create(clientId)
            .WithRedirectUri("http://localhost");

        // Use different authority builders based on mode
        if (useLegacyB2c)
        {
            builder = builder.WithB2CAuthority(_authority);
        }
        else
        {
            builder = builder.WithAuthority(_authority);
        }

        _app = builder.Build();
    }

    public string GetAuthority() => _authority;

    public async Task<AuthenticationResult> AcquireAsync()
    {
        ConsoleHelpers.WriteLine($"Checking for existing accounts...", ConsoleColor.DarkGray, overrideQuiet: true);
        var accounts = await _app.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account != null)
        {
            ConsoleHelpers.WriteLine($"Found existing account: {account.Username}", ConsoleColor.DarkGray, overrideQuiet: true);
        }
        else
        {
            ConsoleHelpers.WriteLine($"No existing accounts found.", ConsoleColor.DarkGray, overrideQuiet: true);
        }

        try
        {
            ConsoleHelpers.WriteLine($"Attempting silent token acquisition...", ConsoleColor.DarkGray, overrideQuiet: true);
            return await _app.AcquireTokenSilent(_scopes, account).ExecuteAsync();
        }
        catch (MsalUiRequiredException uiEx)
        {
            ConsoleHelpers.WriteLine($"UI required: {uiEx.ErrorCode} - {uiEx.Message}", ConsoleColor.DarkGray, overrideQuiet: true);
            ConsoleHelpers.WriteLine($"Starting device code flow...", ConsoleColor.DarkGray, overrideQuiet: true);

            try
            {
                return await _app.AcquireTokenWithDeviceCode(_scopes, dc =>
                {
                    ConsoleHelpers.WriteLine(dc.Message, ConsoleColor.Yellow, overrideQuiet: true);
                    return Task.CompletedTask;
                }).ExecuteAsync();
            }
            catch (MsalServiceException msalEx)
            {
                ConsoleHelpers.WriteErrorLine($"Device code flow failed:");
                ConsoleHelpers.WriteErrorLine($"  Error Code: {msalEx.ErrorCode}");
                ConsoleHelpers.WriteErrorLine($"  Message: {msalEx.Message}");
                ConsoleHelpers.WriteErrorLine($"  Status Code: {msalEx.StatusCode}");
                if (!string.IsNullOrEmpty(msalEx.ResponseBody))
                {
                    ConsoleHelpers.WriteErrorLine($"  Response Body: {msalEx.ResponseBody}");
                }
                throw;
            }
        }
    }
}
