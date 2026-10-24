using Bookside.Data;
using Bookside.Data.Sqlite;
using Bookside.Integrations;
using Bookside.Web;
using Bookside.Web.Components;

var builder = WebApplication.CreateBuilder(args);

var databasePath = builder.Configuration["Bookside:DatabasePath"];
if (string.IsNullOrWhiteSpace(databasePath))
{
    throw new InvalidOperationException("Configuration value Bookside:DatabasePath is required.");
}

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services
    .AddBooksideSqlite(Path.Combine(builder.Environment.ContentRootPath, databasePath))
    .AddBooksideServices();
builder.Services.AddDictionaryApiClient();

var app = builder.Build();

await app.Services.InitializeBooksideDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapCoverEndpoint();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Bookside.UI.CoverUrls).Assembly);

await app.RunAsync();
