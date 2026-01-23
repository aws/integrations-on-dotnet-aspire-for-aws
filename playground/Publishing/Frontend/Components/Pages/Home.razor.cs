using Microsoft.AspNetCore.Components;
using StackExchange.Redis;

namespace Frontend.Components.Pages
{
    public partial class Home : ComponentBase
    {
        protected override async Task OnInitializedAsync()
        {
            try
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync(new RedisKey("cacheString"), new RedisValue("Hello World"));
                CacheString = await db.StringGetAsync(new RedisKey("cacheString"));
                logger.LogInformation("Set and Get value from Redis: {0}", CacheString);
            }
            catch (Exception ex)
            {
                CacheString = "Failed to cache: " + ex.Message;
            }
        }

        public string? CacheString { get; set; }
    }
}
