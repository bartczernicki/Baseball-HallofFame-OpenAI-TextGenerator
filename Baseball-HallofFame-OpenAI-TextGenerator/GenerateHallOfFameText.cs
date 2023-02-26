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
using System;
using System.Net.Http.Headers;
using Baseball_HallofFame_OpenAI_TextGenerator.Shared;

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
                response.WriteString("Generate Hall of Fame API is working");
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

                // Bing - Web Search Components
                var searchString = string.Format("{0} baseball Hall of Fame", mlbBatterInfo?.FullPlayerName);
                var webSearchResultsString = "Web search results:\r\n\r\n";
                var footNotes = string.Empty;
                var bingSearchId = 0;

                // Cache - Initialize Cache (Redis) & Check Cache
                var redisCache = new Cache.Redis();
                var webSearchResults = redisCache.GetWebSearchResults(mlbBatterInfo, searchString);

                if(webSearchResults.Count> 0)
                {
                    _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo found in Cache");

                    // Itertate over the Bing Web Pages (Cache)
                    foreach (var bingWebPage in webSearchResults)
                    {
                        bingSearchId++;

                        webSearchResultsString += string.Format("[{0}]: \"{1}: {2}\"\r\nURL: {3}\r\n\r\n",
                            bingSearchId, bingWebPage.Name, bingWebPage.Snippet, bingWebPage.Url);

                        footNotes += string.Format("[{0}]: {1}: {2}  \r\n",
                            bingSearchId, bingWebPage.Name, bingWebPage.Url);
                    }
                }
                else
                {
                    _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo NOT FOUND in Cache");

                    var bingSearchKey = System.Environment.GetEnvironmentVariable("BING_SEARCH_KEY");
                    var bingSearchClient = new WebSearchClient(new ApiKeyServiceClientCredentials(bingSearchKey));
                    var bingWebData = await bingSearchClient.Web.SearchAsync(query: searchString, count: 10);


                    if (bingWebData?.WebPages?.Value?.Count > 0)
                    {
                        // Itertate over the Bing Web Pages (Non-Cache Results)
                        foreach (var bingWebPage in bingWebData.WebPages.Value)
                        {
                            bingSearchId++;

                            webSearchResultsString += string.Format("[{0}]: \"{1}: {2}\"\r\nURL: {3}\r\n\r\n",
                                bingSearchId, bingWebPage.Name, bingWebPage.Snippet, bingWebPage.Url);

                            footNotes += string.Format("[{0}]: {1}: {2}  \r\n",
                                bingSearchId, bingWebPage.Name, bingWebPage.Url);

                            webSearchResults.Add(new WebSearchResult
                            {
                                Id = bingSearchId,
                                Name = bingWebPage.Name,
                                Snippet = bingWebPage.Snippet,
                                Url = bingWebPage.Url
                            });
                        }

                        // Add to Cache
                        redisCache.AddMLBBatterWebSearchResults(mlbBatterInfo, searchString, webSearchResults);
                    }
                }


                // OpenAI - Text Generator Components
                var promptInstructions = string.Format("The current date is {5}. Using most of the provided Web search results and probability and statistics found in the given query, write a comprehensive reply to the given query. " +
                    "Make sure to cite results using [number] notation of each URL after the reference. " +
                    "If the provided search results refer to multiple subjects with the same name, write separate answers for each subject. " +
                    "Query: An AI model states the probability of baseball hall of fame induction for {0} as {1}. {0} has played baseball for {2} years. Provide a detailed case supporting or against {0} to be considered for the Hall of Fame.\r\n",
                    mlbBatterInfo?.FullPlayerName, mlbBatterInfo?.HallOfFameProbability.ToString("P", CultureInfo.InvariantCulture), mlbBatterInfo?.YearsPlayed,
                    mlbBatterInfo?.HR, mlbBatterInfo?.TotalPlayerAwards, DateTime.Now.ToString("M/d/yyyy"));

                // OpenAI - Retrieve Keys
                var azureOpenAIKey = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
                var azureOpenAIDeployment = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

                using var client = new HttpClient();
                client.BaseAddress = new Uri(string.Format("https://{0}.openai.azure.com/openai/deployments/text-davinci-003-demo/completions?api-version=2022-12-01", azureOpenAIDeployment));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                client.DefaultRequestHeaders.Add("api-key", azureOpenAIKey);

                // OpenAI - Body
                var resultsAndInstructions = webSearchResultsString + promptInstructions;
                // OpenAI - Calculate the max tokens
                var resultsAndInstructionsLength = resultsAndInstructions.Length;
                var resultsAndInstructionsTokensEstimate = resultsAndInstructionsLength / 4;
                int maxTokens = Convert.ToInt32(resultsAndInstructionsTokensEstimate * 1.6) > 2000 ? 2000 : Convert.ToInt32(resultsAndInstructionsTokensEstimate * 1.6);

                // OpenAI - Completions Settings
                var openAICompletions = new OpenAICompletions()
                {
                    prompt = resultsAndInstructions,
                    max_tokens = maxTokens,
                    temperature = 0.24f,
                    top_p = 0.86f,
                    frequency_penalty = 0.14f,
                    presence_penalty = 0.14f,
                    stop = string.Empty
                };
                var openAICompletionsJsonString = JsonSerializer.Serialize(openAICompletions);

                // OpenAI - Post Request
                var openAIRequestBody = new StringContent(openAICompletionsJsonString, Encoding.UTF8, "application/json");
                var opeanAIResponse = await client.PostAsync(client.BaseAddress, openAIRequestBody);
                var openAICompletionResponseBody = await opeanAIResponse.Content.ReadAsStringAsync();
                var openAICompletionResponse = JsonSerializer.Deserialize<OpenAICompletionsResponse>(openAICompletionResponseBody);

                var openAICompletionResponseGeneratedText = openAICompletionResponse?.choices[0].text.Trim();
                _logger.LogInformation("GenerateHallOfFameText - OpenAI Text: " + openAICompletionResponseGeneratedText);

                // Successful response (OK)
                response.StatusCode = HttpStatusCode.OK;
                response.WriteString(openAICompletionResponseGeneratedText + "\r\n\r\n" + footNotes);
            }

            _logger.LogInformation("GenerateHallOfFameText - End");

            return response;
        }
    }
}
