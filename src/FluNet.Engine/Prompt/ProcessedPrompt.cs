namespace FluNET.Prompt
{
    public class ProcessedPrompt
    {
        private readonly string prompt;

        public ProcessedPrompt(string prompt)
        {
            this.prompt = prompt;            
        }
        public override string ToString()
        {
            return prompt.Replace("  ", " ").Trim();
        }   
    }
}