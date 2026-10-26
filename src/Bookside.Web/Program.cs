using Bookside.Data;
using Bookside.Data.Sqlite;
using Bookside.Integrations;
using Bookside.Web;
using Bookside.Web.Components;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var databasePath = builder.Configuration["Bookside:DatabasePath"];
if (string.IsNullOrWhiteSpace(databasePath))
{
    throw new InvalidOperationException(
        "Configuration value Bookside:DatabasePath is required.");
}

// Antiforgery tokens and Blazor circuit state are protected with these keys;
// persisting them lets open pages and forms survive a container restart. 
// Unset = ASP.NET Core's default location.
var keysPath = builder.Configuration["Bookside:DataProtectionKeysPath"];

var dataProtection = builder
    .Services
    .AddDataProtection()
    .SetApplicationName("Bookside");

if (!string.IsNullOrWhiteSpace(keysPath))
{
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(
            builder.Environment.ContentRootPath,
            keysPath)));
}

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddBooksideSqlite(Path.Combine(
        builder.Environment.ContentRootPath,
        databasePath))
    .AddBooksideServices();

// Dictionary providers, tried in this order when a word is looked up.
builder.Services.AddFreeDictionaryApiClient();
builder.Services.AddDictionaryApiClient();

// Book metadata providers, tried in this order.
// Google Books only with a key (keyless calls get HTTP 429).
builder.Services.AddOpenLibraryClient();
if (builder.Configuration["Bookside:GoogleBooks:ApiKey"] is { Length: > 0 } googleBooksKey)
{
    builder.Services.AddGoogleBooksClient(o => o.ApiKey = googleBooksKey);
}

var app = builder.Build();

await app.Services.InitializeBooksideDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseAntiforgery();
app.MapStaticAssets();
app.MapCoverEndpoint();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Bookside.UI.CoverUrls).Assembly);

await app.RunAsync();
