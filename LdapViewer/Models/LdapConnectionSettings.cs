using System.ComponentModel.DataAnnotations;

namespace LdapViewer.Models;

public class LdapConnectionSettings
{
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Server ist erforderlich")]
    public string Server { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Port muss zwischen 1 und 65535 liegen")]
    public int Port { get; set; } = 389;

    [Required(ErrorMessage = "Base DN ist erforderlich")]
    public string BaseDn { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; }
}
