// Member 1 contribution
// Contributor: Dewald Allers
// Scope: Validated input-file parsing for the LPR381 project format.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LPR381Solver
{
    public static class ModelInputParser
    {
        private static readonly Regex ConstraintPattern = new Regex(
            @"^(?<left>.+?)\s*(?<relation><=|>=|=>|=<|=)\s*(?<rhs>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static LinearProgrammingModel ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ModelInputException("Select an input text file first.");
            if (!File.Exists(path))
                throw new ModelInputException($"The input file does not exist: {path}");

            try
            {
                return ParseLines(File.ReadAllLines(path));
            }
            catch (ModelInputException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ModelInputException("The input file cannot be read because access was denied.", 0, ex);
            }
            catch (IOException ex)
            {
                throw new ModelInputException($"The input file could not be read: {ex.Message}", 0, ex);
            }
        }

        public static LinearProgrammingModel ParseLines(IEnumerable<string> sourceLines)
        {
            if (sourceLines == null)
                throw new ArgumentNullException(nameof(sourceLines));

            List<NumberedLine> lines = sourceLines
                .Select((text, index) => new NumberedLine(index + 1, text == null ? string.Empty : text.Trim()))
                .Where(line => line.Text.Length > 0 && !line.Text.StartsWith("#"))
                .ToList();

            if (lines.Count < 3)
                throw new ModelInputException("The file must contain an objective line, at least one constraint, and a final sign-restriction line.");

            NumberedLine objectiveLine = lines[0];
            string[] objectiveTokens = SplitTokens(objectiveLine.Text);
            if (objectiveTokens.Length < 2)
                throw new ModelInputException("The objective line must start with max or min and include at least one signed coefficient.", objectiveLine.Number);

            ObjectiveSense sense;
            if (objectiveTokens[0].Equals("max", StringComparison.OrdinalIgnoreCase))
                sense = ObjectiveSense.Maximize;
            else if (objectiveTokens[0].Equals("min", StringComparison.OrdinalIgnoreCase))
                sense = ObjectiveSense.Minimize;
            else
                throw new ModelInputException("The objective line must start with max or min.", objectiveLine.Number);

            double[] objective = ParseSignedCoefficients(objectiveTokens.Skip(1).ToArray(), objectiveLine.Number).ToArray();
            if (objective.Length == 0)
                throw new ModelInputException("The objective function contains no coefficients.", objectiveLine.Number);

            NumberedLine restrictionLine = lines[lines.Count - 1];
            VariableRestriction[] restrictions = ParseRestrictions(restrictionLine, objective.Length);

            var constraints = new List<LinearConstraint>();
            for (int i = 1; i < lines.Count - 1; i++)
                constraints.Add(ParseConstraint(lines[i], objective.Length));

            if (constraints.Count == 0)
                throw new ModelInputException("At least one constraint is required.");

            try
            {
                return new LinearProgrammingModel(sense, objective, constraints, restrictions);
            }
            catch (ModelValidationException ex)
            {
                throw new ModelInputException(ex.Message, 0, ex);
            }
        }

        private static LinearConstraint ParseConstraint(NumberedLine line, int expectedCoefficientCount)
        {
            Match match = ConstraintPattern.Match(line.Text);
            if (!match.Success)
                throw new ModelInputException("A constraint must contain signed coefficients, a relation (=, <=, or >=), and one numeric right-hand side.", line.Number);

            double[] coefficients = ParseSignedCoefficients(SplitTokens(match.Groups["left"].Value), line.Number).ToArray();
            if (coefficients.Length != expectedCoefficientCount)
                throw new ModelInputException($"The constraint has {coefficients.Length} coefficients; expected {expectedCoefficientCount}.", line.Number);

            ConstraintRelation relation = ParseRelation(match.Groups["relation"].Value, line.Number);
            double rhs = ParseNumber(match.Groups["rhs"].Value, "right-hand side", line.Number);
            return new LinearConstraint(coefficients, relation, rhs);
        }

        private static IEnumerable<double> ParseSignedCoefficients(string[] tokens, int lineNumber)
        {
            var coefficients = new List<double>();
            int index = 0;

            while (index < tokens.Length)
            {
                string token = tokens[index];
                if (token == "+" || token == "-")
                {
                    if (index + 1 >= tokens.Length)
                        throw new ModelInputException($"The sign '{token}' is missing its coefficient.", lineNumber);

                    string numericToken = tokens[index + 1];
                    if (numericToken.StartsWith("+") || numericToken.StartsWith("-"))
                        throw new ModelInputException($"Coefficient '{numericToken}' must not repeat the preceding sign '{token}'.", lineNumber);

                    double value = ParseNumber(numericToken, "coefficient", lineNumber);
                    coefficients.Add(token == "-" ? -value : value);
                    index += 2;
                    continue;
                }

                if (token.Length < 2 || (token[0] != '+' && token[0] != '-'))
                    throw new ModelInputException($"Coefficient '{token}' must include an explicit + or - sign.", lineNumber);

                coefficients.Add(ParseNumber(token, "coefficient", lineNumber));
                index++;
            }

            return coefficients;
        }

        private static VariableRestriction[] ParseRestrictions(NumberedLine line, int expectedCount)
        {
            string[] tokens = SplitTokens(line.Text);
            if (tokens.Length != expectedCount)
                throw new ModelInputException($"The sign-restriction line has {tokens.Length} entries; expected {expectedCount}.", line.Number);

            var restrictions = new VariableRestriction[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                switch (tokens[i].ToLowerInvariant())
                {
                    case "+":
                        restrictions[i] = VariableRestriction.NonNegative;
                        break;
                    case "-":
                        restrictions[i] = VariableRestriction.NonPositive;
                        break;
                    case "urs":
                        restrictions[i] = VariableRestriction.Unrestricted;
                        break;
                    case "int":
                        restrictions[i] = VariableRestriction.Integer;
                        break;
                    case "bin":
                        restrictions[i] = VariableRestriction.Binary;
                        break;
                    default:
                        throw new ModelInputException($"Unknown variable restriction '{tokens[i]}'. Use +, -, urs, int, or bin.", line.Number);
                }
            }

            return restrictions;
        }

        private static ConstraintRelation ParseRelation(string token, int lineNumber)
        {
            switch (token)
            {
                case "<=":
                case "=<":
                    return ConstraintRelation.LessThanOrEqual;
                case ">=":
                case "=>":
                    return ConstraintRelation.GreaterThanOrEqual;
                case "=":
                    return ConstraintRelation.Equal;
                default:
                    throw new ModelInputException($"Unknown constraint relation '{token}'.", lineNumber);
            }
        }

        private static double ParseNumber(string token, string description, int lineNumber)
        {
            double value;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ModelInputException($"'{token}' is not a valid finite {description}.", lineNumber);
            }

            return value;
        }

        private static string[] SplitTokens(string value)
        {
            return value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private sealed class NumberedLine
        {
            public NumberedLine(int number, string text)
            {
                Number = number;
                Text = text;
            }

            public int Number { get; private set; }
            public string Text { get; private set; }
        }
    }
}
