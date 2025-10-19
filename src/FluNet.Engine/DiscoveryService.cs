using FluNET.Syntax;

namespace FluNET
{
    public class DiscoveryService
    {
        public DiscoveryService()
        {
            this.Words = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => x is IWord);
        }

        public IEnumerable<Type> Words { get; }
    }
}