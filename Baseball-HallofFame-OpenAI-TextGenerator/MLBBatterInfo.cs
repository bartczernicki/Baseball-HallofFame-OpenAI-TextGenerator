using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baseball_HallofFame_OpenAI_TextGenerator
{
    public class MLBBatterInfo
    {
        public string FullPlayerName { get; set; } = string.Empty;

        public float YearsPlayed { get; set; }

        public float HR { get; set; }

        public float TotalPlayerAwards { get; set; }

        public float HallOfFameProbability { get; set; }

        public bool ReturnResponseAsMarkdown { get; set; } = true;

        public override string ToString()
        {
            return (FullPlayerName + "-" + YearsPlayed + "-" + HR + "-" + TotalPlayerAwards.ToString() + "-" + HallOfFameProbability.ToString()).ToString();
        }
    }
}
