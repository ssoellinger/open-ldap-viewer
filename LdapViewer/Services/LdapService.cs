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
        _connection = CreateConnection(settings, settings.Username, settings.Password);
        _connection.Bind();
    }

    private LdapConnection EnsureConnected()
    {
        return _connection ?? throw new InvalidOperationException("Nicht verbunden");
    }

    private async Task<T> WithConnection<T>(Func<LdapConnection, T> action)
    {
        var conn = EnsureConnected();
        await _lock.WaitAsync();
        try
        {
            return await Task.Run(() => action(conn));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WithConnection(Action<LdapConnection> action)
    {
        var conn = EnsureConnected();
        await _lock.WaitAsync();
        try
        {
            await Task.Run(() => action(conn));
        }
        finally
        {
            _lock.Release();
        }
    }

    private static LdapConnection CreateConnection(LdapConnectionSettings settings, string? bindDn, string? bindPassword)
    {
        var identifier = new LdapDirectoryIdentifier(settings.Server, settings.Port);
        var conn = new LdapConnection(identifier);

        conn.SessionOptions.ProtocolVersion = 3;
        conn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        conn.AuthType = AuthType.Basic;

        if (settings.UseSsl)
            conn.SessionOptions.SecureSocketLayer = true;

        if (!string.IsNullOrWhiteSpace(bindDn))
            conn.Credential = new NetworkCredential(bindDn, bindPassword);

        return conn;
    }

    public async Task<List<LdapEntry>> GetChildren(string parentDn)
    {
        return await WithConnection(conn =>
        {
            return PagedSearch(conn, parentDn, "(objectClass=*)", SearchScope.OneLevel, ["1.1"])
                .Select(MapEntry)
                .OrderBy(e => e.DisplayName)
                .ToList();
        });
    }

    public async Task<int> GetChildCount(string parentDn)
    {
        return await WithConnection(conn =>
        {
            return PagedSearch(conn, parentDn, "(objectClass=*)", SearchScope.OneLevel, ["1.1"]).Count;
        });
    }

    public async Task<bool> HasChildren(string parentDn)
    {
        return await WithConnection(conn =>
        {
            var request = new SearchRequest(parentDn, "(objectClass=*)", SearchScope.OneLevel, ["1.1"]);
            request.SizeLimit = 1;
            var response = (SearchResponse)conn.SendRequest(request);
            return response.Entries.Count > 0;
        });
    }

    public async Task<LdapEntry?> GetEntry(string dn)
    {
        return await WithConnection(conn =>
        {
            var request = new SearchRequest(dn, "(objectClass=*)", SearchScope.Base, null);
            var response = (SearchResponse)conn.SendRequest(request);
            return response.Entries.Count == 0 ? null : MapEntry(response.Entries[0]);
        });
    }

    public async Task<List<LdapEntry>> Search(string baseDn, string filter)
    {
        return await WithConnection(conn =>
        {
            return PagedSearch(conn, baseDn, filter, SearchScope.Subtree, null)
                .Select(MapEntry)
                .OrderBy(e => e.Dn)
                .ToList();
        });
    }

    public async Task<List<LdapEntry>> GetSubtree(string baseDn)
    {
        return await WithConnection(conn =>
        {
            return PagedSearch(conn, baseDn, "(objectClass=*)", SearchScope.Subtree, null)
                .Select(MapEntry)
                .OrderBy(e => e.Dn)
                .ToList();
        });
    }

    public List<string> GetNamingContexts()
    {
        var conn = EnsureConnected();
        var request = new SearchRequest("", "(objectClass=*)", SearchScope.Base,
            ["namingContexts", "defaultNamingContext"]);
        var response = (SearchResponse)conn.SendRequest(request);

        string? defaultContext = null;
        var contexts = new List<string>();

        if (response.Entries.Count > 0)
        {
            var entry = response.Entries[0];

            if (entry.Attributes.Contains("defaultNamingContext"))
                defaultContext = (string)entry.Attributes["defaultNamingContext"][0];

            if (entry.Attributes.Contains("namingContexts"))
            {
                var attr = entry.Attributes["namingContexts"];
                for (int i = 0; i < attr.Count; i++)
                    contexts.Add((string)attr[i]);
            }
        }

        // Put defaultNamingContext first if available
        if (defaultContext != null)
        {
            contexts.Remove(defaultContext);
            contexts.Insert(0, defaultContext);
        }

        return contexts;
    }

    public async Task<LdapSchema> GetSchema()
    {
        return await WithConnection(conn =>
        {
            // Find the schema DN via RootDSE
            var rootDse = new SearchRequest("", "(objectClass=*)", SearchScope.Base, ["subschemaSubentry"]);
            var rootResponse = (SearchResponse)conn.SendRequest(rootDse);
            var schemaDn = "cn=Subschema";

            if (rootResponse.Entries.Count > 0)
            {
                var entry = rootResponse.Entries[0];
                if (entry.Attributes.Contains("subschemaSubentry"))
                    schemaDn = (string)entry.Attributes["subschemaSubentry"][0];
            }

            var schemaRequest = new SearchRequest(schemaDn, "(objectClass=*)", SearchScope.Base, ["objectClasses", "attributeTypes"]);
            var schemaResponse = (SearchResponse)conn.SendRequest(schemaRequest);
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

        using var testConn = CreateConnection(_settings, userDn, password);
        testConn.Bind();
        return true;
    }

    public async Task<Dictionary<string, int>> GetStatistics(string baseDn)
    {
        return await WithConnection(conn =>
        {
            var entries = PagedSearch(conn, baseDn, "(objectClass=*)", SearchScope.Subtree, ["objectClass"]);
            var stats = new Dictionary<string, int>();

            foreach (SearchResultEntry entry in entries)
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

    public async Task<Dictionary<string, int>> GetOuStatistics(string baseDn)
    {
        return await WithConnection(conn =>
        {
            var entries = PagedSearch(conn, baseDn, "(objectClass=*)", SearchScope.Subtree, ["1.1"]);
            var ouStats = new Dictionary<string, int>();

            foreach (SearchResultEntry entry in entries)
            {
                var dn = entry.DistinguishedName;
                foreach (var part in dn.Split(','))
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

    public async Task ModifyEntry(string dn, List<LdapModification> modifications)
    {
        await WithConnection(conn =>
        {
            var request = new ModifyRequest(dn);

            foreach (var mod in modifications)
            {
                var dirMod = new DirectoryAttributeModification
                {
                    Name = mod.AttributeName,
                    Operation = mod.Type switch
                    {
                        LdapModificationType.Add => DirectoryAttributeOperation.Add,
                        LdapModificationType.Delete => DirectoryAttributeOperation.Delete,
                        LdapModificationType.Replace => DirectoryAttributeOperation.Replace,
                        _ => DirectoryAttributeOperation.Replace
                    }
                };

                if (mod.Type == LdapModificationType.Delete && mod.OldValue != null)
                    dirMod.Add(mod.OldValue);
                else if (mod.NewValue != null)
                    dirMod.Add(mod.NewValue);

                request.Modifications.Add(dirMod);
            }

            conn.SendRequest(request);
        });
    }

    public async Task CreateEntry(string dn, Dictionary<string, List<string>> attributes)
    {
        await WithConnection(conn =>
        {
            var request = new AddRequest(dn);
            foreach (var attr in attributes)
                request.Attributes.Add(new DirectoryAttribute(attr.Key, attr.Value.ToArray()));
            conn.SendRequest(request);
        });
    }

    public async Task MoveEntry(string dn, string newRdn, string? newParentDn)
    {
        await WithConnection(conn =>
        {
            conn.SendRequest(new ModifyDNRequest(dn, newParentDn, newRdn));
        });
    }

    public async Task SetPassword(string dn, string password, string hashAlgorithm)
    {
        var hashedPassword = HashPassword(password, hashAlgorithm);
        await ModifyEntry(dn, [new LdapModification
        {
            AttributeName = "userPassword",
            NewValue = hashedPassword,
            Type = LdapModificationType.Replace
        }]);
    }

    public static string HashPassword(string password, string algorithm)
    {
        var bytes = Encoding.UTF8.GetBytes(password);

        switch (algorithm.ToUpperInvariant())
        {
            case "SSHA":
            {
                var salt = new byte[8];
                using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
                rng.GetBytes(salt);
                using var sha = System.Security.Cryptography.SHA1.Create();
                var combined = new byte[bytes.Length + salt.Length];
                Buffer.BlockCopy(bytes, 0, combined, 0, bytes.Length);
                Buffer.BlockCopy(salt, 0, combined, bytes.Length, salt.Length);
                var hash = sha.ComputeHash(combined);
                var result = new byte[hash.Length + salt.Length];
                Buffer.BlockCopy(hash, 0, result, 0, hash.Length);
                Buffer.BlockCopy(salt, 0, result, hash.Length, salt.Length);
                return "{SSHA}" + Convert.ToBase64String(result);
            }
            case "SHA":
            {
                using var sha = System.Security.Cryptography.SHA1.Create();
                return "{SHA}" + Convert.ToBase64String(sha.ComputeHash(bytes));
            }
            case "SHA256":
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                return "{SHA256}" + Convert.ToBase64String(sha.ComputeHash(bytes));
            }
            case "SHA512":
            {
                using var sha = System.Security.Cryptography.SHA512.Create();
                return "{SHA512}" + Convert.ToBase64String(sha.ComputeHash(bytes));
            }
            case "MD5":
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                return "{MD5}" + Convert.ToBase64String(md5.ComputeHash(bytes));
            }
            default:
                return password;
        }
    }

    public async Task DeleteEntry(string dn)
    {
        await WithConnection(conn =>
        {
            conn.SendRequest(new DeleteRequest(dn));
        });
    }

    public static List<LdifOperation> ParseLdif(string ldifContent)
    {
        var operations = new List<LdifOperation>();
        var blocks = ldifContent.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
                .ToList();

            if (lines.Count == 0) continue;

            var op = new LdifOperation();
            string? currentDn = null;
            LdifChangeType? changeType = null;
            string? currentModAttr = null;
            LdapModificationType? currentModType = null;

            foreach (var line in lines)
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;

                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].TrimStart();

                // Handle base64 encoded values (key:: value)
                if (value.StartsWith(":"))
                {
                    value = value[1..].TrimStart();
                    try
                    {
                        value = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                    }
                    catch { /* use raw value */ }
                }

                if (key.Equals("dn", StringComparison.OrdinalIgnoreCase))
                {
                    currentDn = value;
                    op.Dn = value;
                }
                else if (key.Equals("changetype", StringComparison.OrdinalIgnoreCase))
                {
                    changeType = value.ToLowerInvariant() switch
                    {
                        "add" => LdifChangeType.Add,
                        "modify" => LdifChangeType.Modify,
                        "delete" => LdifChangeType.Delete,
                        _ => LdifChangeType.Add
                    };
                    op.ChangeType = changeType.Value;
                }
                else if (key.Equals("add", StringComparison.OrdinalIgnoreCase) && changeType == LdifChangeType.Modify)
                {
                    currentModAttr = value;
                    currentModType = LdapModificationType.Add;
                }
                else if (key.Equals("replace", StringComparison.OrdinalIgnoreCase) && changeType == LdifChangeType.Modify)
                {
                    currentModAttr = value;
                    currentModType = LdapModificationType.Replace;
                }
                else if (key.Equals("delete", StringComparison.OrdinalIgnoreCase) && changeType == LdifChangeType.Modify)
                {
                    currentModAttr = value;
                    currentModType = LdapModificationType.Delete;
                }
                else if (line.Trim() == "-")
                {
                    currentModAttr = null;
                    currentModType = null;
                }
                else if (currentModAttr != null && currentModType != null && key.Equals(currentModAttr, StringComparison.OrdinalIgnoreCase))
                {
                    op.Modifications.Add(new LdapModification
                    {
                        AttributeName = currentModAttr,
                        NewValue = currentModType != LdapModificationType.Delete ? value : null,
                        OldValue = currentModType == LdapModificationType.Delete ? value : null,
                        Type = currentModType.Value
                    });
                }
                else if (!key.Equals("dn", StringComparison.OrdinalIgnoreCase) &&
                         !key.Equals("changetype", StringComparison.OrdinalIgnoreCase))
                {
                    if (!op.Attributes.ContainsKey(key))
                        op.Attributes[key] = new List<string>();
                    op.Attributes[key].Add(value);
                }
            }

            if (currentDn == null) continue;

            if (changeType == null)
                op.ChangeType = LdifChangeType.Add;

            operations.Add(op);
        }

        return operations;
    }

    public async Task<List<LdifResult>> ApplyLdif(List<LdifOperation> operations)
    {
        var results = new List<LdifResult>();

        foreach (var op in operations)
        {
            var result = new LdifResult { Dn = op.Dn, ChangeType = op.ChangeType };

            try
            {
                switch (op.ChangeType)
                {
                    case LdifChangeType.Add:
                        await CreateEntry(op.Dn, op.Attributes);
                        break;
                    case LdifChangeType.Modify:
                        await ModifyEntry(op.Dn, op.Modifications);
                        break;
                    case LdifChangeType.Delete:
                        await DeleteEntry(op.Dn);
                        break;
                }
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            results.Add(result);
        }

        return results;
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

    private static List<SearchResultEntry> PagedSearch(LdapConnection conn, string baseDn, string filter, SearchScope scope, string[]? attributes, int pageSize = 1000)
    {
        var results = new List<SearchResultEntry>();
        var request = new SearchRequest(baseDn, filter, scope, attributes);
        var pageControl = new PageResultRequestControl(pageSize);
        request.Controls.Add(pageControl);

        while (true)
        {
            var response = (SearchResponse)conn.SendRequest(request);

            foreach (SearchResultEntry entry in response.Entries)
                results.Add(entry);

            var responseControl = response.Controls
                .OfType<PageResultResponseControl>()
                .FirstOrDefault();

            if (responseControl == null || responseControl.Cookie.Length == 0)
                break;

            pageControl.Cookie = responseControl.Cookie;
        }

        return results;
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

    public async Task<byte[]?> GetBinaryAttribute(string dn, string attributeName)
    {
        return await WithConnection(conn =>
        {
            var request = new SearchRequest(dn, "(objectClass=*)", SearchScope.Base, [attributeName]);
            var response = (SearchResponse)conn.SendRequest(request);
            if (response.Entries.Count == 0) return null;

            var entry = response.Entries[0];
            if (!entry.Attributes.Contains(attributeName)) return null;

            var attr = entry.Attributes[attributeName];
            if (attr.Count == 0) return null;

            var val = attr[0];
            return val is byte[] bytes ? bytes : Encoding.UTF8.GetBytes(val?.ToString() ?? "");
        });
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
