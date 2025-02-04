using WebMinusLambdaFunction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new HostApplicationBuilder();

builder.AddServiceDefaults();
builder.Services.AddHostedService<LambdaFunction>();

builder.Build().Run();