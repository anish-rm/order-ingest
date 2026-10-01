using Microsoft.EntityFrameworkCore;
using OrderIngest.Business;
using OrderIngest.Business.Retry;
using OrderIngest.Business.Uber;
using OrderIngest.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();


builder.Services.AddOptions<UberOptions>()
    .BindConfiguration(UberOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret),
        "Uber:ClientSecret must be configured (the dev-only value lives in appsettings.Development.json)")
    .ValidateOnStart();

builder.Services.AddDbContext<OrderIngestDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Orders")));
builder.Services.AddScoped<OrderRepository>();

builder.Services.AddSingleton<UberSignatureVerifier>();
builder.Services.AddSingleton<RetryExecutor>();
builder.Services.AddSingleton<WebhookQueue>();
builder.Services.AddHostedService<WebhookProcessingService>();

// Order-client selection is a single config switch (Uber:OrderClientMode).
// "Http" performs the real GET against resource_href; the default "Fixture"
// serves fixtures/uber-get-order.json, because the real call requires Uber
// developer credentials (OAuth token) that a local demo cannot have.
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

app.UseExceptionHandler();

// Demo-scope schema management; production would use EF migrations.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<OrderIngestDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/healthz");
app.MapControllers();

app.Run();
