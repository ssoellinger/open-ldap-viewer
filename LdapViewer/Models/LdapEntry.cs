namespace LdapViewer.Models;

public class LdapEntry
{
    public string Dn { get; set; } = string.Empty;

    public Dictionary<string, List<string>> Attributes { get; set; } = new();

    /// <summary>
    /// Returns the RDN (first component of the DN) for display in the tree.
    /// </summary>
    public string DisplayName => GetRdn(Dn);

    /// <summary>
    /// Extracts the RDN display value from any DN string (e.g. "cn=Max,ou=People,dc=test" -> "Max").
    /// </summary>
    public static string GetRdn(string dn)
    {
        var idx = dn.IndexOf(',');
        var rdn = idx > 0 ? dn[..idx] : dn;
        var eqIdx = rdn.IndexOf('=');
        return eqIdx > 0 ? rdn[(eqIdx + 1)..] : rdn;
    }
}
