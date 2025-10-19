using FluNET.Syntax;

namespace FluNET.Keywords
{
    public class From : IKeyword, IWord, IValidatable
    {
        public string Text => "FROM";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // FROM can be followed by variables or literals
            return ValidationResult.Success();
        }
    }

    public class To : IKeyword, IWord, IValidatable
    {
        public string Text => "TO";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // TO can be followed by variables or literals
            return ValidationResult.Success();
        }
    }

    public class Then : IKeyword, IWord, IValidatable
    {
        public string Text => "THEN";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // THEN must be followed by a verb
            return nextWord is IVerb
                ? ValidationResult.Success()
                : ValidationResult.Failure("THEN must be followed by a verb");
        }
    }

    public class And : IKeyword, IWord, IValidatable
    {
        public string Text => "AND";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // AND can connect multiple clauses
            return ValidationResult.Success();
        }
    }
}
