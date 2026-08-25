// Member 1 contribution
// Contributor: Dewald Allers
// Scope: Dependency-free verification of parsing, canonical conversion, output, and errors.

using System;
using System.IO;
using LPR381Solver;

namespace Member1Verification
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                VerifySantaModelAndSolver();
                VerifyKnapsackRestrictions();
                VerifyGeneralCanonicalConversion();
                VerifyInvalidInputHandling();
                VerifyUnboundedHandling();
                Console.WriteLine("Member 1 verification passed: 5/5 checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Member 1 verification failed: " + ex.Message);
                return 1;
            }
        }

        private static void VerifySantaModelAndSolver()
        {
            string[] lines =
            {
                "max +3 +2",
                "+2 +1 <= 100",
                "+1 +1 <= 80",
                "+1 +0 <= 40",
                "+ +"
            };

            string tempDirectory = Path.Combine(Path.GetTempPath(), "LPR381-Member1-" + Guid.NewGuid().ToString("N"));
            string inputPath = Path.Combine(tempDirectory, "santas-workshop.txt");
            string outputPath = Path.Combine(tempDirectory, "santas-workshop-output.txt");

            try
            {
                Directory.CreateDirectory(tempDirectory);
                File.WriteAllLines(inputPath, lines);
                LinearProgrammingModel model = ModelInputParser.ParseFile(inputPath);
                CanonicalForm canonical = CanonicalFormConverter.Convert(model);
                Assert(model.VariableCount == 2, "Santa model variable count is incorrect.");
                Assert(model.ConstraintCount == 3, "Santa model constraint count is incorrect.");
                Assert(!canonical.RequiresPhaseOne, "Santa model should have a direct slack basis.");

                SolverExecution execution = new PrimalSimplexAdapter().Solve(model, canonical);
                Assert(Math.Abs(execution.SimplexResult.GetOriginalOptimalValue() - 180.0) < 0.000001,
                    "Santa model optimal value should be 180.000.");

                OutputFileWriter.Write(outputPath, inputPath, model, canonical, execution.Report);
                string output = File.ReadAllText(outputPath);
                Assert(output.Contains("Dewald Allers"), "Output is missing the Member 1 contributor attribution.");
                Assert(output.Contains("180.000"), "Output does not use the required three-decimal format.");
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }
        }

        private static void VerifyKnapsackRestrictions()
        {
            LinearProgrammingModel model = ModelInputParser.ParseLines(new[]
            {
                "max +2 +3 +3 +5 +2 +4",
                "+11 +8 +6 +14 +10 +10 <= 40",
                "bin bin bin bin bin bin"
            });
            Assert(model.VariableCount == 6, "Knapsack variable count is incorrect.");
            foreach (VariableRestriction restriction in model.VariableRestrictions)
                Assert(restriction == VariableRestriction.Binary, "Knapsack variables must remain binary.");
        }

        private static void VerifyGeneralCanonicalConversion()
        {
            LinearProgrammingModel model = ModelInputParser.ParseLines(new[]
            {
                "min -2 +3",
                "-1 +2 >= -4",
                "+1 -1 = 3",
                "- urs"
            });
            CanonicalForm canonical = CanonicalFormConverter.Convert(model);
            Assert(canonical.DecisionVariableCount == 3, "Negative/unrestricted substitution count is incorrect.");
            Assert(canonical.RequiresPhaseOne, "Equality constraint should create an artificial variable.");
            Assert(canonical.NormalizedRelations[0] == ConstraintRelation.LessThanOrEqual,
                "Negative RHS normalization did not flip the relation.");
            Assert(canonical.OriginalObjectiveValueMultiplier == -1.0,
                "Minimisation objective mapping is incorrect.");
        }

        private static void VerifyInvalidInputHandling()
        {
            bool rejected = false;
            try
            {
                ModelInputParser.ParseLines(new[]
                {
                    "max +2 +3",
                    "+1 <= 4",
                    "+ +"
                });
            }
            catch (ModelInputException ex)
            {
                rejected = ex.LineNumber == 2;
            }
            Assert(rejected, "Malformed coefficient counts must produce a line-specific error.");
        }

        private static void VerifyUnboundedHandling()
        {
            LinearProgrammingModel model = ModelInputParser.ParseLines(new[]
            {
                "max +1",
                "-1 <= 1",
                "+"
            });
            CanonicalForm canonical = CanonicalFormConverter.Convert(model);
            bool rejected = false;
            try
            {
                new PrimalSimplexAdapter().Solve(model, canonical);
            }
            catch (InvalidOperationException ex)
            {
                rejected = ex.Message.IndexOf("unbounded", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            Assert(rejected, "Unbounded models must be identified clearly.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
