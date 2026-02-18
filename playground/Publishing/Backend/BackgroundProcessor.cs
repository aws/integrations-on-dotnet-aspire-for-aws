using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Backend
{
    internal class BackgroundProcessor : BackgroundService
    {
        readonly IDatabase _db;
        readonly FrontendApiClient _frontendApiClient;

        public BackgroundProcessor(IConnectionMultiplexer mp, FrontendApiClient frontendApiClient)
        {
            _db = mp.GetDatabase();
            _frontendApiClient = frontendApiClient;
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
                Console.WriteLine($"Lines printed: {processedMessages}");

                var data = await _frontendApiClient.GetFrontendDataAsync(cancellationToken: stoppingToken);
                Console.WriteLine($"Data from frontend: {data}");
            }
        }
    }
}
