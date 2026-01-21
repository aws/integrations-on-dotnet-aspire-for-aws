
namespace DeploymentTestApps.WebApp2;

public class WebApp1Client(ILogger<WebApp1Client> logger, HttpClient httpClient)
{
    public async Task<bool> PingWebApp1(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await httpClient.GetStringAsync("/ping", cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed ping to WebApp1");
            return false;
        }

        return true;
    }
}
