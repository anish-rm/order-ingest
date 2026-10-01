using Microsoft.EntityFrameworkCore;
using OrderIngest.Api.Endpoints;
using OrderIngest.Business;
using OrderIngest.Business.Uber;
using OrderIngest.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<UberOptions>(
    builder.Configuration.GetSection(UberOptions.SectionName));

builder.Services.AddDbContext<OrderIngestDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Orders")));
builder.Services.AddScoped<OrderRepository>();

builder.Services.AddSingleton<UberSignatureVerifier>();
builder.Services.AddSingleton<WebhookQueue>();
builder.Services.AddHostedService<WebhookProcessingService>();

// The one-config-change swap: "Fixture" serves fixtures/uber-get-order.json,
// "Http" does the real GET against resource_href.
if (builder.Configuration["Uber:OrderClientMode"] == "Http")
{
    builder.Services.AddHttpClient<IUberOrderClient, HttpUberOrderClient>();
}
else
{
    builder.Services.AddSingleton<IUberOrderClient, FixtureUberOrderClient>();
}

builder.Services.AddOpenApi();

var app = builder.Build();

// Demo-scope schema management; production would use EF migrations.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<OrderIngestDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapWebhookEndpoints();
app.MapOrderEndpoints();

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in tests.
public partial class Program;
