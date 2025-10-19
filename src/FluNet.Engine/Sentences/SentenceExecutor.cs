using FluNET.Keywords;
using FluNET.Syntax;
using FluNET.Syntax.Verbs;
using FluNET.Tokens;
using FluNET.Variables;
using FluNET.Words;

namespace FluNET.Sentences
{
    /// <summary>
    /// Executes a sentence by invoking verb actions and chaining operations.
    /// Handles THEN keyword for sequential action execution on the same data.
    /// </summary>
    public class SentenceExecutor(VariableResolver variableResolver)
    {
        /// <summary>
        /// Execute a sentence and return the final result.
        /// The result is the output of the last verb in the chain.
        /// </summary>
        /// <param name="sentence">The sentence to execute</param>
        /// <returns>The result of executing the sentence chain</returns>
        public object? Execute(ISentence sentence)
        {
            if (sentence?.Root == null)
                return null;

            object? result = null;
            IWord? currentWord = sentence.Root;

            // Try to interpret and execute the sentence pattern
            result = TryExecuteVerbPattern(currentWord);

            return result;
        }

        private object? TryExecuteVerbPattern(IWord? startWord)
        {
            if (startWord == null) return null;

            // Get the verb keyword
            var verbKeyword = GetKeywordText(startWord);
            if (string.IsNullOrEmpty(verbKeyword))
                return null;

            // Pattern: VERB [what] PREPOSITION [value]
            // Example: GET [text] FROM file.txt
            // Example: SAVE [data] TO output.txt

            var what = startWord.Next;
            if (what == null) return null;

            var preposition = what.Next;
            if (preposition == null) return null;

            var value = preposition.Next;
            if (value == null) return null;

            var prepositionText = GetKeywordText(preposition);

            // Resolve variables
            var whatValue = ResolveValue(what);
            var valueValue = ResolveValue(value);

            // Execute based on verb pattern
            return verbKeyword.ToUpperInvariant() switch
            {
                "GET" when prepositionText == "FROM" => ExecuteGet(whatValue, valueValue),
                "SAVE" when prepositionText == "TO" => ExecuteSave(whatValue, valueValue),
                "POST" when prepositionText == "TO" => ExecutePost(whatValue, valueValue),
                "DELETE" when prepositionText == "FROM" => ExecuteDelete(whatValue, valueValue),
                "LOAD" when prepositionText == "FROM" => ExecuteLoad(whatValue, valueValue),
                "SEND" when prepositionText == "TO" => ExecuteSend(whatValue, valueValue),
                _ => null
            };
        }

        private string GetKeywordText(IWord word)
        {
            // Get text from IKeyword implementations
            if (word is IKeyword keyword)
                return keyword.Text;
            return "";
        }

        private object? ResolveValue(IWord word)
        {
            // If it's a variable, resolve it
            if (word is VariableWord varWord)
            {
                return variableResolver.Resolve<object>(varWord.VariableReference);
            }

            // If it's a literal, get its value
            if (word is LiteralWord litWord)
            {
                return litWord.Value?.TrimEnd('.');
            }

            // Try to get Text property for keyword values
            if (word is IKeyword kw)
                return kw.Text;

            return null;
        }

        private object? ExecuteGet(object? what, object? from)
        {
            if (from is string filePath)
            {
                try
                {
                    // Create GetText instance and execute
                    var fileInfo = new FileInfo(filePath);
                    var verb = new GetText(Array.Empty<string>(), fileInfo);
                    return verb.Execute();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading file: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        private object? ExecuteSave(object? what, object? to)
        {
            if (what is string text && to is string filePath)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var verb = new SaveText(text, fileInfo);
                    return verb.Execute();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving file: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        private object? ExecutePost(object? what, object? to)
        {
            // Placeholder for POST implementation
            Console.WriteLine($"POST not yet fully implemented: {what} -> {to}");
            return null;
        }

        private object? ExecuteDelete(object? what, object? from)
        {
            // Placeholder for DELETE implementation
            Console.WriteLine($"DELETE not yet fully implemented: {what} from {from}");
            return null;
        }

        private object? ExecuteLoad(object? what, object? from)
        {
            // Placeholder for LOAD implementation
            Console.WriteLine($"LOAD not yet fully implemented: {what} from {from}");
            return null;
        }

        private object? ExecuteSend(object? what, object? to)
        {
            // Placeholder for SEND implementation
            Console.WriteLine($"SEND not yet fully implemented: {what} to {to}");
            return null;
        }
    }
}
