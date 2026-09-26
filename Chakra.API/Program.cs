using System.Text.Json.Serialization;
using Chakra.API.Configurations;
using Chakra.API.Endpoints;
using Chakra.API.Middlewares;


var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddPersistence(builder.Configuration)
    .AddMediatRSetup()
    .AddValidationSetup()
    .AddApplicationSetup(builder.Configuration)
    .AddOpenApiSetup()
    .AddAuthSetup(builder.Configuration);

var app = builder.Build();

app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
app.Logger.LogInformation("Application: {App}", app.Environment.ApplicationName);

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<EnsureUserMiddleware>();

app.UseOpenApiSetup();
app.UseCors();

app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();