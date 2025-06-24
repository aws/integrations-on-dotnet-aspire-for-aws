using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend
{
    internal class BackgroundProcessor : BackgroundService
    {
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
            }
        }
    }
}
