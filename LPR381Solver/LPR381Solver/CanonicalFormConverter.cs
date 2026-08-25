// Member 1 contribution
// Contributor: Dewald Allers
// Scope: Canonical-form conversion and display formatting.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LPR381Solver
{
    public static class CanonicalFormConverter
    {
        private const double ZeroTolerance = 1e-12;

        public static CanonicalForm Convert(LinearProgrammingModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var decisionVariables = new List<CanonicalVariable>();
            var mappings = new List<OriginalVariableMapping>();

            for (int j = 0; j < model.VariableCount; j++)
            {
                string baseName = $"x{j + 1}";
                switch (model.VariableRestrictions[j])
                {
                    case VariableRestriction.NonPositive:
                    {
                        int index = decisionVariables.Count;
                        decisionVariables.Add(new CanonicalVariable(baseName + "_neg", CanonicalVariableKind.Decision, j, -1.0));
                        mappings.Add(new OriginalVariableMapping(j, new[] { index }, new[] { -1.0 }));
                        break;
                    }
                    case VariableRestriction.Unrestricted:
                    {
                        int positiveIndex = decisionVariables.Count;
                        decisionVariables.Add(new CanonicalVariable(baseName + "_pos", CanonicalVariableKind.Decision, j, 1.0));
                        int negativeIndex = decisionVariables.Count;
                        decisionVariables.Add(new CanonicalVariable(baseName + "_neg", CanonicalVariableKind.Decision, j, -1.0));
                        mappings.Add(new OriginalVariableMapping(j, new[] { positiveIndex, negativeIndex }, new[] { 1.0, -1.0 }));
                        break;
                    }
                    default:
                    {
                        int index = decisionVariables.Count;
                        decisionVariables.Add(new CanonicalVariable(baseName, CanonicalVariableKind.Decision, j, 1.0));
                        mappings.Add(new OriginalVariableMapping(j, new[] { index }, new[] { 1.0 }));
                        break;
                    }
                }
            }

            int decisionCount = decisionVariables.Count;
            int rowCount = model.ConstraintCount;
            var transformedObjective = new double[decisionCount];
            var transformedRows = new double[rowCount, decisionCount];

            for (int originalIndex = 0; originalIndex < model.VariableCount; originalIndex++)
            {
                OriginalVariableMapping mapping = mappings[originalIndex];
                for (int term = 0; term < mapping.CanonicalIndexes.Length; term++)
                {
                    int canonicalIndex = mapping.CanonicalIndexes[term];
                    double multiplier = mapping.Multipliers[term];
                    transformedObjective[canonicalIndex] += model.ObjectiveCoefficients[originalIndex] * multiplier;

                    for (int row = 0; row < rowCount; row++)
                        transformedRows[row, canonicalIndex] += model.Constraints[row].Coefficients[originalIndex] * multiplier;
                }
            }

            if (model.ObjectiveSense == ObjectiveSense.Minimize)
            {
                for (int j = 0; j < transformedObjective.Length; j++)
                    transformedObjective[j] = -transformedObjective[j];
            }

            var normalizedRelations = new ConstraintRelation[rowCount];
            var rhs = new double[rowCount];
            for (int row = 0; row < rowCount; row++)
            {
                normalizedRelations[row] = model.Constraints[row].Relation;
                rhs[row] = NormalizeZero(model.Constraints[row].RightHandSide);

                if (rhs[row] < 0.0)
                {
                    rhs[row] = -rhs[row];
                    for (int column = 0; column < decisionCount; column++)
                        transformedRows[row, column] = -transformedRows[row, column];
                    normalizedRelations[row] = Flip(normalizedRelations[row]);
                }
            }

            int auxiliaryCount = normalizedRelations.Sum(relation =>
                relation == ConstraintRelation.LessThanOrEqual ? 1 :
                relation == ConstraintRelation.GreaterThanOrEqual ? 2 : 1);
            int totalColumns = decisionCount + auxiliaryCount;

            var variables = new List<CanonicalVariable>(decisionVariables);
            var matrix = new double[rowCount, totalColumns];
            var objective = new double[totalColumns];
            Array.Copy(transformedObjective, objective, transformedObjective.Length);

            for (int row = 0; row < rowCount; row++)
                for (int column = 0; column < decisionCount; column++)
                    matrix[row, column] = NormalizeZero(transformedRows[row, column]);

            var basis = new int[rowCount];
            var artificialIndexes = new List<int>();
            int slackNumber = 0;
            int surplusNumber = 0;
            int artificialNumber = 0;
            int nextColumn = decisionCount;

            for (int row = 0; row < rowCount; row++)
            {
                switch (normalizedRelations[row])
                {
                    case ConstraintRelation.LessThanOrEqual:
                        slackNumber++;
                        variables.Add(new CanonicalVariable($"s{slackNumber}", CanonicalVariableKind.Slack));
                        matrix[row, nextColumn] = 1.0;
                        basis[row] = nextColumn;
                        nextColumn++;
                        break;

                    case ConstraintRelation.GreaterThanOrEqual:
                        surplusNumber++;
                        variables.Add(new CanonicalVariable($"e{surplusNumber}", CanonicalVariableKind.Surplus));
                        matrix[row, nextColumn] = -1.0;
                        nextColumn++;

                        artificialNumber++;
                        variables.Add(new CanonicalVariable($"a{artificialNumber}", CanonicalVariableKind.Artificial));
                        matrix[row, nextColumn] = 1.0;
                        basis[row] = nextColumn;
                        artificialIndexes.Add(nextColumn);
                        nextColumn++;
                        break;

                    case ConstraintRelation.Equal:
                        artificialNumber++;
                        variables.Add(new CanonicalVariable($"a{artificialNumber}", CanonicalVariableKind.Artificial));
                        matrix[row, nextColumn] = 1.0;
                        basis[row] = nextColumn;
                        artificialIndexes.Add(nextColumn);
                        nextColumn++;
                        break;
                }
            }

            return new CanonicalForm
            {
                OriginalObjectiveSense = model.ObjectiveSense,
                OriginalObjectiveValueMultiplier = model.ObjectiveSense == ObjectiveSense.Maximize ? 1.0 : -1.0,
                Variables = variables.ToArray(),
                DecisionVariableCount = decisionCount,
                ObjectiveCoefficients = objective,
                ConstraintMatrix = matrix,
                RightHandSides = rhs,
                NormalizedRelations = normalizedRelations,
                BasisIndexes = basis,
                ArtificialVariableIndexes = artificialIndexes.ToArray(),
                OriginalVariableMappings = mappings.ToArray(),
                OriginalVariableRestrictions = (VariableRestriction[])model.VariableRestrictions.Clone()
            };
        }

        private static ConstraintRelation Flip(ConstraintRelation relation)
        {
            if (relation == ConstraintRelation.LessThanOrEqual)
                return ConstraintRelation.GreaterThanOrEqual;
            if (relation == ConstraintRelation.GreaterThanOrEqual)
                return ConstraintRelation.LessThanOrEqual;
            return ConstraintRelation.Equal;
        }

        private static double NormalizeZero(double value)
        {
            return Math.Abs(value) < ZeroTolerance ? 0.0 : value;
        }
    }

    public static class CanonicalFormFormatter
    {
        public static string Format(CanonicalForm form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            string[] names = form.Variables.Select(variable => variable.Name).ToArray();
            var output = new StringBuilder();
            output.AppendLine("CANONICAL FORM");
            output.AppendLine(new string('=', 72));

            if (form.OriginalObjectiveSense == ObjectiveSense.Minimize)
                output.AppendLine("Original minimisation converted to maximisation by negating the objective.");

            output.AppendLine("Maximise z = " + FormatExpression(form.ObjectiveCoefficients, names));
            output.AppendLine("Objective row: z" + FormatExpression(form.ObjectiveCoefficients.Select(value => -value).ToArray(), names, true) + " = 0.000");
            output.AppendLine("Subject to:");

            for (int row = 0; row < form.RightHandSides.Length; row++)
            {
                double[] coefficients = GetRow(form.ConstraintMatrix, row);
                output.AppendLine($"  ({row + 1}) {FormatExpression(coefficients, names)} = {FormatNumber(form.RightHandSides[row])}");
            }

            output.AppendLine();
            output.AppendLine("Initial basis: " + string.Join(", ", form.BasisIndexes.Select(index => names[index])));
            output.AppendLine("Non-negativity: " + string.Join(", ", names.Select(name => name + " >= 0")));

            if (form.RequiresPhaseOne)
            {
                output.AppendLine("Phase I required for artificial variables: " +
                    string.Join(", ", form.ArtificialVariableIndexes.Select(index => names[index])));
            }

            AppendSubstitutions(output, form);
            AppendDiscreteRestrictions(output, form);
            return output.ToString();
        }

        private static void AppendSubstitutions(StringBuilder output, CanonicalForm form)
        {
            var substitutions = new List<string>();
            foreach (OriginalVariableMapping mapping in form.OriginalVariableMappings)
            {
                VariableRestriction restriction = form.OriginalVariableRestrictions[mapping.OriginalVariableIndex];
                if (restriction == VariableRestriction.NonPositive)
                {
                    substitutions.Add($"x{mapping.OriginalVariableIndex + 1} = -{form.Variables[mapping.CanonicalIndexes[0]].Name}");
                }
                else if (restriction == VariableRestriction.Unrestricted)
                {
                    substitutions.Add($"x{mapping.OriginalVariableIndex + 1} = {form.Variables[mapping.CanonicalIndexes[0]].Name} - {form.Variables[mapping.CanonicalIndexes[1]].Name}");
                }
            }

            if (substitutions.Count > 0)
            {
                output.AppendLine("Variable substitutions:");
                foreach (string substitution in substitutions)
                    output.AppendLine("  " + substitution);
            }
        }

        private static void AppendDiscreteRestrictions(StringBuilder output, CanonicalForm form)
        {
            var restrictions = new List<string>();
            for (int i = 0; i < form.OriginalVariableRestrictions.Length; i++)
            {
                if (form.OriginalVariableRestrictions[i] == VariableRestriction.Integer)
                    restrictions.Add($"x{i + 1} integer");
                else if (form.OriginalVariableRestrictions[i] == VariableRestriction.Binary)
                    restrictions.Add($"x{i + 1} binary (0 or 1)");
            }

            if (restrictions.Count > 0)
                output.AppendLine("Discrete restrictions: " + string.Join(", ", restrictions));
        }

        public static string FormatExpression(double[] coefficients, string[] names, bool includeLeadingSign = false)
        {
            var expression = new StringBuilder();
            bool wroteTerm = false;

            for (int i = 0; i < coefficients.Length; i++)
            {
                double coefficient = Math.Abs(coefficients[i]) < 0.0000005 ? 0.0 : coefficients[i];
                if (coefficient == 0.0)
                    continue;

                string sign = coefficient < 0.0 ? "-" : "+";
                if (!wroteTerm && !includeLeadingSign)
                {
                    if (coefficient < 0.0)
                        expression.Append("-");
                }
                else
                {
                    expression.Append(" ").Append(sign).Append(" ");
                }

                expression.Append(FormatNumber(Math.Abs(coefficient))).Append(names[i]);
                wroteTerm = true;
            }

            if (!wroteTerm)
                return includeLeadingSign ? " + 0.000" : "0.000";
            return expression.ToString();
        }

        public static string FormatNumber(double value)
        {
            if (Math.Abs(value) < 0.0005)
                value = 0.0;
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static double[] GetRow(double[,] matrix, int row)
        {
            int columns = matrix.GetLength(1);
            var result = new double[columns];
            for (int column = 0; column < columns; column++)
                result[column] = matrix[row, column];
            return result;
        }
    }
}
