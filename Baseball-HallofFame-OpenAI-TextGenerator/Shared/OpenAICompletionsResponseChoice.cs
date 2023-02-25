namespace Baseball_HallofFame_OpenAI_TextGenerator.Shared
{
    public class OpenAICompletionsResponseChoice
    {
        public string text { get; set; } = string.Empty;
        public int index { get; set; }
        public object logprobs { get; set; } = null;
        public string finish_reason { get; set; } = string.Empty;
    }
}