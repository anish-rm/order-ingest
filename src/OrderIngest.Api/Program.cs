using Microsoft.EntityFrameworkCore;
using OrderIngest.Business;
using OrderIngest.Business.Uber;
using OrderIngest.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<UberOptions>(
    builder.Configuration.GetSection(UberOptions.SectionName));

builder.Services.AddDbContext<OrderIngestDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Orders")));
builder.Services.AddScoped<OrderRepository>();

builder.Services.AddSingleton<UberSignatureVerifier>();
builder.Services.AddSingleton<WebhookQueue>();
builder.Services.AddHostedService<WebhookProcessingService>();

// The one-config-change swap: "Fixture" serves fixtures/uber-get-order.json,
// Currently I dont have Uber developer account, an app registered and approved for Eats API access, and a token flow
// So I implemented fixtureUberOrderClient for this demo.
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

app.MapControllers();

app.Run();
