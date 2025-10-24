using FluNET.Prompt;
using FluNET.Context;
using FluNET.Syntax.Verbs;
using FluNET.Words;
using FluNET.Syntax.Validation;
using FluNET.Sentences;

namespace FluNET.Tests
{
    /// <summary>
    /// Tests for THEN clause sentence chaining functionality.
    /// THEN allows multiple commands to be executed in sequence with shared variable context.
    /// Example: DOWNLOAD [file] FROM url TO {file.txt} THEN SAY [file].
    /// </summary>
    [TestFixture]
    public class ThenClauseTests
    {
        private FluNetContext? _context;
        private Engine? engine;
        private string? testDirectory;

        [SetUp]
        public void Setup()
        {
            // Create test directory
            testDirectory = Path.Combine(Path.GetTempPath(), $"FluNET_ThenClause_{Guid.NewGuid()}");
            Directory.CreateDirectory(testDirectory);

            _context = FluNetContext.Create();
            engine = _context.GetEngine();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test directory
            if (testDirectory != null && Directory.Exists(testDirectory))
            {
                try
                {
                    Directory.Delete(testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            _context?.Dispose();
        }

        #region Basic THEN Clause Tests

        [Test]
        public void ThenClause_GetThenSay_ShouldExecuteBothCommands()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Hello World");

            // Act - Chain GET and SAY with THEN
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [content] FROM {testFile} THEN SAY [content]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Hello World"));
            });
        }

        [Test]
        public void ThenClause_SaveThenLoad_ShouldChainCommands()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "output.txt");

            // Act - Chain SAVE and LOAD with THEN
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"SAVE \"Test Content\" TO {testFile} THEN GET [result] FROM {testFile}."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(testFile), Is.True);
                Assert.That(result, Is.Not.Null);
                // Result from GET will be string[]
                Assert.That(result, Is.InstanceOf<string[]>());
                string[] lines = (string[])result!;
                Assert.That(lines, Has.Length.GreaterThan(0));
                Assert.That(string.Join("", lines), Does.Contain("Test Content"));
            });
        }

        [Test]
        public void ThenClause_MultipleChains_ShouldExecuteInSequence()
        {
            // Arrange
            string file1 = Path.Combine(testDirectory!, "file1.txt");
            string file2 = Path.Combine(testDirectory!, "file2.txt");
            File.WriteAllText(file1, "First");

            // Act - Chain three commands: GET THEN SAVE THEN SAY
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {file1} THEN SAVE [data] TO {file2} THEN SAY [data]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(File.Exists(file2), Is.True);
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public void ThenClause_VariableSharing_ShouldMaintainContext()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "shared.txt");
            File.WriteAllText(testFile, "Shared Data");

            // Act - First command stores in variable, second uses it
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [shared] FROM {testFile} THEN SAY [shared]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(result, Is.Not.Null);
                Assert.That(result as string, Does.Contain("Shared Data"));
            });
        }

        #endregion

        #region Error Handling

        [Test]
        public void ThenClause_FirstCommandFails_ShouldReturnError()
        {
            // Arrange - Non-existent file
            string nonExistentFile = Path.Combine(testDirectory!, "nonexistent.txt");

            // Act
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {nonExistentFile} THEN SAY [data]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("Variable [data] not found"));
            });
        }

        [Test]
        public void ThenClause_SecondCommandFails_ShouldReturnError()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Data");

            // Act - Second command references non-existent variable
            (ValidationResult validation, _, object? result) =
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {testFile} THEN SAY [nonexistent]."));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.False);
                Assert.That(validation.FailureReason, Does.Contain("Variable [nonexistent] not found"));
            });
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ThenClause_EmptyAfterThen_ShouldFail()
        {
            // Arrange
            string testFile = Path.Combine(testDirectory!, "test.txt");
            File.WriteAllText(testFile, "Data");

            // Act - THEN without following command
            (ValidationResult validation, _, _) =
                engine!.Run(new ProcessedPrompt($"GET [data] FROM {testFile} THEN ."));

            // Assert
            Assert.That(validation.IsValid, Is.False);
        }

        [Test]
        public void ThenClause_OnlyThen_ShouldFail()
        {
            // Act
            (ValidationResult validation, _, _) =
                engine!.Run(new ProcessedPrompt("THEN."));

            // Assert
            Assert.That(validation.IsValid, Is.False);
        }

        #endregion

        #region Complex Chaining Tests

        [Test]
        public void ThenClause_FourChains_ComplexWorkflow_ShouldExecuteAllSteps()
        {
            // Arrange - Setup test files
            string inputFile = Path.Combine(testDirectory!, "input.txt");
            string tempFile1 = Path.Combine(testDirectory!, "temp1.txt");
            string tempFile2 = Path.Combine(testDirectory!, "temp2.txt");
            string finalFile = Path.Combine(testDirectory!, "final.txt");
            string logFile = Path.Combine(testDirectory!, "log.txt");

            File.WriteAllText(inputFile, "Initial Data");

            // Act - Complex workflow with 4 THEN clauses:
            // 1. GET from input file
            // 2. SAVE to temp1
            // 3. GET from temp1
            // 4. SAVE to temp2
            // 5. GET from temp2 and SAY the result
            string complexSentence =
                $"GET [step1] FROM {inputFile} " +
                $"THEN SAVE [step1] TO {tempFile1} " +
                $"THEN GET [step2] FROM {tempFile1} " +
                $"THEN SAVE [step2] TO {tempFile2} " +
                $"THEN GET [final] FROM {tempFile2}.";

            Console.WriteLine($"\n📝 Complex Sentence:\n{complexSentence}\n");

            (ValidationResult validation, ISentence? parsedSentence, object? result) =
                engine!.Run(new ProcessedPrompt(complexSentence));

            // Assert
            Assert.Multiple(() =>
            {
                // Validation should succeed
                Assert.That(validation.IsValid, Is.True, "Validation should succeed");

                // Sentence should have 4 sub-sentences
                Assert.That(parsedSentence, Is.Not.Null, "Sentence should not be null");
                Assert.That(parsedSentence!.HasSubSentences, Is.True, "Sentence should have sub-sentences");
                Assert.That(parsedSentence.SubSentences.Count, Is.EqualTo(4), "Should have exactly 4 sub-sentences");

                // All intermediate files should exist
                Assert.That(File.Exists(tempFile1), Is.True, "Temp file 1 should exist");
                Assert.That(File.Exists(tempFile2), Is.True, "Temp file 2 should exist");

                // Final result should contain the original data
                Assert.That(result, Is.Not.Null, "Result should not be null");
                Assert.That(result, Is.InstanceOf<string[]>(), "Result should be string array");

                string[] lines = (string[])result!;
                Assert.That(lines.Length, Is.GreaterThan(0), "Should have at least one line");

                string content = string.Join("", lines);
                Assert.That(content, Does.Contain("Initial Data"), "Final result should contain original data");

                // Verify file contents match
                string temp1Content = File.ReadAllText(tempFile1);
                string temp2Content = File.ReadAllText(tempFile2);
                Assert.That(temp1Content, Does.Contain("Initial Data"), "Temp1 should contain original data");
                Assert.That(temp2Content, Does.Contain("Initial Data"), "Temp2 should contain original data");
            });

            // Additional verification: Print the sentence structure
            Console.WriteLine($"\n🏗️  Sentence Structure:");
            Console.WriteLine($"   Main sentence has {parsedSentence!.SubSentences.Count} sub-sentences");
            for (int i = 0; i < parsedSentence.SubSentences.Count; i++)
            {
                Console.WriteLine($"      Sub-sentence [{i + 1}]");
            }
        }

        [Test]
        public void ThenClause_FourChains_WithSAY_ShouldPrintAndExecute()
        {
            // Arrange
            string file1 = Path.Combine(testDirectory!, "data1.txt");
            string file2 = Path.Combine(testDirectory!, "data2.txt");
            string file3 = Path.Combine(testDirectory!, "data3.txt");

            File.WriteAllText(file1, "Step One");
            File.WriteAllText(file2, "Step Two");
            File.WriteAllText(file3, "Step Three");

            // Act - Chain with multiple SAY commands to verify output
            string testSentence =
                $"GET [d1] FROM {file1} " +
                $"THEN SAY [d1] " +
                $"THEN GET [d2] FROM {file2} " +
                $"THEN SAY [d2] " +
                $"THEN GET [d3] FROM {file3}.";

            Console.WriteLine($"\n📝 Test Sentence:\n{testSentence}\n");
            Console.WriteLine("🔊 Expected Output: 'Step One' and 'Step Two'\n");

            (ValidationResult validation, ISentence? parsedSentence, object? result) =
                engine!.Run(new ProcessedPrompt(testSentence));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(parsedSentence, Is.Not.Null);
                Assert.That(parsedSentence!.SubSentences.Count, Is.EqualTo(4));

                // Final result should be from GET [d3]
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<string[]>());

                string finalContent = string.Join("", (string[])result!);
                Assert.That(finalContent, Does.Contain("Step Three"));
            });
        }

        [Test]
        public void ThenClause_FourChains_VariablePersistence_ShouldMaintainAllVariables()
        {
            // Arrange
            string file1 = Path.Combine(testDirectory!, "v1.txt");
            string file2 = Path.Combine(testDirectory!, "v2.txt");
            string file3 = Path.Combine(testDirectory!, "v3.txt");
            string file4 = Path.Combine(testDirectory!, "v4.txt");
            string outputFile = Path.Combine(testDirectory!, "combined.txt");

            File.WriteAllText(file1, "Value-1");
            File.WriteAllText(file2, "Value-2");
            File.WriteAllText(file3, "Value-3");
            File.WriteAllText(file4, "Value-4");

            // Act - Load 4 variables, then use first one in final command
            string testSentence =
                $"GET [var1] FROM {file1} " +
                $"THEN GET [var2] FROM {file2} " +
                $"THEN GET [var3] FROM {file3} " +
                $"THEN GET [var4] FROM {file4} " +
                $"THEN SAVE [var1] TO {outputFile}.";

            (ValidationResult validation, ISentence? parsedSentence, object? result) =
                engine!.Run(new ProcessedPrompt(testSentence));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True);
                Assert.That(parsedSentence, Is.Not.Null);
                Assert.That(parsedSentence!.SubSentences.Count, Is.EqualTo(4));

                // Output file should contain the first variable's value
                Assert.That(File.Exists(outputFile), Is.True);
                string savedContent = File.ReadAllText(outputFile);
                Assert.That(savedContent, Does.Contain("Value-1"),
                    "Should save the first variable's value, proving variables persist across all chains");
            });
        }

        #endregion
    }
}