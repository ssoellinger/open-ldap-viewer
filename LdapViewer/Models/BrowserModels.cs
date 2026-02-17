namespace LdapViewer.Models;

public class TreeContextMenuArgs
{
    public string Dn { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double ClientX { get; set; }
    public double ClientY { get; set; }
    public bool IsLeaf { get; set; }
}

public class SearchHistoryItem
{
    public string Attribute { get; set; } = "";
    public string Value { get; set; } = "";
}

public class SavedSearch
{
    public string Name { get; set; } = "";
    public string Attribute { get; set; } = "";
    public string Value { get; set; } = "";
    public string Filter { get; set; } = "";
}

public class CertInfo
{
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string NotBefore { get; set; } = "";
    public string NotAfter { get; set; } = "";
    public string SerialNumber { get; set; } = "";
}
