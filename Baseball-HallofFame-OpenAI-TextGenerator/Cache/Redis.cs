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
        public bool IsRedisConnected = false;

        public Redis() 
        {
            // Connecting to Redis
            var connString = System.Environment.GetEnvironmentVariable("REDIS_CONNECTIONSTRING") ?? string.Empty;
            try
            {                 
                cache = ConnectionMultiplexer.Connect(connString, null).GetDatabase();
                IsRedisConnected = cache.IsConnected("test");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public bool ShouldAddNarrativeCount(MLBBatterInfo mlBBatterInfo)
        {
            var mlbBatterInfoNarrativeCountKey = string.Format("{0}:{1}", "NarrativeCount", mlBBatterInfo.ToString());
            var currentCount = 0;
            RedisValue? result = cache?.StringGet(mlbBatterInfoNarrativeCountKey);

            if (result.Value.IsNullOrEmpty)
            {
                currentCount = 0;
            }
            else
            {
                currentCount = (int) result;
            }

            if (currentCount < 5)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void AddNarrative(MLBBatterInfo mlBBatterInfo, string generatedNarrative)
        {
            var shouldAddNarrative = ShouldAddNarrativeCount(mlBBatterInfo);

            // Check if narrative should be cached (less than max for each player)
            if (shouldAddNarrative)
            {
                var mlbBatterInfoNarrativeKey = string.Format("{0}:{1}", "Narratives", mlBBatterInfo.ToString());
                var mlbBatterInfoNarrativeCountKey = string.Format("{0}:{1}", "NarrativeCount", mlBBatterInfo.ToString());

                RedisValue? result = cache?.StringGet(mlbBatterInfoNarrativeKey);
                var narratives = new List<NarrativeResult>();

                if (!result.Value.IsNullOrEmpty)
                {
                    narratives = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NarrativeResult>>(result);
                }

                // Add the new narrative
                narratives.Add(new NarrativeResult { Text = generatedNarrative });
                var narrativesJson = Newtonsoft.Json.JsonConvert.SerializeObject(narratives);

                // Increment the count
                cache?.StringIncrement(mlbBatterInfoNarrativeCountKey, 1);
                // Add the serialized JSON narratives
                cache?.StringSet(mlbBatterInfoNarrativeKey, narrativesJson);
            }
        }

        public void AddWebSearchResults(MLBBatterInfo mLBBatterInfo, string searchString, List<WebSearchResult> webSearchResults)
        {
            var webSearchResultsHash = Util.GetSequenceHashCode(webSearchResults);
            var searchStringHash = searchString.GetDeterministicHashCode();
            var mlbBatterInfoKey = string.Format("{0}:{1}-{2}", "WebSearchResults", mLBBatterInfo.ToString(), searchStringHash);

            var webSearchResultsJson = Newtonsoft.Json.JsonConvert.SerializeObject(webSearchResults);

            cache?.StringSet(mlbBatterInfoKey, webSearchResultsJson);
        }

        public List<NarrativeResult> GetNarratives(MLBBatterInfo mlBBatterInfo)
        {
            var mlbBatterInfoNarrativeKey = string.Format("{0}:{1}", "Narratives", mlBBatterInfo.ToString());

            RedisValue? result = cache?.StringGet(mlbBatterInfoNarrativeKey);

            if (result.Value.IsNullOrEmpty)
            {
                return new List<NarrativeResult>();
            }
            else
            {
                var narrativeResults = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NarrativeResult>>(result);
                return narrativeResults;
            }
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
