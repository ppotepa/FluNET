using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;
using FluNET.Words;

namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for setting variables.
    /// Usage: SET [variableName] TO value.
    /// </summary>
    public class SetVariable : IVerb, IWord, IKeyword, IWhat<string>, ITo<string>
    {
        public SetVariable() : this(string.Empty, string.Empty)
        {
        }

        public SetVariable(string what, string to)
        {
            What = what;
            To = to;
        }

        public string Text => "SET";

        public string[] Synonyms => Array.Empty<string>();

        public string What { get; set; }
        public string To { get; set; }

        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // SET must be followed by [variableName]
            if (nextWord is VariableWord)
            {
                return ValidationResult.Success();
            }

            if (nextWord is LiteralWord literal)
            {
                string value = literal.Value.TrimEnd('.').ToUpperInvariant();
                if (value == "." || string.IsNullOrWhiteSpace(value))
                {
                    return ValidationResult.Failure("SET requires a [variableName] after it.");
                }
            }

            return ValidationResult.Failure("SET must be followed by [variableName].");
        }

        public bool Validate(IWord word)
        {
            return true;
        }

        public void Execute(Engine engine, string variableName, string value)
        {
            try
            {
                engine.RegisterVariable(variableName, value);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Variable [{variableName}] registered successfully.");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error registering variable: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }
    }
}