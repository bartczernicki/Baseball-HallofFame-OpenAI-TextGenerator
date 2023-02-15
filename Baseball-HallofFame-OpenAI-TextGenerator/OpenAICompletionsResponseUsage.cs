namespace Baseball_HallofFame_OpenAI_TextGenerator
{
    public class OpenAICompletionsResponseUsage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
    }
}