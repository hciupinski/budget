using Budget.Worker;
using Budget.Worker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
});

builder.Services.AddHealthChecks();
builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<ScheduledWorker>();

var app = builder.Build();

app.UseHttpLogging();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "worker",
    utcTime = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapHealthChecks("/health/live").AllowAnonymous();

app.Run();
