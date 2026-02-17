using LdapViewer.Components;
using LdapViewer.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();

// ConnectionManager manages multiple LdapService instances per circuit (session)
builder.Services.AddScoped<ConnectionManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

var supportedCultures = new[] { "de", "en" };
app.UseRequestLocalization(opt =>
{
    opt.SetDefaultCulture("de");
    opt.AddSupportedCultures(supportedCultures);
    opt.AddSupportedUICultures(supportedCultures);
    opt.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
