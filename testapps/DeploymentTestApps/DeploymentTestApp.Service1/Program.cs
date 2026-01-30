using DeploymentTestApp.Service1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .AddServiceDefaults();

builder.AddRedisClient("cache");
builder.Services.AddHostedService<BackgroundProcessor>();
builder.Services.AddHttpClient<WebApp1Client>(client =>
{
    // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
    // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
    client.BaseAddress = new("https+http://WebApp1");
});

await builder.Build().RunAsync();