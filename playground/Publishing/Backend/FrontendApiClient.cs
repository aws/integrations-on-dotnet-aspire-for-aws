using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Backend;

public class FrontendApiClient(HttpClient httpClient)
{
    public async Task<string> GetFrontendDataAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        var data = await httpClient.GetStringAsync("/backend/data", cancellationToken);
        return data;
    }
}
