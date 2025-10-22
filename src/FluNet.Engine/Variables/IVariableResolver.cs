namespace FluNET.Variables
{
    /// <summary>
    /// Interface for resolving and registering variables.
    /// Implementations can be scoped differently for CLI (persistent) vs single-command (transient) scenarios.
    /// </summary>
    public interface IVariableResolver
    {
        /// <summary>
        /// Resolves a variable by name and returns its value.
        /// </summary>
        /// <typeparam name="T">Expected type of the variable</typeparam>
        /// <param name="tokenValue">Variable reference (e.g., [Name] or [{prop1, prop2}])</param>
        /// <returns>Variable value or null/default if not found</returns>
        T? Resolve<T>(string tokenValue);

        /// <summary>
        /// Registers a variable with a value.
        /// </summary>
        /// <typeparam name="T">The type of the variable value</typeparam>
        /// <param name="name">Variable name (without brackets)</param>
        /// <param name="value">Variable value</param>
        void Register<T>(string name, T value);

        /// <summary>
        /// Checks if a variable is registered.
        /// </summary>
        /// <param name="name">Variable name (without brackets)</param>
        /// <returns>True if variable exists</returns>
        bool IsRegistered(string name);

        /// <summary>
        /// Clears all variables.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets all registered variable names.
        /// </summary>
        IEnumerable<string> GetVariableNames();
    }
}
