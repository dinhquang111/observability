using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)
    .Enrich.WithSpan()
    .WriteTo.Console(new ElasticsearchJsonFormatter())
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Get environment variables
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";

// Create ActivitySource for custom spans
var activitySource = new ActivitySource("Authorization.Service");

Action<ResourceBuilder> appResourceBuilder =
    resource => resource
        .AddService(builder.Environment.ApplicationName);
// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(appResourceBuilder)
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Authorization.Service")
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
app.MapPost("/token", async () =>
    {
        // Create first child span (1 second work)
        using (var activity1 = activitySource.StartActivity("Query user permissions"))
        {
            // await Task.Delay(1000);
        }
        
        // Create second child span (2 second work)
        using (var activity2 = activitySource.StartActivity("Sign token"))
        {
            // await Task.Delay(2000);
        }
        
        return Results.Ok(new { Token = "fake-jwt-token-" + Guid.NewGuid() });
    })
    .WithName("GetToken");

app.Run();