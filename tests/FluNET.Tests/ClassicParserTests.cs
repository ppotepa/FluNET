using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Parsing;

namespace FluNET.Tests;

public class ClassicParserTests
{
    [Fact]
    public void Parser_preserves_classic_sentence_shape_and_then_pipeline()
    {
        var parser = new ClassicParser(new LanguageRegistry().Snapshot);

        ParseResult result = parser.Parse("GET TEXT [data] FROM {input.txt} THEN SAY [data]");

        Assert.True(result.Success);
        PipelineNode pipeline = Assert.Single(result.Script!.Pipelines);
        Assert.Equal(2, pipeline.Sentences.Count);
        Assert.Equal("GET", pipeline.Sentences[0].Verb);
        Assert.Equal("TEXT", pipeline.Sentences[0].Qualifier);
        Assert.Contains(pipeline.Sentences[0].Clauses, x => x.Kind == ClauseKind.What && x.Value is VariableExpression);
        Assert.Contains(pipeline.Sentences[0].Clauses, x => x.Kind == ClauseKind.From && x.Value is ReferenceExpression);
    }

    [Fact]
    public void Parser_keeps_multiple_values_for_the_same_role()
    {
        var parser = new ClassicParser(new LanguageRegistry().Snapshot);
        ParseResult result = parser.Parse("GET [data] FROM a.txt b.txt c.txt");
        SentenceNode sentence = Assert.Single(Assert.Single(result.Script!.Pipelines).Sentences);
        Assert.Equal(3, sentence.Clauses.Count(x => x.Kind == ClauseKind.From));
    }
}
