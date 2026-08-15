namespace FluNET.Capabilities;

internal static class DefaultCapabilities
{
    private static readonly IExecutionPolicy Policy = new AllowAllExecutionPolicy();
    public static IFluNetFileSystem FileSystem { get; } = new PhysicalFluNetFileSystem(Policy);
    public static IHttpTransport Http { get; } = new HttpTransport(new HttpClient(), Policy);
    public static ITextOutput Output { get; } = new ConsoleTextOutput();
}
