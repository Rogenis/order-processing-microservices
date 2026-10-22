using System.Text.Json.Serialization;
using Orders.Api.Endpoints;
using Orders.Api.Security;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOrderStore(builder.Configuration);
builder.Services.AddOrderEventPublisher(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithTags("Health");
app.MapOrderEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
