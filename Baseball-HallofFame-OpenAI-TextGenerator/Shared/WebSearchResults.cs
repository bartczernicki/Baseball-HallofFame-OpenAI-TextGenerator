using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baseball_HallofFame_OpenAI_TextGenerator.Shared
{
    public class WebSearchResult
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
