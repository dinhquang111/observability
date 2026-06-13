using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)
    .Enrich.WithSpan()
    .WriteTo.Console(new ElasticsearchJsonFormatter())
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

// Get environment variables
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var authorizationServiceUrl = Environment.GetEnvironmentVariable("AUTHORIZATION_SERVICE_URL");

if (string.IsNullOrEmpty(otlpEndpoint) || string.IsNullOrEmpty(authorizationServiceUrl))
    throw new ArgumentNullException(nameof(otlpEndpoint), $"{nameof(otlpEndpoint)} is null or empty");

Action<ResourceBuilder> appResourceBuilder =
    resource => resource
        .AddService(builder.Environment.ApplicationName);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(appResourceBuilder)
    .WithTracing(tracing => 
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri(otlpEndpoint);
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri(otlpEndpoint);
            });
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/healthz");
app.MapPost("/login", async (IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
    {
        // Simulate 500ms work
        // await Task.Delay(500); 
        // Call Authorization project's /token endpoint
        
        logger.LogInformation("User quang is logging in cls");
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsync($"{authorizationServiceUrl}/token", null);

        if (!response.IsSuccessStatusCode) 
            return Results.BadRequest("Failed to get token from authorization service");
        
        var content = await response.Content.ReadAsStringAsync();   
        return Results.Ok(content);

    })
    .WithName("Login");

app.Run();