namespace LdapViewer.Models;

public static class LdapAttributeSuggestions
{
    public static readonly string[] Common =
    [
        // Identity
        "cn", "sn", "givenName", "displayName", "uid", "uidNumber", "gidNumber",
        "userPassword", "title", "initials", "description",

        // Contact
        "mail", "telephoneNumber", "facsimileTelephoneNumber", "mobile",
        "homePhone", "pager", "labeledURI",

        // Address
        "street", "l", "st", "postalCode", "postalAddress",
        "postOfficeBox", "c", "co", "preferredLanguage",

        // Organization
        "o", "ou", "businessCategory", "departmentNumber",
        "employeeNumber", "employeeType", "manager", "secretary",

        // Object
        "objectClass",

        // Groups / Members
        "member", "uniqueMember", "memberOf", "memberUid",
        "owner", "seeAlso",

        // POSIX / Unix
        "homeDirectory", "loginShell", "gecos",

        // X.500
        "serialNumber", "destinationIndicator", "registeredAddress",
        "preferredDeliveryMethod", "physicalDeliveryOfficeName",
        "teletexTerminalIdentifier", "x121Address",

        // Samba / AD-like
        "sAMAccountName", "userPrincipalName", "distinguishedName",
        "whenCreated", "whenChanged",

        // Certificates / Security
        "userCertificate", "userSMIMECertificate", "userPKCS12",
        "sshPublicKey", "authorizedService",

        // Photo / Binary
        "jpegPhoto", "photo", "audio",

        // DNS / Network
        "dNSHostName", "ipHostNumber", "macAddress",

        // Schema
        "structuralObjectClass", "entryDN", "subschemaSubentry",
        "hasSubordinates", "numSubordinates",

        // Misc
        "roomNumber", "carLicense", "info", "comment"
    ];

    public static string[] Filter(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Common;

        return Common
            .Where(a => a.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => !a.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a)
            .ToArray();
    }
}
