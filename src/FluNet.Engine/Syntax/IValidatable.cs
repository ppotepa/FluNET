namespace FluNET.Syntax
{
    public interface IValidatable
    {
        ValidationResult ValidateNext(string nextTokenValue, DiscoveryService discoveryService);
    }
}
