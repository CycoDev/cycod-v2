using System;
using System.Linq;
using CycoTui.Sample.Auth;

namespace CycoTui.Sample.Auth;

internal static class AzureLoginHandler
{
    // Hard-coded defaults for Microsoft Entra External ID (CIAM)
    // Users don't need to know these values - they're embedded in the app
    private const string DefaultTenant = "ee2e495d-478c-46ef-a79e-08218a6b54b9";  // External ID tenant ID
    private const string DefaultPolicy = "";  // Not used for External ID
    private const string DefaultClientId = "a1100676-5e5a-4bc5-b746-54342dc09d8d";  // cycocli app (multi-tenant)

    public static void Handle()
    {
        try
        {
            var tenant = Environment.GetEnvironmentVariable("AZURE_TENANT") ?? DefaultTenant;
            var policy = Environment.GetEnvironmentVariable("AZURE_B2C_POLICY") ?? DefaultPolicy;
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? DefaultClientId;
            var scopesEnv = Environment.GetEnvironmentVariable("AZURE_SCOPES");
            var scopes = (scopesEnv?.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries) ?? new[] { "openid", "offline_access" });
            var useLegacyB2c = false; // future constant/flag

            if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(clientId))
            {
                ConsoleHelpers.WriteLine("Missing required configuration for login.", ConsoleColor.Yellow, overrideQuiet: true);
                return;
            }

            if (useLegacyB2c && string.IsNullOrEmpty(policy))
            {
                ConsoleHelpers.WriteLine("Legacy B2C mode requires policy configuration.", ConsoleColor.Yellow, overrideQuiet: true);
                return;
            }

            var (isValid, errorMessage) = AzureAuthHelper.ValidateConfiguration(tenant!, policy, clientId!, scopes, useLegacyB2c);
            if (!isValid)
            {
                ConsoleHelpers.WriteErrorLine($"Configuration validation failed: {errorMessage}");
                return;
            }

            var authMode = useLegacyB2c ? "Azure AD B2C (Legacy)" : "Microsoft Entra External ID";
            ConsoleHelpers.WriteLine($"Authenticating with {authMode}...", ConsoleColor.DarkGray, overrideQuiet: true);
            ConsoleHelpers.WriteLine("Configuration:", ConsoleColor.DarkGray, overrideQuiet: true);
            ConsoleHelpers.WriteLine($"  Tenant: {tenant}", ConsoleColor.DarkGray, overrideQuiet: true);
            if (useLegacyB2c)
                ConsoleHelpers.WriteLine($"  Policy: {policy}", ConsoleColor.DarkGray, overrideQuiet: true);
            ConsoleHelpers.WriteLine($"  ClientId: {clientId}", ConsoleColor.DarkGray, overrideQuiet: true);
            ConsoleHelpers.WriteLine($"  Scopes: {string.Join(", ", scopes)}", ConsoleColor.DarkGray, overrideQuiet: true);

            var helper = new AzureAuthHelper(tenant!, policy, clientId!, scopes, useLegacyB2c);
            var result = helper.AcquireAsync().GetAwaiter().GetResult();
            var record = AzureAuthRecord.CreateFrom(result, tenant!, policy, clientId!, scopes, useLegacyB2c);
            AuthFileStore.Save(record);
            ConsoleHelpers.WriteLine($"{authMode} login successful.", ConsoleColor.Green, overrideQuiet: true);
        }
        catch (Microsoft.Identity.Client.MsalServiceException msalEx)
        {
            ConsoleHelpers.WriteErrorLine("Azure login failed (MSAL Service Exception):");
            ConsoleHelpers.WriteErrorLine($"  Error Code: {msalEx.ErrorCode}");
            ConsoleHelpers.WriteErrorLine($"  Message: {msalEx.Message}");
            ConsoleHelpers.WriteErrorLine($"  Status Code: {msalEx.StatusCode}");
            ConsoleHelpers.WriteErrorLine($"  Correlation ID: {msalEx.CorrelationId}");
            if (!string.IsNullOrEmpty(msalEx.ResponseBody))
            {
                ConsoleHelpers.WriteErrorLine($"  Response Body: {msalEx.ResponseBody}");
            }
            if (msalEx.InnerException != null)
            {
                ConsoleHelpers.WriteErrorLine($"  Inner Exception: {msalEx.InnerException.Message}");
            }
        }
        catch (Microsoft.Identity.Client.MsalClientException msalClientEx)
        {
            ConsoleHelpers.WriteErrorLine("Azure login failed (MSAL Client Exception):");
            ConsoleHelpers.WriteErrorLine($"  Error Code: {msalClientEx.ErrorCode}");
            ConsoleHelpers.WriteErrorLine($"  Message: {msalClientEx.Message}");
            if (msalClientEx.InnerException != null)
            {
                ConsoleHelpers.WriteErrorLine($"  Inner Exception: {msalClientEx.InnerException.Message}");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelpers.WriteErrorLine($"Azure login failed: {ex.Message}");
            ConsoleHelpers.WriteErrorLine($"  Exception Type: {ex.GetType().FullName}");
            if (ex.InnerException != null)
            {
                ConsoleHelpers.WriteErrorLine($"  Inner Exception: {ex.InnerException.Message}");
                ConsoleHelpers.WriteErrorLine($"  Inner Exception Type: {ex.InnerException.GetType().FullName}");
            }
            ConsoleHelpers.WriteErrorLine($"  Stack Trace: {ex.StackTrace}");
        }
    }
}
