using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381Solver
{
    /// <summary>
    /// All of the below code was generated with AI and is only here for the testing of Sensitivity analysis.
    /// </summary>
    public class SimplexResult
    {
        // Final tableau: rows = constraints (+1 objective row at index 0),
        // columns = original decision vars + slack vars + RHS (last column)
        public double[,] Tableau;
        public int[] Basis;          // column index of the basic variable for each constraint row
        public int NumOriginalVars;
        public int NumSlacks;
        public int NumConstraints;
        public double[] CVector;     // original objective coefficients (decision vars only)
        public List<double[,]> Iterations; // snapshot of tableau at every iteration

        // Member 1 integration additions - Contributor: Dewald Allers
        public string[] DecisionVariableNames;
        public double OriginalObjectiveValueMultiplier = 1.0;

        public double[,] GetBInverse()
        {
            // B-inverse = the columns of the final tableau that correspond
            // to where the slack (identity) columns started.
            int rows = NumConstraints;
            double[,] bInv = new double[rows, rows];
            for (int i = 0; i < rows; i++)
            {
                int slackCol = NumOriginalVars + i;
                for (int r = 0; r < rows; r++)
                    bInv[r, i] = Tableau[r + 1, slackCol]; // +1 to skip objective row
            }
            return bInv;
        }

        public double[] GetShadowPrices()
        {
            // Reduced cost of each slack column in the final objective row
            double[] shadow = new double[NumConstraints];
            for (int i = 0; i < NumConstraints; i++)
                shadow[i] = Tableau[0, NumOriginalVars + i];
            return shadow;
        }

        public double GetReducedCost(int colIndex) => Tableau[0, colIndex];

        public double GetOptimalValue() => Tableau[0, Tableau.GetLength(1) - 1];

        public double GetOriginalOptimalValue() => GetOptimalValue() * OriginalObjectiveValueMultiplier;
    }

    public static class PrimalSimplex
    {
        // c: objective coefficients (max c.x), A: constraint matrix (<=), b: RHS (all >= 0)
        public static SimplexResult Solve(double[] c, double[,] A, double[] b)
        {
            int m = A.GetLength(0); // constraints
            int n = A.GetLength(1); // decision vars
            int totalCols = n + m + 1; // decision vars + slacks + RHS
            int totalRows = m + 1;     // + objective row

            double[,] T = new double[totalRows, totalCols];

            // Objective row (row 0): -c for decision vars, 0 for slacks, 0 RHS
            for (int j = 0; j < n; j++) T[0, j] = -c[j];

            // Constraint rows
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) T[i + 1, j] = A[i, j];
                T[i + 1, n + i] = 1.0;          // slack identity column
                T[i + 1, totalCols - 1] = b[i]; // RHS
            }

            int[] basis = Enumerable.Range(n, m).ToArray(); // slacks start basic
            var iterations = new List<double[,]> { (double[,])T.Clone() };

            int guard = 0;
            while (guard++ < 200)
            {
                // Find entering column: most negative in objective row
                int pivotCol = -1;
                double mostNeg = -1e-9;
                for (int j = 0; j < totalCols - 1; j++)
                {
                    if (T[0, j] < mostNeg) { mostNeg = T[0, j]; pivotCol = j; }
                }
                if (pivotCol == -1) break; // optimal

                // Ratio test to find leaving row
                int pivotRow = -1;
                double bestRatio = double.PositiveInfinity;
                for (int i = 1; i < totalRows; i++)
                {
                    if (T[i, pivotCol] > 1e-9)
                    {
                        double ratio = T[i, totalCols - 1] / T[i, pivotCol];
                        if (ratio < bestRatio - 1e-9)
                        {
                            bestRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }
                if (pivotRow == -1)
                    throw new UnboundedModelException();

                // Pivot
                double pivotVal = T[pivotRow, pivotCol];
                for (int j = 0; j < totalCols; j++) T[pivotRow, j] /= pivotVal;

                for (int i = 0; i < totalRows; i++)
                {
                    if (i == pivotRow) continue;
                    double factor = T[i, pivotCol];
                    if (Math.Abs(factor) < 1e-12) continue;
                    for (int j = 0; j < totalCols; j++)
                        T[i, j] -= factor * T[pivotRow, j];
                }

                basis[pivotRow - 1] = pivotCol;
                iterations.Add((double[,])T.Clone());
            }

            return new SimplexResult
            {
                Tableau = T,
                Basis = basis,
                NumOriginalVars = n,
                NumSlacks = m,
                NumConstraints = m,
                CVector = c,
                Iterations = iterations
            };
        }

        // Formats a tableau as a string, for display in a TextBox, log, or console.
        public static string TableauToString(double[,] T)
        {
            var sb = new StringBuilder();
            int rows = T.GetLength(0);
            int cols = T.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                    sb.Append(T[i, j].ToString("0.000", CultureInfo.InvariantCulture).PadLeft(12));
                sb.AppendLine();
            }
            sb.AppendLine(new string('-', 12 * cols));
            return sb.ToString();
        }
    }
 
}
