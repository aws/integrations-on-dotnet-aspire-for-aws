using Microsoft.AspNetCore.Components;
using StackExchange.Redis;

namespace Frontend.Components.Pages
{
    public partial class Home : ComponentBase
    {
        protected override async Task OnInitializedAsync()
        {
            //var db = redis.GetDatabase();
            //await db.StringSetAsync(new RedisKey("cacheString"), new RedisValue("Hello World"));
            //CacheString = await db.StringGetAsync(new RedisKey("cacheString"));
        }

        public string? CacheString { get; set; }
    }
}
