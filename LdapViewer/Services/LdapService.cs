using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using LdapViewer.Models;

namespace LdapViewer.Services;

public class LdapService : IDisposable
{
    private LdapConnection? _connection;
    private LdapConnectionSettings? _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected => _connection != null;

    public LdapConnectionSettings? Settings => _settings;

    public void Connect(LdapConnectionSettings settings)
    {
        Dispose();

        _settings = settings;

        var identifier = new LdapDirectoryIdentifier(settings.Server, settings.Port);
        _connection = new LdapConnection(identifier);

        _connection.SessionOptions.ProtocolVersion = 3;
        _connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        _connection.AuthType = AuthType.Basic;

        if (settings.UseSsl)
        {
            _connection.SessionOptions.SecureSocketLayer = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            _connection.Credential = new NetworkCredential(settings.Username, settings.Password);
        }

        _connection.Bind();
    }

    public async Task<List<LdapEntry>> GetChildren(string parentDn)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    parentDn,
                    "(objectClass=*)",
                    SearchScope.OneLevel,
                    null);

                request.SizeLimit = 1000;

                var response = (SearchResponse)_connection.SendRequest(request);
                var entries = new List<LdapEntry>();

                foreach (SearchResultEntry resultEntry in response.Entries)
                {
                    entries.Add(MapEntry(resultEntry));
                }

                return entries.OrderBy(e => e.DisplayName).ToList();
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> GetChildCount(string parentDn)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    parentDn,
                    "(objectClass=*)",
                    SearchScope.OneLevel,
                    ["1.1"]); // request no attributes for speed

                request.SizeLimit = 1000;

                var response = (SearchResponse)_connection.SendRequest(request);
                return response.Entries.Count;
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LdapEntry?> GetEntry(string dn)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    dn,
                    "(objectClass=*)",
                    SearchScope.Base,
                    null);

                var response = (SearchResponse)_connection.SendRequest(request);

                if (response.Entries.Count == 0)
                    return null;

                return MapEntry(response.Entries[0]);
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<LdapEntry>> Search(string baseDn, string filter)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    baseDn,
                    filter,
                    SearchScope.Subtree,
                    null);

                request.SizeLimit = 500;

                var response = (SearchResponse)_connection.SendRequest(request);
                var entries = new List<LdapEntry>();

                foreach (SearchResultEntry resultEntry in response.Entries)
                {
                    entries.Add(MapEntry(resultEntry));
                }

                return entries.OrderBy(e => e.Dn).ToList();
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<LdapEntry>> GetSubtree(string baseDn)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    baseDn,
                    "(objectClass=*)",
                    SearchScope.Subtree,
                    null);

                request.SizeLimit = 5000;

                var response = (SearchResponse)_connection.SendRequest(request);
                var entries = new List<LdapEntry>();

                foreach (SearchResultEntry resultEntry in response.Entries)
                {
                    entries.Add(MapEntry(resultEntry));
                }

                return entries.OrderBy(e => e.Dn).ToList();
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LdapSchema> GetSchema()
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                // Find the schema DN via RootDSE
                var rootDse = new SearchRequest(
                    "",
                    "(objectClass=*)",
                    SearchScope.Base,
                    ["subschemaSubentry"]);

                var rootResponse = (SearchResponse)_connection.SendRequest(rootDse);
                var schemaDn = "cn=Subschema";

                if (rootResponse.Entries.Count > 0)
                {
                    var entry = rootResponse.Entries[0];
                    if (entry.Attributes.Contains("subschemaSubentry"))
                        schemaDn = (string)entry.Attributes["subschemaSubentry"][0];
                }

                var schemaRequest = new SearchRequest(
                    schemaDn,
                    "(objectClass=*)",
                    SearchScope.Base,
                    ["objectClasses", "attributeTypes"]);

                var schemaResponse = (SearchResponse)_connection.SendRequest(schemaRequest);
                var schema = new LdapSchema();

                if (schemaResponse.Entries.Count > 0)
                {
                    var schemaEntry = schemaResponse.Entries[0];

                    if (schemaEntry.Attributes.Contains("objectClasses"))
                    {
                        var attr = schemaEntry.Attributes["objectClasses"];
                        for (int i = 0; i < attr.Count; i++)
                        {
                            var val = attr[i]?.ToString();
                            if (val != null)
                                schema.ObjectClasses.Add(ParseSchemaName(val));
                        }
                        schema.ObjectClasses.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    }

                    if (schemaEntry.Attributes.Contains("attributeTypes"))
                    {
                        var attr = schemaEntry.Attributes["attributeTypes"];
                        for (int i = 0; i < attr.Count; i++)
                        {
                            var val = attr[i]?.ToString();
                            if (val != null)
                                schema.AttributeTypes.Add(ParseSchemaName(val));
                        }
                        schema.AttributeTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    }
                }

                return schema;
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    private static SchemaItem ParseSchemaName(string definition)
    {
        var item = new SchemaItem { Definition = definition };

        // Extract NAME
        var nameIdx = definition.IndexOf("NAME ", StringComparison.Ordinal);
        if (nameIdx >= 0)
        {
            var afterName = definition[(nameIdx + 5)..].TrimStart();
            if (afterName.StartsWith("'"))
            {
                var end = afterName.IndexOf('\'', 1);
                if (end > 0)
                    item.Name = afterName[1..end];
            }
            else if (afterName.StartsWith("("))
            {
                var firstQuote = afterName.IndexOf('\'');
                var secondQuote = afterName.IndexOf('\'', firstQuote + 1);
                if (firstQuote >= 0 && secondQuote > firstQuote)
                    item.Name = afterName[(firstQuote + 1)..secondQuote];
            }
        }

        // Extract OID
        var parts = definition.TrimStart('(').TrimStart().Split(' ');
        if (parts.Length > 0 && parts[0].Contains('.'))
            item.Oid = parts[0];

        // Extract DESC
        var descIdx = definition.IndexOf("DESC '", StringComparison.Ordinal);
        if (descIdx >= 0)
        {
            var afterDesc = definition[(descIdx + 6)..];
            var end = afterDesc.IndexOf('\'');
            if (end > 0)
                item.Description = afterDesc[..end];
        }

        if (string.IsNullOrEmpty(item.Name))
            item.Name = item.Oid ?? "unknown";

        return item;
    }

    public bool TestBind(string userDn, string password)
    {
        if (_settings == null)
            throw new InvalidOperationException("Nicht verbunden");

        var identifier = new LdapDirectoryIdentifier(_settings.Server, _settings.Port);
        using var testConn = new LdapConnection(identifier);
        testConn.SessionOptions.ProtocolVersion = 3;
        testConn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        testConn.AuthType = AuthType.Basic;

        if (_settings.UseSsl)
        {
            testConn.SessionOptions.SecureSocketLayer = true;
        }

        testConn.Credential = new NetworkCredential(userDn, password);
        testConn.Bind();
        return true;
    }

    public async Task<Dictionary<string, int>> GetStatistics(string baseDn)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    baseDn,
                    "(objectClass=*)",
                    SearchScope.Subtree,
                    ["objectClass"]);

                request.SizeLimit = 10000;

                var response = (SearchResponse)_connection.SendRequest(request);
                var stats = new Dictionary<string, int>();

                foreach (SearchResultEntry entry in response.Entries)
                {
                    if (entry.Attributes.Contains("objectClass"))
                    {
                        var attr = entry.Attributes["objectClass"];
                        for (int i = 0; i < attr.Count; i++)
                        {
                            var val = attr[i]?.ToString() ?? "unknown";
                            stats[val] = stats.GetValueOrDefault(val) + 1;
                        }
                    }
                }

                return stats;
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Dictionary<string, int>> GetOuStatistics(string baseDn)
    {
        if (_connection == null)
            throw new InvalidOperationException("Nicht verbunden");

        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var request = new SearchRequest(
                    baseDn,
                    "(objectClass=*)",
                    SearchScope.Subtree,
                    ["1.1"]); // no attributes needed, just DNs

                request.SizeLimit = 10000;

                var response = (SearchResponse)_connection.SendRequest(request);
                var ouStats = new Dictionary<string, int>();

                foreach (SearchResultEntry entry in response.Entries)
                {
                    var dn = entry.DistinguishedName;
                    // Find the first OU in the DN
                    var parts = dn.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (trimmed.StartsWith("ou=", StringComparison.OrdinalIgnoreCase))
                        {
                            ouStats[trimmed] = ouStats.GetValueOrDefault(trimmed) + 1;
                            break;
                        }
                    }
                }

                return ouStats;
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    public static string ToLdif(LdapEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"dn: {entry.Dn}");
        foreach (var attr in entry.Attributes.OrderBy(a => a.Key))
        {
            foreach (var val in attr.Value)
            {
                if (val.StartsWith("[Binary:"))
                    sb.AppendLine($"{attr.Key}:: ");
                else
                    sb.AppendLine($"{attr.Key}: {val}");
            }
        }
        return sb.ToString();
    }

    public static string ToLdif(IEnumerable<LdapEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.Append(ToLdif(entry));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static LdapEntry MapEntry(SearchResultEntry resultEntry)
    {
        var entry = new LdapEntry
        {
            Dn = resultEntry.DistinguishedName,
            Attributes = new Dictionary<string, List<string>>()
        };

        foreach (DirectoryAttribute attr in resultEntry.Attributes.Values)
        {
            var values = new List<string>();
            for (int i = 0; i < attr.Count; i++)
            {
                var val = attr[i];
                if (val is byte[] bytes)
                {
                    try
                    {
                        var str = Encoding.UTF8.GetString(bytes);
                        if (str.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
                            values.Add($"[Binary: {bytes.Length} bytes]");
                        else
                            values.Add(str);
                    }
                    catch
                    {
                        values.Add($"[Binary: {bytes.Length} bytes]");
                    }
                }
                else
                {
                    values.Add(val?.ToString() ?? string.Empty);
                }
            }
            entry.Attributes[attr.Name] = values;
        }

        return entry;
    }

    public void Disconnect()
    {
        Dispose();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        _settings = null;
    }
}
