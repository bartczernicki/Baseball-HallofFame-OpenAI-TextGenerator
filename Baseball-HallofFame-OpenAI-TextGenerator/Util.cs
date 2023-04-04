using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baseball_HallofFame_OpenAI_TextGenerator
{
    public static class Util
    {
        public static async Task<string> GetRawBodyAsync(this HttpRequestData request, Encoding encoding)
        {
            request.Body.Position = 0;

            var reader = new StreamReader(request.Body, encoding ?? Encoding.UTF8);

            var body = await reader.ReadToEndAsync().ConfigureAwait(false);

            request.Body.Position = 0;

            return body;
        }

        public static int GetSequenceHashCode<T>(this IList<T> sequence)
        {
            const int seed = 100;
            const int modifier = 600;

            unchecked
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                return sequence.Aggregate(seed, (current, item) =>
                    (current * modifier) + item.GetHashCode());
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
        }

        public static int GetDeterministicHashCode(this string str)
        {
            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        // String literal test
        public static string OpenAIPromptExample = """
        {
        "prompt": "Once upon a time",
        "max_tokens": 20,
        "temperature": 0.4
        }
        """;

        public static bool UseRedisCache(bool isConnected)
        {
            var percentageToUseRedis = 75;
            var randomSeed = DateTime.Now.Millisecond;
            var random = new Random(randomSeed).Next(0, 100);

            if ((random <= percentageToUseRedis) && (isConnected))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
