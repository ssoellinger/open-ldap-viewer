namespace LdapViewer.Models;

public class LdapEntry
{
    public string Dn { get; set; } = string.Empty;

    public Dictionary<string, List<string>> Attributes { get; set; } = new();

    /// <summary>
    /// Returns the RDN (first component of the DN) for display in the tree.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var firstPart = Dn.Split(',')[0];
            var eqIndex = firstPart.IndexOf('=');
            return eqIndex >= 0 ? firstPart[(eqIndex + 1)..] : firstPart;
        }
    }

    public bool HasChildren { get; set; }
}
