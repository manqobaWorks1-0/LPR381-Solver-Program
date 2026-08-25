// Member 1 contribution
// Contributor: Dewald Allers
// Scope: Shared LP/IP domain model and solver-integration contracts.

using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381Solver
{
    public enum ObjectiveSense
    {
        Maximize,
        Minimize
    }

    public enum ConstraintRelation
    {
        LessThanOrEqual,
        Equal,
        GreaterThanOrEqual
    }

    public enum VariableRestriction
    {
        NonNegative,
        NonPositive,
        Unrestricted,
        Integer,
        Binary
    }

    public sealed class LinearConstraint
    {
        public LinearConstraint(IEnumerable<double> coefficients, ConstraintRelation relation, double rightHandSide)
        {
            if (coefficients == null)
                throw new ArgumentNullException(nameof(coefficients));

            Coefficients = coefficients.ToArray();
            Relation = relation;
            RightHandSide = rightHandSide;
        }

        public double[] Coefficients { get; private set; }
        public ConstraintRelation Relation { get; private set; }
        public double RightHandSide { get; private set; }
    }

    public sealed class LinearProgrammingModel
    {
        public LinearProgrammingModel(
            ObjectiveSense objectiveSense,
            IEnumerable<double> objectiveCoefficients,
            IEnumerable<LinearConstraint> constraints,
            IEnumerable<VariableRestriction> variableRestrictions)
        {
            if (objectiveCoefficients == null)
                throw new ArgumentNullException(nameof(objectiveCoefficients));
            if (constraints == null)
                throw new ArgumentNullException(nameof(constraints));
            if (variableRestrictions == null)
                throw new ArgumentNullException(nameof(variableRestrictions));

            ObjectiveSense = objectiveSense;
            ObjectiveCoefficients = objectiveCoefficients.ToArray();
            Constraints = constraints.ToArray();
            VariableRestrictions = variableRestrictions.ToArray();

            Validate();
        }

        public ObjectiveSense ObjectiveSense { get; private set; }
        public double[] ObjectiveCoefficients { get; private set; }
        public LinearConstraint[] Constraints { get; private set; }
        public VariableRestriction[] VariableRestrictions { get; private set; }

        public int VariableCount => ObjectiveCoefficients.Length;
        public int ConstraintCount => Constraints.Length;

        private void Validate()
        {
            if (ObjectiveCoefficients.Length == 0)
                throw new ModelValidationException("The model must contain at least one decision variable.");
            if (Constraints.Length == 0)
                throw new ModelValidationException("The model must contain at least one constraint.");
            if (VariableRestrictions.Length != ObjectiveCoefficients.Length)
                throw new ModelValidationException("The number of sign restrictions must equal the number of decision variables.");

            ValidateFinite(ObjectiveCoefficients, "objective coefficient");

            for (int i = 0; i < Constraints.Length; i++)
            {
                LinearConstraint constraint = Constraints[i];
                if (constraint == null)
                    throw new ModelValidationException($"Constraint {i + 1} is missing.");
                if (constraint.Coefficients.Length != ObjectiveCoefficients.Length)
                    throw new ModelValidationException($"Constraint {i + 1} has {constraint.Coefficients.Length} coefficients; expected {ObjectiveCoefficients.Length}.");

                ValidateFinite(constraint.Coefficients, $"coefficient in constraint {i + 1}");
                if (double.IsNaN(constraint.RightHandSide) || double.IsInfinity(constraint.RightHandSide))
                    throw new ModelValidationException($"The right-hand side of constraint {i + 1} is not a finite number.");
            }
        }

        private static void ValidateFinite(IEnumerable<double> values, string description)
        {
            if (values.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
                throw new ModelValidationException($"A {description} is not a finite number.");
        }
    }

    public sealed class ModelValidationException : Exception
    {
        public ModelValidationException(string message) : base(message)
        {
        }
    }

    public sealed class ModelInputException : Exception
    {
        public ModelInputException(string message, int lineNumber = 0, Exception innerException = null)
            : base(lineNumber > 0 ? $"Line {lineNumber}: {message}" : message, innerException)
        {
            LineNumber = lineNumber;
        }

        public int LineNumber { get; private set; }
    }

    public sealed class AlgorithmCompatibilityException : Exception
    {
        public AlgorithmCompatibilityException(string message) : base(message)
        {
        }
    }

    public sealed class AlgorithmUnavailableException : Exception
    {
        public AlgorithmUnavailableException(string message) : base(message)
        {
        }
    }

    public sealed class InfeasibleModelException : InvalidOperationException
    {
        public InfeasibleModelException(string message = "The programming model is infeasible.") : base(message)
        {
        }
    }

    public sealed class UnboundedModelException : InvalidOperationException
    {
        public UnboundedModelException(string message = "The programming model is unbounded.") : base(message)
        {
        }
    }

    public enum CanonicalVariableKind
    {
        Decision,
        Slack,
        Surplus,
        Artificial
    }

    public sealed class CanonicalVariable
    {
        public CanonicalVariable(string name, CanonicalVariableKind kind, int sourceVariableIndex = -1, double sourceMultiplier = 0.0)
        {
            Name = name;
            Kind = kind;
            SourceVariableIndex = sourceVariableIndex;
            SourceMultiplier = sourceMultiplier;
        }

        public string Name { get; private set; }
        public CanonicalVariableKind Kind { get; private set; }
        public int SourceVariableIndex { get; private set; }
        public double SourceMultiplier { get; private set; }
    }

    public sealed class OriginalVariableMapping
    {
        public OriginalVariableMapping(int originalVariableIndex, IEnumerable<int> canonicalIndexes, IEnumerable<double> multipliers)
        {
            OriginalVariableIndex = originalVariableIndex;
            CanonicalIndexes = canonicalIndexes.ToArray();
            Multipliers = multipliers.ToArray();
        }

        public int OriginalVariableIndex { get; private set; }
        public int[] CanonicalIndexes { get; private set; }
        public double[] Multipliers { get; private set; }
    }

    public sealed class CanonicalForm
    {
        public ObjectiveSense OriginalObjectiveSense { get; set; }
        public double OriginalObjectiveValueMultiplier { get; set; }
        public CanonicalVariable[] Variables { get; set; }
        public int DecisionVariableCount { get; set; }
        public double[] ObjectiveCoefficients { get; set; }
        public double[,] ConstraintMatrix { get; set; }
        public double[] RightHandSides { get; set; }
        public ConstraintRelation[] NormalizedRelations { get; set; }
        public int[] BasisIndexes { get; set; }
        public int[] ArtificialVariableIndexes { get; set; }
        public OriginalVariableMapping[] OriginalVariableMappings { get; set; }
        public VariableRestriction[] OriginalVariableRestrictions { get; set; }

        public bool RequiresPhaseOne => ArtificialVariableIndexes != null && ArtificialVariableIndexes.Length > 0;
    }

    public sealed class SolverRunReport
    {
        public SolverRunReport(string algorithmName)
        {
            AlgorithmName = algorithmName;
            Iterations = new List<string>();
        }

        public string AlgorithmName { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public List<string> Iterations { get; private set; }
    }
}
