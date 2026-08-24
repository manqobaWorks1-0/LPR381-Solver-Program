// Member 1 contribution
// Contributor: Dewald Allers
// Scope: Original-model formatting and required output text-file generation.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LPR381Solver
{
    public static class ModelFormatter
    {
        public static string Format(LinearProgrammingModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var output = new StringBuilder();
            output.AppendLine("ORIGINAL PROGRAMMING MODEL");
            output.AppendLine(new string('=', 72));
            output.Append(model.ObjectiveSense == ObjectiveSense.Maximize ? "max" : "min");
            foreach (double coefficient in model.ObjectiveCoefficients)
                output.Append(" ").Append(FormatSigned(coefficient));
            output.AppendLine();

            foreach (LinearConstraint constraint in model.Constraints)
            {
                foreach (double coefficient in constraint.Coefficients)
                    output.Append(FormatSigned(coefficient)).Append(" ");
                output.Append(RelationToken(constraint.Relation)).Append(" ");
                output.AppendLine(CanonicalFormFormatter.FormatNumber(constraint.RightHandSide));
            }

            output.AppendLine(string.Join(" ", model.VariableRestrictions.Select(RestrictionToken)));
            return output.ToString();
        }

        private static string FormatSigned(double value)
        {
            return (value < 0.0 ? "-" : "+") + CanonicalFormFormatter.FormatNumber(Math.Abs(value));
        }

        private static string RelationToken(ConstraintRelation relation)
        {
            if (relation == ConstraintRelation.LessThanOrEqual)
                return "<=";
            if (relation == ConstraintRelation.GreaterThanOrEqual)
                return ">=";
            return "=";
        }

        private static string RestrictionToken(VariableRestriction restriction)
        {
            switch (restriction)
            {
                case VariableRestriction.NonNegative: return "+";
                case VariableRestriction.NonPositive: return "-";
                case VariableRestriction.Unrestricted: return "urs";
                case VariableRestriction.Integer: return "int";
                case VariableRestriction.Binary: return "bin";
                default: throw new ArgumentOutOfRangeException(nameof(restriction));
            }
        }
    }

    public static class OutputFileWriter
    {
        public static void Write(
            string outputPath,
            string inputPath,
            LinearProgrammingModel model,
            CanonicalForm canonicalForm,
            SolverRunReport report)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new OutputWriteException("Select an output text file first.");

            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!Directory.Exists(directory))
                    throw new OutputWriteException($"The output directory does not exist: {directory}");

                File.WriteAllText(outputPath, Build(inputPath, model, canonicalForm, report), new UTF8Encoding(false));
            }
            catch (OutputWriteException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new OutputWriteException("The output file cannot be written because access was denied.", ex);
            }
            catch (IOException ex)
            {
                throw new OutputWriteException($"The output file could not be written: {ex.Message}", ex);
            }
        }

        public static string Build(
            string inputPath,
            LinearProgrammingModel model,
            CanonicalForm canonicalForm,
            SolverRunReport report)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (canonicalForm == null)
                throw new ArgumentNullException(nameof(canonicalForm));

            var output = new StringBuilder();
            output.AppendLine("LPR381 LINEAR & INTEGER PROGRAMMING SOLVER");
            output.AppendLine("Member 1 contribution: Dewald Allers");
            output.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            output.AppendLine("Input file: " + (string.IsNullOrWhiteSpace(inputPath) ? "(in-memory model)" : inputPath));
            output.AppendLine(new string('=', 72));
            output.AppendLine();
            output.AppendLine(ModelFormatter.Format(model));
            output.AppendLine(CanonicalFormFormatter.Format(canonicalForm));

            if (report != null)
            {
                output.AppendLine();
                output.AppendLine("ALGORITHM RESULT");
                output.AppendLine(new string('=', 72));
                output.AppendLine("Algorithm: " + report.AlgorithmName);
                output.AppendLine("Status: " + (string.IsNullOrWhiteSpace(report.Status) ? "Unknown" : report.Status));
                if (!string.IsNullOrWhiteSpace(report.Summary))
                    output.AppendLine(report.Summary);

                for (int i = 0; i < report.Iterations.Count; i++)
                {
                    output.AppendLine();
                    output.AppendLine($"Iteration {i}");
                    output.AppendLine(new string('-', 72));
                    output.Append(report.Iterations[i]);
                }
            }

            return output.ToString();
        }
    }

    public sealed class OutputWriteException : Exception
    {
        public OutputWriteException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}
