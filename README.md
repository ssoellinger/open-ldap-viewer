# Open LDAP Viewer

A web-based LDAP directory browser built with Blazor Server (.NET 8) and Bootstrap.

## Features

- **Connection Management** - Save, load and quick-connect to LDAP servers (SSL/TLS supported)
- **Tree Browser** - Hierarchical directory tree with expand all, lazy loading and child counts
- **Search** - Attribute-based search (CN, UID, Mail, etc.) or raw LDAP filter with search history
- **DN Breadcrumb Navigation** - Click on DN segments to navigate the directory hierarchy
- **Bookmarks** - Pin frequently used entries for quick access
- **Group Member Resolution** - `member`, `uniqueMember`, `memberOf` attributes rendered as clickable links
- **Schema Browser** - Browse objectClasses and attributeTypes with OID and description
- **Statistics Dashboard** - Entry counts by objectClass and OU with progress bar visualization
- **Password Check** - Test LDAP bind with user DN and password
- **Dark Mode** - Toggle between light and dark theme, persisted in localStorage
- **LDIF Export** - Export single entries, search results or the entire subtree as LDIF

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Access to an LDAP server (e.g. OpenLDAP, Active Directory)

## Getting Started

```bash
cd LdapViewer
dotnet run
```

Open http://localhost:5161 in your browser.

## Screenshots

### Light Mode
Connect to an LDAP server, browse the directory tree, search entries and view attributes.

### Dark Mode
Toggle dark mode via the button in the navigation bar. The setting persists across sessions.

## Tech Stack

- **Blazor Server** (.NET 8) - Interactive server-side rendering
- **System.DirectoryServices.Protocols** - LDAP communication
- **Bootstrap 5** - UI framework
- **localStorage** - Client-side persistence for connections, bookmarks, search history and theme

## Project Structure

```
LdapViewer/
  Components/
    Layout/MainLayout.razor    - Navigation bar with dark mode toggle
    Pages/
      Home.razor               - Connection form + password check
      Browser.razor            - Tree browser + search + detail view
      Schema.razor             - Schema browser
      Statistics.razor         - Statistics dashboard
    LdapEntryDetail.razor      - Entry detail with breadcrumb + group links
    LdapTree.razor             - Recursive tree component
  Models/                      - LdapEntry, LdapConnectionSettings, LdapSchema
  Services/LdapService.cs      - LDAP operations (connect, search, bind test, stats)
  wwwroot/
    css/ldap.css               - Custom styles + dark mode
    js/storage.js              - localStorage helpers + dark mode toggle
```

## License

MIT
