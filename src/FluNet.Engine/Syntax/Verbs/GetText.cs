namespace FluNET.Syntax.Verbs
{
    public class GetText : Get<string[], FileInfo>
    {
        public GetText(string[] what, FileInfo from) : base(what, from)
        {
        }

        public override Func<FileInfo, string[]> Act
        {
            get
            {
                return (info) =>
                {
                    using (var reader = new StreamReader(info.OpenRead()))
                    {
                        return reader.ReadToEnd().Split('\n');
                    }
                };
            }
        }
    }
}
