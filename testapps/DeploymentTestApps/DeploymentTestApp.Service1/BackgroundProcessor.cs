using DeploymentTestApp.Service1;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace DeploymentTestApp.Service1;

internal class BackgroundProcessor : BackgroundService
{
    IDatabase _db;
    WebApp1Client _webApp1Client;


    public BackgroundProcessor(IConnectionMultiplexer mp, WebApp1Client webApp1Client)
    {
        _db = mp.GetDatabase();
        _webApp1Client = webApp1Client;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long printLine = 0;
        while(true)
        {
            await Task.Delay(1000);
            printLine++;
            if (printLine == long.MaxValue)
                printLine = 0;

            Console.WriteLine($"Print line: {printLine}");
            
            var processedMessages = await _db.StringIncrementAsync("printlines", printLine);
            Console.WriteLine($"Lines printed: {printLine}");

            var data = await _webApp1Client.PingWebApp1(cancellationToken: stoppingToken);
            Console.WriteLine($"Ping to WebApp1 health: {data}");
        }
    }
}
