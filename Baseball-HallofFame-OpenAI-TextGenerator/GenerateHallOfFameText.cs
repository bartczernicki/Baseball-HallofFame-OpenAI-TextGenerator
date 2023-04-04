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
                var isRedisCacheWorking = false;
                var isBingSearchWorking = false;
                var isOpenAIKeyPresent = false;

                // 1) - Check Redis
                // Cache - Initialize Cache (Redis) & Check Cache
                var redisCache = new Cache.Redis();
                if (Util.UseRedisCache(redisCache.IsRedisConnected))
                {
                    isRedisCacheWorking= true;
                }

                // 2) - Check Bing
                var bingSearchKey = System.Environment.GetEnvironmentVariable("BING_SEARCH_KEY");
                var bingSearchClient = new WebSearchClient(new ApiKeyServiceClientCredentials(bingSearchKey));
                // check if Bing client is working
                try
                {
                    var bingSearchResult = await bingSearchClient.Web.SearchAsync(query: "test");
                    isBingSearchWorking= true;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("GenerateHallOfFameText - Bing Search Error: " + ex.Message);
                }

                // 3) - Check OpenAI (just check if key is present)
                var openAIKey = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
                if (!string.IsNullOrEmpty(openAIKey))
                {
                    isOpenAIKeyPresent = true;
                }

                response.WriteString(string.Format("Generate Hall of Fame API. Function Connection: True, Redis Connection: {0}, Bing Connection: {1}, Valid Azure OpenAI Key: {2}", isRedisCacheWorking, isBingSearchWorking, isOpenAIKeyPresent));
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

                var mlbBatterInfo = JsonSerializer.Deserialize<MLBBatterInfo>(requestBodyString) ?? new MLBBatterInfo();
                _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo Deserialized");

                // Cache - Initialize Cache (Redis) & Check Cache
                var redisCache = new Cache.Redis();

                var narratives = new List<NarrativeResult>();
                // Cache - Determine if should pull from cache
                if (Util.UseRedisCache(redisCache.IsRedisConnected))
                {
                    _logger.LogInformation("GenerateHallOfFameText - Random selection attempting Cache");
                    narratives = redisCache.GetNarratives(mlbBatterInfo);
                }

                // Cache - Return the Narrative from Cache
                if (narratives.Count > 0)
                {
                    var random = new Random(DateTime.Now.Second).Next(0, narratives.Count - 1);
                    var selectedNarrative = narratives[random].Text;
                    _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo - Narrative FOUND in Cache");

                    // Successful response (OK)
                    response.StatusCode = HttpStatusCode.OK;
                    response.WriteString(selectedNarrative);
                }
                else
                {   // ELSE PROCESS either BING and/or OPENAI

                    // Bing - Web Search Components
                    var searchString = string.Format("{0} baseball Hall of Fame", mlbBatterInfo?.FullPlayerName);
                    var webSearchResultsString = "Web search results:\r\n\r\n";
                    var footNotes = string.Empty;
                    var bingSearchId = 0;

                    var webSearchResults = redisCache.GetWebSearchResults(mLBBatterInfo: mlbBatterInfo, searchString);

                    if (webSearchResults.Count > 0)
                    {
                        _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo - WebSearchResults FOUND in Cache");

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
                        _logger.LogInformation("GenerateHallOfFameText - MLBBatterInfo - WebSearchResults NOT FOUND in Cache");

                        var bingSearchKey = System.Environment.GetEnvironmentVariable("BING_SEARCH_KEY");
                        var bingSearchClient = new WebSearchClient(new ApiKeyServiceClientCredentials(bingSearchKey));
                        var bingWebData = await bingSearchClient.Web.SearchAsync(query: searchString, count: 8);


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

                            // Add to Cache - WebSearchResults
                            redisCache.AddWebSearchResults(mlbBatterInfo, searchString, webSearchResults);
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
                    int maxTokens = Convert.ToInt32(resultsAndInstructionsTokensEstimate * 1.8) > 2000 ? 2000 : Convert.ToInt32(resultsAndInstructionsTokensEstimate * 1.6);

                    // OpenAI - Completions Settings
                    var openAICompletions = new OpenAICompletions()
                    {
                        prompt = resultsAndInstructions,
                        max_tokens = maxTokens,
                        temperature = 0.26f,
                        top_p = 0.84f,
                        frequency_penalty = 0.12f,
                        presence_penalty = 0.12f,
                        stop = string.Empty
                    };
                    var openAICompletionsJsonString = JsonSerializer.Serialize(openAICompletions);

                    // OpenAI - Post Request
                    var openAIRequestBody = new StringContent(openAICompletionsJsonString, Encoding.UTF8, "application/json");
                    var opeanAIResponse = await client.PostAsync(client.BaseAddress, openAIRequestBody);
                    var openAICompletionResponseBody = await opeanAIResponse.Content.ReadAsStringAsync();
                    var openAICompletionResponse = JsonSerializer.Deserialize<OpenAICompletionsResponse>(openAICompletionResponseBody);

                    var openAICompletionResponseGeneratedText = openAICompletionResponse?.choices[0].text.Trim();
                    var fullNarrativeResponse = openAICompletionResponseGeneratedText + "\r\n\r\n" + footNotes;
                    _logger.LogInformation("GenerateHallOfFameText - OpenAI Text: " + openAICompletionResponseGeneratedText);

                    // Cache - Add Response To Cache
                    redisCache.AddNarrative(mlbBatterInfo, fullNarrativeResponse ?? string.Empty);

                    // Successful response (OK)
                    response.StatusCode = HttpStatusCode.OK;
                    response.WriteString(fullNarrativeResponse);
                }   // ENDOF - ELSE PROCESS either BING and/or OPENAI
            }

            _logger.LogInformation("GenerateHallOfFameText - End");

            return response;
        }
    }
}
