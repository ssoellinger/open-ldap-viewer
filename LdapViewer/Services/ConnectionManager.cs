using System.Text.Json;
using LdapViewer.Models;
using Microsoft.JSInterop;

namespace LdapViewer.Services;

public record ConnectionInfo(string Id, string Name, bool IsActive);

public class ConnectionManager : IDisposable
{
    private readonly Dictionary<string, LdapService> _connections = new();
    private readonly Dictionary<string, string> _connectionNames = new();

    public string? ActiveId { get; private set; }

    public LdapService? Active => ActiveId != null && _connections.TryGetValue(ActiveId, out var svc) ? svc : null;

    public bool IsConnected => Active?.IsConnected == true;

    public LdapConnectionSettings? Settings => Active?.Settings;

    public string AddConnection(LdapConnectionSettings settings)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var service = new LdapService();
        service.Connect(settings);

        _connections[id] = service;
        _connectionNames[id] = !string.IsNullOrWhiteSpace(settings.Name)
            ? settings.Name
            : $"{settings.Server}:{settings.Port}";

        ActiveId = id;
        return id;
    }

    public void RemoveConnection(string id)
    {
        if (_connections.TryGetValue(id, out var svc))
        {
            svc.Dispose();
            _connections.Remove(id);
            _connectionNames.Remove(id);

            if (ActiveId == id)
            {
                ActiveId = _connections.Keys.FirstOrDefault();
            }
        }
    }

    public void SetActive(string id)
    {
        if (_connections.ContainsKey(id))
        {
            ActiveId = id;
        }
    }

    public List<ConnectionInfo> GetAll()
    {
        return _connections.Keys
            .Select(id => new ConnectionInfo(id, _connectionNames.GetValueOrDefault(id, id), id == ActiveId))
            .ToList();
    }

    /// <summary>
    /// Tries to reconnect from localStorage saved connection. Returns true if successful.
    /// </summary>
    public async Task<bool> TryReconnectFromStorage(IJSRuntime js)
    {
        if (IsConnected) return true;

        try
        {
            var json = await js.InvokeAsync<string>("connectionStorage.loadLastConnection");
            if (string.IsNullOrEmpty(json)) return false;

            var settings = JsonSerializer.Deserialize<LdapConnectionSettings>(json);
            if (settings == null) return false;

            await Task.Run(() => AddConnection(settings));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var svc in _connections.Values)
        {
            svc.Dispose();
        }
        _connections.Clear();
        _connectionNames.Clear();
        ActiveId = null;
    }
}
