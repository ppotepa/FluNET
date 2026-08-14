using FluNET.Context;
using FluNET.Language.Resources;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class BinaryImageValueTests
{
    [Test]
    public void SurfaceRuntimeDeclaresBinaryAndImageAsDistinctTypes()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var types = context.GetService<FluNET.Language.LanguageSnapshot>().Types;
        Assert.Multiple(() => { Assert.That(types.Get<BinaryValue>().Name, Is.EqualTo("Binary")); Assert.That(types.Get<ImageValue>().Name, Is.EqualTo("Image")); Assert.That(types.Get<BinaryValue>(), Is.Not.EqualTo(types.File)); });
    }

    [Test]
    public void PngDecoderReadsDimensionsWithoutGraphicsDependency()
    {
        byte[] png = new byte[24]; png[0]=0x89; png[1]=(byte)'P'; png[2]=(byte)'N'; png[3]=(byte)'G'; png[19]=2; png[23]=3;
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var types = context.GetService<FluNET.Language.LanguageSnapshot>().Types;
        ResourceDescriptor descriptor = new(new FileResourceReference("x.png", true), ResourceFormat.Image, types.Get<ImageValue>(), "x");
        ImageValue image = (ImageValue)new ImageResourceDecoder().Decode(descriptor, new ResourcePayload(png, "image/png"));
        Assert.Multiple(() => { Assert.That(image.Width, Is.EqualTo(2)); Assert.That(image.Height, Is.EqualTo(3)); Assert.That(image.ToString(), Does.Not.Contain("System.Byte")); });
    }
}
