using FluNET.Matching;
using FluNET.Matching.Regex;
using FluNET.Matching.StringBased;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Extensions
{
    /// <summary>
    /// Extension methods for registering pattern matching services in DI container.
    /// </summary>
    public static class PatternMatcherServiceExtensions
    {
        /// <summary>
        /// Registers all pattern matcher services including regex and string-based implementations.
        /// The MatcherResolver will select the appropriate implementation based on configuration.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddPatternMatchers(this IServiceCollection services)
        {
            // Register regex-based matchers
            services.AddTransient<IMatcher, RegexVariableMatcher>();
            services.AddTransient<IMatcher, RegexReferenceMatcher>();
            services.AddTransient<IMatcher, RegexDestructuringMatcher>();

            // Register string-based matchers (performance optimized)
            services.AddTransient<IMatcher, StringVariableMatcher>();
            services.AddTransient<IMatcher, StringReferenceMatcher>();
            services.AddTransient<IMatcher, StringDestructuringMatcher>();

            // Register the resolver service
            services.AddTransient<MatcherResolver>();

            return services;
        }
    }
}
