using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baseball_HallofFame_OpenAI_TextGenerator
{
    public class OpenAICompletions
    {
        //public string id { get; set; }
        //public string object1 { get; set; }
        //public string created { get; set; }
        //public string model { get; set; }
        //public string choices { get; set; }
        public string prompt { get; set; } = string.Empty;
        public int max_tokens { get; set; }
        public float temperature { get; set; }
        public float top_p { get; set; }
        public float frequency_penalty { get; set; }
        public float presence_penalty { get; set; }
        public string stop { get; set; } = string.Empty;
    }

    public class OpenAICompletionsResponse
    {
        public string id { get; set; } = string.Empty;
        public string object1 { get; set; } = string.Empty;
        public int created { get; set; }
        public string model { get; set; } = string.Empty;
        public List<OpenAICompletionsResponseChoice> choices { get; set; } = new List<OpenAICompletionsResponseChoice>();
    }
}

