using FluNET.Context;
using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class CsvSurfaceTests
{
    [Test]
    public void SaveCsvLowersToTheCsvEncoderCommand()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var result = context.CompileSurface(
            "GET https://example.test/rows AS rows\nSAVE CSV [rows] TO \"./rows.csv\"");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(result.Lowering.CanonicalSyntax.Commands.Select(command => command.AllTokens.First().Text),
            Is.EqualTo(new[] { "GETHTTP", "SAVECSV" }));
    }

    [Test]
    public async Task SaveCsvWritesQuotedValuesFromHttpRows()
    {
        string path = Path.Combine(Path.GetTempPath(), "flunet-save-csv-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
                services => services.AddSingleton<IHttpTransport>(new JsonHttp(
                    "[{\"name\":\"Ada, Lovelace\",\"age\":36},{\"name\":\"Grace\",\"note\":\"line one\\nline two\"}]")));
            var execution = await context.ExecuteSurfaceAsync(
                $"GET https://example.test/rows AS rows\nSAVE CSV [rows] TO \"{path}\"");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(
                "name,age,note\r\n\"Ada, Lovelace\",36,\r\nGrace,,\"line one\nline two\"\r\n"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class JsonHttp(string json) : IHttpTransport
    {
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes(json));

        public Task<string> PostJsonAsync(Uri uri, string body, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
