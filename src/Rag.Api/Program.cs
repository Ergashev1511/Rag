using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Rag.Api.Endpoints;
using Rag.Api.Middleware;
using Rag.Api.Security;
using Rag.Application;
using Rag.Application.Abstractions;
using Rag.Application.Options;
using Rag.Infrastructure;
using Rag.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwagger();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Yuklash limiti: PDF hajmi + multipart uchun zaxira.
var maxFileSizeMb = builder.Configuration.GetSection(RagOptions.SectionName).Get<RagOptions>()?.Ingestion.MaxFileSizeMb ?? 50;
var maxRequestBytes = (maxFileSizeMb + 1) * 1024L * 1024L;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxRequestBytes);

// Chat: foydalanuvchi bo'yicha limit (free LLM kvotasini himoya qiladi).
var chatPermitPerMinute = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()?.ChatRequestsPerMinute ?? 20;
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ChatEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Request.Headers[HttpCurrentUser.HeaderName].ToString() is { Length: > 0 } userId
            ? userId
            : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = chatPermitPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();

if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.MapDocumentsEndpoints();
app.MapChatEndpoints();

app.Run();
