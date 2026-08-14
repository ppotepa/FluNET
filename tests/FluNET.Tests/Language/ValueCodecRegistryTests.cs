using FluNET.Language;
using FluNET.Language.Values;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class ValueCodecRegistryTests
{
    [Test]
    public void BuiltInRegistryParsesInvariantNumbersAndUsesExplicitTextConversionEdge()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        IValueCodecRegistry registry = ValueCodecRegistryFactory.CreateDefault(language);

        decimal number = registry.Parse<decimal>(new ValueLiteral("42.5"));
        ConversionResolution conversion = registry.ResolveConversion(
            language.Types.Number,
            language.Types.Text);

        Assert.Multiple(() =>
        {
            Assert.That(number, Is.EqualTo(42.5m));
            Assert.That(conversion.IsFound, Is.True);
            Assert.That(conversion.Path!.IsIdentity, Is.False);
            Assert.That(conversion.Path.Steps, Has.Count.EqualTo(1));
            Assert.That(registry.Convert(number, conversion.Path), Is.EqualTo("42.5"));
        });
    }

    [Test]
    public void ModuleCanRegisterDomainCodec()
    {
        FluNetModuleBuilder builder = new();
        builder.Language.Type<Slug>("Slug");
        builder.Codec<Slug, SlugCodec>();
        FluNetRuntimeDefinition runtime = builder.Build();

        ServiceCollection services = new();
        runtime.RegisterRoutes(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        IValueCodecRegistry registry = provider.GetRequiredService<IValueCodecRegistry>();

        Slug value = registry.Parse<Slug>(new ValueLiteral("Hello World"));

        Assert.Multiple(() =>
        {
            Assert.That(value.Value, Is.EqualTo("hello-world"));
            Assert.That(registry.Format(runtime.Language.Types.Get<Slug>().Id, value),
                Is.EqualTo("hello-world"));
        });
    }

    public sealed record Slug(string Value);

    public sealed class SlugCodec : IValueCodec<Slug>
    {
        public SlugCodec()
        {
        }

        public Slug Parse(ValueLiteral literal, ValueParseContext context) =>
            new(literal.Text.Trim().ToLowerInvariant().Replace(' ', '-'));

        public string Format(Slug value, ValueFormatContext context) => value.Value;
    }
}
