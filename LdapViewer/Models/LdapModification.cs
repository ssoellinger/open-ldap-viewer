namespace LdapViewer.Models;

public enum LdapModificationType
{
    Add,
    Replace,
    Delete
}

public class LdapModification
{
    public string AttributeName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public LdapModificationType Type { get; set; }
}

public enum LdifChangeType
{
    Add,
    Modify,
    Delete
}

public class LdifOperation
{
    public string Dn { get; set; } = string.Empty;
    public LdifChangeType ChangeType { get; set; }
    public Dictionary<string, List<string>> Attributes { get; set; } = new();
    public List<LdapModification> Modifications { get; set; } = new();
}

public class LdifResult
{
    public string Dn { get; set; } = string.Empty;
    public LdifChangeType ChangeType { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
