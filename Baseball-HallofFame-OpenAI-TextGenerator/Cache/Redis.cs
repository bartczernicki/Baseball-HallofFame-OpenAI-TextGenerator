using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Baseball_HallofFame_OpenAI_TextGenerator.Shared;
using StackExchange.Redis;

namespace Baseball_HallofFame_OpenAI_TextGenerator.Cache
{
    public class Redis
    {
        private IDatabase? cache = null;

        public Redis() 
        {
            // Connecting to Redis
            var connString = System.Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING");
            cache  = ConnectionMultiplexer.Connect(connString, null).GetDatabase();
        }

        public void AddObject(string key, object obj)
        {
            cache?.StringSet(key, Newtonsoft.Json.JsonConvert.SerializeObject(obj));
        }

        public void AddMLBBatterWebSearchResults(MLBBatterInfo mLBBatterInfo, string searchString, List<WebSearchResult> webSearchResults)
        {
            var webSearchResultsHash = Util.GetSequenceHashCode(webSearchResults);
            var searchStringHash = searchString.GetDeterministicHashCode();
            var mlbBatterInfoKey = string.Format("{0}:{1}-{2}", "WebSearchResults", mLBBatterInfo.ToString(), searchStringHash);

            var webSearchResultsJson = Newtonsoft.Json.JsonConvert.SerializeObject(webSearchResults);

            cache?.StringSet(mlbBatterInfoKey, webSearchResultsJson);
        }

        public List<WebSearchResult> GetWebSearchResults(MLBBatterInfo mLBBatterInfo, string searchString)
        {
            var searchStringHash = searchString.GetDeterministicHashCode();
            var mlbBatterInfoKey = string.Format("{0}:{1}-{2}", "WebSearchResults", mLBBatterInfo.ToString(), searchStringHash);

            RedisValue? result = cache?.StringGet(mlbBatterInfoKey);

            if (result.Value.IsNullOrEmpty)
            {
                return new List<WebSearchResult>();
            }
            else
            {
                var webSearchResults = Newtonsoft.Json.JsonConvert.DeserializeObject<List<WebSearchResult>>(result);
                return webSearchResults;
            }

        }
    }
}
