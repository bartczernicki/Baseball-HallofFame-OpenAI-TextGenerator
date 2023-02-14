using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Bing.WebSearch;
using Microsoft.Bing.WebSearch.Models;
using Grpc.Net.Client.Configuration;
using System.Globalization;

namespace Baseball_HallofFame_OpenAI_TextGenerator
{
    public class GenerateHallOfFameText
    {
        private readonly ILogger _logger;

        public GenerateHallOfFameText(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GenerateHallOfFameText>();
        }

        [Function("GenerateHallOfFameText")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("GenerateHallOfFameText - Enter");
            HttpResponseData response = req.CreateResponse();

            if (req.Method == "GET")
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                response.WriteString("API is working");
            }
            else // POST
            {
                // Request process Body
                var requestBodyString = await req.GetRawBodyAsync(Encoding.UTF8);
                _logger.LogInformation("GenerateHallOfFameText - Request Body: " + requestBodyString);

                if (requestBodyString.Length < 20)
                {
                    _logger.LogInformation("GenerateHallOfFameText - Not Enough to process error");
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.WriteString("Message request does not have enough inofrmation in the body request. Need MLBBatterInfo contract.");
                    return response;
                }

                var mlbBatterInfo = JsonSerializer.Deserialize<MLBBatterInfo>(requestBodyString);
                _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo Deserialized");

                // Bing Search
                var searchString = string.Format("{0} baseball Hall of Fame", mlbBatterInfo?.FullPlayerName);
                var bingSearchKey = System.Environment.GetEnvironmentVariable("BING_SEARCH_KEY");

                var bingSearchClient = new WebSearchClient(new ApiKeyServiceClientCredentials(bingSearchKey));
                var bingWebData = await bingSearchClient.Web.SearchAsync(query: searchString, count: 8);

                var webSearchResults = "Web search results:\r\n\r\n";
                var footNotes = string.Empty;
                var bingSearchId = 0;
                if (bingWebData?.WebPages?.Value?.Count > 0)
                {
                    // Itertate over the Bing Web Pages
                    foreach(var bingWebPage in bingWebData.WebPages.Value)
                    {
                        bingSearchId++;

                        webSearchResults += string.Format("[{0}] \"{1}. {2}\"\r\nURL: {3}\r\n\r\n", 
                            bingSearchId, bingWebPage.Name, bingWebPage.Snippet, bingWebPage.Url);

                        footNotes += string.Format("{0}. {1}: {2}\r\n",
                            bingSearchId, bingWebPage.Name, bingWebPage.Url);
                    }
                }

                var promptInstructions = string.Format("The current date is {5}. Using the provided Web search results and information in the query, write a comprehensive reply to the given query. " +
                    "Make sure to cite results using [[number](URL)] notation after the reference. " +
                    "If the provided search results refer to multiple subjects with the same name, write separate answers for each subject. " +
                    "Query: An AI model states the probability of baseball hall of fame induction for {0} as {1}. {0} has played baseball for {2} years. Provide a detailed case supporting or against {0} to be considered for the Hall of Fame.\r\n",
                    mlbBatterInfo?.FullPlayerName, mlbBatterInfo?.HallOfFameProbability.ToString("P", CultureInfo.InvariantCulture), mlbBatterInfo?.YearsPlayed,
                    mlbBatterInfo?.HR, mlbBatterInfo?.TotalPlayerAwards, DateTime.Now.ToString("M/d/yyyy"));

                // Successful response (OK)
                response.StatusCode = HttpStatusCode.OK;
                response.WriteString(webSearchResults + promptInstructions + footNotes);
            }

            _logger.LogInformation("GenerateHallOfFameText - End");

            return response;
        }
    }
}
