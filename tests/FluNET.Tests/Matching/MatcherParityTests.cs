using FluNET.Matching;
using FluNET.Matching.Regex;
using FluNET.Matching.StringBased;
using System.Diagnostics;

namespace FluNET.Tests
{
    [TestFixture]
    public class MatcherParityTests
    {
        [Test]
        public void VariableMatcher_RegexAndString_ProduceSameResults()
        {
            var regexMatcher = new RegexVariableMatcher();
            var stringMatcher = new StringVariableMatcher();
            
            var testCases = new[]
            {
                "[variable]",
                "[my_var]",
                "[output]",
                "[result123]",
                "not a variable",
                "[]",
                "[",
                "]"
            };

            foreach (var testCase in testCases)
            {
                var regexIsMatch = regexMatcher.IsMatch(testCase);
                var stringIsMatch = stringMatcher.IsMatch(testCase);
                
                Assert.That(regexIsMatch, Is.EqualTo(stringIsMatch), 
                    $"Mismatch for '{testCase}': Regex={regexIsMatch}, String={stringIsMatch}");
                
                if (regexIsMatch && stringIsMatch)
                {
                    var regexExtract = regexMatcher.Extract(testCase);
                    var stringExtract = stringMatcher.Extract(testCase);
                    
                    Assert.That(stringExtract, Is.EqualTo(regexExtract),
                        $"Extract values differ for '{testCase}': Regex='{regexExtract}', String='{stringExtract}'");
                }
            }
        }

        [Test]
        public void ReferenceMatcher_RegexAndString_ProduceSameResults()
        {
            var regexMatcher = new RegexReferenceMatcher();
            var stringMatcher = new StringReferenceMatcher();
            
            var testCases = new[]
            {
                "{reference}",
                "{my_ref}",
                "{data}",
                "{value123}",
                "not a reference",
                "{}",
                "{",
                "}"
            };

            foreach (var testCase in testCases)
            {
                var regexIsMatch = regexMatcher.IsMatch(testCase);
                var stringIsMatch = stringMatcher.IsMatch(testCase);
                
                Assert.That(regexIsMatch, Is.EqualTo(stringIsMatch), 
                    $"Mismatch for '{testCase}': Regex={regexIsMatch}, String={stringIsMatch}");
                
                if (regexIsMatch && stringIsMatch)
                {
                    var regexExtract = regexMatcher.Extract(testCase);
                    var stringExtract = stringMatcher.Extract(testCase);
                    
                    Assert.That(stringExtract, Is.EqualTo(regexExtract),
                        $"Extract values differ for '{testCase}': Regex='{regexExtract}', String='{stringExtract}'");
                }
            }
        }

        [Test]
        public void DestructuringMatcher_RegexAndString_ProduceSameResults()
        {
            var regexMatcher = new RegexDestructuringMatcher();
            var stringMatcher = new StringDestructuringMatcher();
            
            var testCases = new[]
            {
                "{prop1,prop2}",
                "{name,age,city}",
                "{x,y,z}",
                "{single}",
                "not destructuring",
                "{,}",
                "{a,,b}"
            };

            foreach (var testCase in testCases)
            {
                var regexIsMatch = regexMatcher.IsMatch(testCase);
                var stringIsMatch = stringMatcher.IsMatch(testCase);
                
                Assert.That(regexIsMatch, Is.EqualTo(stringIsMatch), 
                    $"Mismatch for '{testCase}': Regex={regexIsMatch}, String={stringIsMatch}");
                
                if (regexIsMatch && stringIsMatch)
                {
                    var regexProps = regexMatcher.GetPropertyNames(testCase);
                    var stringProps = stringMatcher.GetPropertyNames(testCase);
                    
                    Assert.That(stringProps.Length, Is.EqualTo(regexProps.Length),
                        $"Property count differs for '{testCase}': Regex={regexProps.Length}, String={stringProps.Length}");
                    
                    for (int i = 0; i < regexProps.Length; i++)
                    {
                        Assert.That(stringProps[i], Is.EqualTo(regexProps[i]),
                            $"Property {i} differs for '{testCase}': Regex='{regexProps[i]}', String='{stringProps[i]}'");
                    }
                }
            }
        }

        [Test]
        [Explicit("Performance benchmark - run manually")]
        public void VariableMatcher_StringIsFasterThanRegex()
        {
            var regexMatcher = new RegexVariableMatcher();
            var stringMatcher = new StringVariableMatcher();
            
            var testData = Enumerable.Range(0, 10000)
                .Select(i => $"[variable{i}]")
                .ToArray();

            // Warm up
            foreach (var data in testData.Take(100))
            {
                regexMatcher.IsMatch(data);
                stringMatcher.IsMatch(data);
            }

            // Regex benchmark
            var regexStopwatch = Stopwatch.StartNew();
            foreach (var data in testData)
            {
                regexMatcher.IsMatch(data);
                if (regexMatcher.IsMatch(data))
                    regexMatcher.Extract(data);
            }
            regexStopwatch.Stop();

            // String benchmark
            var stringStopwatch = Stopwatch.StartNew();
            foreach (var data in testData)
            {
                stringMatcher.IsMatch(data);
                if (stringMatcher.IsMatch(data))
                    stringMatcher.Extract(data);
            }
            stringStopwatch.Stop();

            Console.WriteLine($"Regex: {regexStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"String: {stringStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Speedup: {(double)regexStopwatch.ElapsedMilliseconds / stringStopwatch.ElapsedMilliseconds:F2}x");

            // String implementation should be faster
            Assert.That(stringStopwatch.ElapsedMilliseconds, Is.LessThan(regexStopwatch.ElapsedMilliseconds),
                "String-based matcher should be faster than regex-based");
        }

        [Test]
        [Explicit("Performance benchmark - run manually")]
        public void ReferenceMatcher_StringIsFasterThanRegex()
        {
            var regexMatcher = new RegexReferenceMatcher();
            var stringMatcher = new StringReferenceMatcher();
            
            var testData = Enumerable.Range(0, 10000)
                .Select(i => $"{{reference{i}}}")
                .ToArray();

            // Warm up
            foreach (var data in testData.Take(100))
            {
                regexMatcher.IsMatch(data);
                stringMatcher.IsMatch(data);
            }

            // Regex benchmark
            var regexStopwatch = Stopwatch.StartNew();
            foreach (var data in testData)
            {
                regexMatcher.IsMatch(data);
                if (regexMatcher.IsMatch(data))
                    regexMatcher.Extract(data);
            }
            regexStopwatch.Stop();

            // String benchmark
            var stringStopwatch = Stopwatch.StartNew();
            foreach (var data in testData)
            {
                stringMatcher.IsMatch(data);
                if (stringMatcher.IsMatch(data))
                    stringMatcher.Extract(data);
            }
            stringStopwatch.Stop();

            Console.WriteLine($"Regex: {regexStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"String: {stringStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Speedup: {(double)regexStopwatch.ElapsedMilliseconds / stringStopwatch.ElapsedMilliseconds:F2}x");

            Assert.That(stringStopwatch.ElapsedMilliseconds, Is.LessThan(regexStopwatch.ElapsedMilliseconds),
                "String-based matcher should be faster than regex-based");
        }

        [Test]
        [Explicit("Performance benchmark - run manually")]
        public void DestructuringMatcher_PerformanceComparison()
        {
            var regexMatcher = new RegexDestructuringMatcher();
            var stringMatcher = new StringDestructuringMatcher();
            
            var testData = Enumerable.Range(0, 10000)
                .Select(i => $"{{prop{i},value{i},data{i}}}")
                .ToArray();

            // Warm up
            foreach (var data in testData.Take(100))
            {
                regexMatcher.IsMatch(data);
                stringMatcher.IsMatch(data);
            }

            // Regex benchmark
            var regexStopwatch = Stopwatch.StartNew();
            foreach (var data in testData)
            {
                if (regexMatcher.IsMatch(data))
                    regexMatcher.GetPropertyNames(data);
            }
            regexStopwatch.Stop();

            // String benchmark
            var stringStopwatch = Stopwatch.StartNew();
            foreach (var data in testData)
            {
                if (stringMatcher.IsMatch(data))
                    stringMatcher.GetPropertyNames(data);
            }
            stringStopwatch.Stop();

            Console.WriteLine($"Regex: {regexStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"String: {stringStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Speedup: {(double)regexStopwatch.ElapsedMilliseconds / stringStopwatch.ElapsedMilliseconds:F2}x");
        }

        [Test]
        public void MatcherResolver_DefaultsToStringMatchers()
        {
            var matchers = new List<IMatcher>
            {
                new StringVariableMatcher(),
                new RegexVariableMatcher(),
                new StringReferenceMatcher(),
                new RegexReferenceMatcher(),
                new StringDestructuringMatcher(),
                new RegexDestructuringMatcher()
            };
            
            var resolver = new MatcherResolver(matchers, useRegex: false);
            
            var variableMatcher = resolver.GetMatcher<IVariableMatcher>();
            var referenceMatcher = resolver.GetMatcher<IReferenceMatcher>();
            var destructuringMatcher = resolver.GetMatcher<IDestructuringMatcher>();

            Assert.That(variableMatcher, Is.TypeOf<StringVariableMatcher>());
            Assert.That(referenceMatcher, Is.TypeOf<StringReferenceMatcher>());
            Assert.That(destructuringMatcher, Is.TypeOf<StringDestructuringMatcher>());
        }

        [Test]
        public void MatcherResolver_CanUseRegexMatchers()
        {
            var matchers = new List<IMatcher>
            {
                new StringVariableMatcher(),
                new RegexVariableMatcher(),
                new StringReferenceMatcher(),
                new RegexReferenceMatcher(),
                new StringDestructuringMatcher(),
                new RegexDestructuringMatcher()
            };
            
            var resolver = new MatcherResolver(matchers, useRegex: true);
            
            var variableMatcher = resolver.GetMatcher<IVariableMatcher>();
            var referenceMatcher = resolver.GetMatcher<IReferenceMatcher>();
            var destructuringMatcher = resolver.GetMatcher<IDestructuringMatcher>();

            Assert.That(variableMatcher, Is.TypeOf<RegexVariableMatcher>());
            Assert.That(referenceMatcher, Is.TypeOf<RegexReferenceMatcher>());
            Assert.That(destructuringMatcher, Is.TypeOf<RegexDestructuringMatcher>());
        }
    }
}
