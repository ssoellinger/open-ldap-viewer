namespace LdapViewer.Models;

public class LdapSchema
{
    public List<SchemaItem> ObjectClasses { get; set; } = [];
    public List<SchemaItem> AttributeTypes { get; set; } = [];

    /// <summary>
    /// Returns the attribute names allowed (MUST + MAY) for the given objectClass names,
    /// including inherited attributes from SUP chain.
    /// </summary>
    public HashSet<string> GetAllowedAttributes(IEnumerable<string> objectClassNames)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ocName in objectClassNames)
        {
            CollectAttributes(ocName, allowed, visited);
        }

        return allowed;
    }

    /// <summary>
    /// Returns only the MUST (required) attributes for the given objectClass names,
    /// including inherited from SUP chain.
    /// </summary>
    public HashSet<string> GetRequiredAttributes(IEnumerable<string> objectClassNames)
    {
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ocName in objectClassNames)
        {
            CollectRequiredAttributes(ocName, required, visited);
        }

        return required;
    }

    /// <summary>
    /// Returns the typical RDN attribute for a given structural objectClass.
    /// </summary>
    public static string? GetTypicalRdnAttribute(IEnumerable<string> objectClassNames)
    {
        var names = new HashSet<string>(objectClassNames, StringComparer.OrdinalIgnoreCase);

        if (names.Contains("inetOrgPerson") || names.Contains("person") ||
            names.Contains("organizationalPerson") || names.Contains("account"))
            return "cn";
        if (names.Contains("organizationalUnit"))
            return "ou";
        if (names.Contains("organization"))
            return "o";
        if (names.Contains("groupOfNames") || names.Contains("groupOfUniqueNames") ||
            names.Contains("posixGroup"))
            return "cn";
        if (names.Contains("dcObject") || names.Contains("domain"))
            return "dc";
        if (names.Contains("device"))
            return "cn";
        if (names.Contains("locality"))
            return "l";
        if (names.Contains("country"))
            return "c";

        return null;
    }

    private void CollectRequiredAttributes(string ocName, HashSet<string> required, HashSet<string> visited)
    {
        if (!visited.Add(ocName)) return;

        var oc = ObjectClasses.FirstOrDefault(o =>
            o.Name.Equals(ocName, StringComparison.OrdinalIgnoreCase));

        if (oc == null) return;

        foreach (var attr in ParseAttributeList(oc.Definition, "MUST"))
            required.Add(attr);

        foreach (var sup in ParseSupList(oc.Definition))
            CollectRequiredAttributes(sup, required, visited);
    }

    private void CollectAttributes(string ocName, HashSet<string> allowed, HashSet<string> visited)
    {
        if (!visited.Add(ocName)) return;

        var oc = ObjectClasses.FirstOrDefault(o =>
            o.Name.Equals(ocName, StringComparison.OrdinalIgnoreCase));

        if (oc == null) return;

        var def = oc.Definition;

        // Extract MUST attributes
        foreach (var attr in ParseAttributeList(def, "MUST"))
            allowed.Add(attr);

        // Extract MAY attributes
        foreach (var attr in ParseAttributeList(def, "MAY"))
            allowed.Add(attr);

        // Follow SUP chain
        foreach (var sup in ParseSupList(def))
            CollectAttributes(sup, allowed, visited);
    }

    private static List<string> ParseAttributeList(string definition, string keyword)
    {
        var result = new List<string>();

        // Match "KEYWORD ( attr1 $ attr2 )" or "KEYWORD attrName"
        var idx = definition.IndexOf($" {keyword} ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return result;

        var after = definition[(idx + keyword.Length + 2)..].TrimStart();

        if (after.StartsWith("("))
        {
            var closeIdx = after.IndexOf(')');
            if (closeIdx > 0)
            {
                var inner = after[1..closeIdx];
                foreach (var part in inner.Split('$'))
                {
                    var attr = part.Trim();
                    if (!string.IsNullOrEmpty(attr))
                        result.Add(attr);
                }
            }
        }
        else
        {
            // Single attribute: take until next space or keyword
            var spaceIdx = after.IndexOf(' ');
            var attr = spaceIdx > 0 ? after[..spaceIdx].Trim() : after.Trim().TrimEnd(')');
            if (!string.IsNullOrEmpty(attr))
                result.Add(attr);
        }

        return result;
    }

    private static List<string> ParseSupList(string definition)
    {
        var result = new List<string>();

        var idx = definition.IndexOf(" SUP ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return result;

        var after = definition[(idx + 5)..].TrimStart();

        if (after.StartsWith("("))
        {
            var closeIdx = after.IndexOf(')');
            if (closeIdx > 0)
            {
                var inner = after[1..closeIdx];
                foreach (var part in inner.Split('$'))
                {
                    var sup = part.Trim();
                    if (!string.IsNullOrEmpty(sup))
                        result.Add(sup);
                }
            }
        }
        else
        {
            var spaceIdx = after.IndexOf(' ');
            var sup = spaceIdx > 0 ? after[..spaceIdx].Trim() : after.Trim().TrimEnd(')');
            if (!string.IsNullOrEmpty(sup))
                result.Add(sup);
        }

        return result;
    }
}

public class SchemaItem
{
    public string Name { get; set; } = string.Empty;
    public string? Oid { get; set; }
    public string? Description { get; set; }
    public string Definition { get; set; } = string.Empty;
}
