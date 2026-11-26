using System.Security.Cryptography;
using System.Text;
using HMIImageExport.Core;

namespace HMIImageExport.App.Infrastructure;

/// <summary>Windows-DPAPI, gebunden an das Windows-Benutzerkonto. Format: "dpapi:" + Base64.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HMIImageExport.v1");

    public string Protect(string plain) =>
        Prefix + Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));

    public string Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal)) throw new FormatException("Kein geschützter Wert.");
        var data = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
    }
}
