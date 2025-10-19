namespace FluNET.Keywords
{
    /// <summary>
    /// GET verb keyword
    /// </summary>
    public class Get : IKeyword, IWord, IValidatable
    {
        public string Text => "GET";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }

    /// <summary>
    /// SAVE verb keyword
    /// </summary>
    public class Save : IKeyword, IWord, IValidatable
    {
        public string Text => "SAVE";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }

    /// <summary>
    /// POST verb keyword
    /// </summary>
    public class Post : IKeyword, IWord, IValidatable
    {
        public string Text => "POST";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }

    /// <summary>
    /// DELETE verb keyword
    /// </summary>
    public class Delete : IKeyword, IWord, IValidatable
    {
        public string Text => "DELETE";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }

    /// <summary>
    /// LOAD verb keyword
    /// </summary>
    public class Load : IKeyword, IWord, IValidatable
    {
        public string Text => "LOAD";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }

    /// <summary>
    /// SEND verb keyword
    /// </summary>
    public class Send : IKeyword, IWord, IValidatable
    {
        public string Text => "SEND";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }

    /// <summary>
    /// TRANSFORM verb keyword
    /// </summary>
    public class Transform : IKeyword, IWord, IValidatable
    {
        public string Text => "TRANSFORM";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            return true;
        }
    }
}
