namespace LdapViewer.Models;

public class LdapSchema
{
    public List<SchemaItem> ObjectClasses { get; set; } = [];
    public List<SchemaItem> AttributeTypes { get; set; } = [];
}

public class SchemaItem
{
    public string Name { get; set; } = string.Empty;
    public string? Oid { get; set; }
    public string? Description { get; set; }
    public string Definition { get; set; } = string.Empty;
}
