using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LPR381Solver
{
    // This class holds the answer once the Primal Simplex has finished solving.
    // SensitivityAnalysis.cs uses this class too, so I kept the same field names
    // that were already here, and only added InitialBasis and IsMax on top - these
    // two extra fields are what make GetBInverse/GetShadowPrices/GetOptimalValue
    // still work correctly now that a constraint can start with more than one
    // extra column (for example >= adds a surplus AND an artificial column).
    public class SimplexResult
    {
        public double[,] Tableau;      // the final tableau after solving
        public int[] Basis;            // which column is basic in each row, at the end
        public int[] InitialBasis;     // which column was basic in each row, at the very start
        public int NumOriginalVars;
        public int NumSlacks;
        public int NumConstraints;
        public double[] CVector;
        public List<double[,]> Iterations;   // a snapshot of the tableau after every pivot
        public bool IsMax = true;            // true if we solved this as a "max" problem

        // B-inverse can be read straight out of the final tableau: it's just the
        // columns that were the identity columns when we started, read from the
        // final table. That's why we need to remember InitialBasis.
        public double[,] GetBInverse()
        {
            int rows = NumConstraints;
            double[,] bInv = new double[rows, rows];
            for (int i = 0; i < rows; i++)
            {
                int startCol = InitialBasis[i];
                for (int r = 0; r < rows; r++)
                    bInv[r, i] = Tableau[r + 1, startCol];
            }
            return bInv;
        }

        // Shadow prices are the reduced costs of the columns that started as the
        // identity columns, taken from the final objective row (row 0).
        public double[] GetShadowPrices()
        {
            double[] shadow = new double[NumConstraints];
            for (int i = 0; i < NumConstraints; i++)
                shadow[i] = Tableau[0, InitialBasis[i]];
            return shadow;
        }

        public double GetReducedCost(int colIndex) => Tableau[0, colIndex];

        // Row 0's RHS holds the Z value directly, but only if we actually maximised.
        // If the real problem was "min", we secretly solved max(-c.x) instead, so we
        // have to flip the sign back here to get the real answer.
        public double GetOptimalValue()
        {
            double raw = Tableau[0, Tableau.GetLength(1) - 1];
            return IsMax ? raw : -raw;
        }
    }

    // This solves an LP using the normal (non-revised) Primal Simplex method - one
    // big table, updated a bit every pivot, until no more improvement is possible.
    // We use the Big-M method so this one method can handle <=, >= and = constraints
    // all at once, instead of needing separate code for each relation type.
    // Big-M works by giving artificial variables (the "fake" variables we add for
    // >= and = constraints) a huge penalty cost, so simplex naturally tries to push
    // them out of the basis as fast as possible - if it can't, the model was never
    // feasible in the first place.
    public static class PrimalSimplex
    {
        private const double BigM = 1000000; // the "huge penalty" number for artificial variables
        private const double Eps = 1e-9;

        // Kept this simple version so the existing hardcoded test call in
        // MainForm.cs keeps working exactly as before - it assumes every
        // constraint is <= and that the model is a "max" problem.
        public static SimplexResult Solve(double[] c, double[,] A, double[] b)
        {
            var relations = Enumerable.Repeat("<=", A.GetLength(0)).ToArray();
            return Solve(c, A, b, relations, isMax: true);
        }

        // Full version: also takes the relation for every constraint ("<=", ">="
        // or "=") and whether the model is max or min.
        public static SimplexResult Solve(double[] c, double[,] A, double[] b, string[] relations, bool isMax = true)
        {
            int m = A.GetLength(0); // number of constraints
            int n = A.GetLength(1); // number of decision variables

            // Work out how many extra columns we need to add.
            // <=  needs 1 extra column (a slack variable)
            // >=  needs 2 extra columns (a surplus variable AND an artificial variable)
            // =   needs 1 extra column (just an artificial variable)
            int extraCols = 0;
            foreach (var rel in relations)
                extraCols += rel == ">=" ? 2 : 1;

            int totalCols = n + extraCols + 1; // +1 for the RHS column at the end
            int totalRows = m + 1;             // +1 for the objective row at the top

            double[,] T = new double[totalRows, totalCols];
            int[] initialBasis = new int[m];
            var artificialCols = new HashSet<int>(); // remember which columns are "fake" ones

            // We always maximise internally. If the real problem is "min", we flip
            // every objective coefficient and maximise that instead - minimising
            // c.x gives the same answer as maximising -c.x, just with the sign
            // flipped back at the end (see GetOptimalValue above).
            double sign = isMax ? 1 : -1;
            for (int j = 0; j < n; j++)
                T[0, j] = -sign * c[j];

            int col = n; // this tracks where the next extra column should go
            for (int i = 0; i < m; i++)
            {
                // copy this constraint's coefficients into the row, and set its
                // right-hand-side straight away - the Big-M cancellation just
                // below needs the RHS to already be here, not set afterwards
                for (int j = 0; j < n; j++)
                    T[i + 1, j] = A[i, j];
                T[i + 1, totalCols - 1] = b[i];

                if (relations[i] == "<=")
                {
                    // a simple slack variable - it starts basic straight away
                    T[i + 1, col] = 1;
                    initialBasis[i] = col;
                    col++;
                }
                else if (relations[i] == ">=")
                {
                    // subtract a surplus variable first (not basic)
                    T[i + 1, col] = -1;
                    col++;

                    // then add an artificial variable to start the basis with
                    T[i + 1, col] = 1;
                    initialBasis[i] = col;
                    artificialCols.Add(col);

                    // give it the Big-M penalty in the objective row, then cancel
                    // it back out of that row so the tableau is still in proper
                    // canonical form for this new basic variable
                    T[0, col] = BigM;
                    for (int j = 0; j < totalCols; j++)
                        T[0, j] -= BigM * T[i + 1, j];
                    col++;
                }
                else // "="
                {
                    // only needs an artificial variable, no surplus
                    T[i + 1, col] = 1;
                    initialBasis[i] = col;
                    artificialCols.Add(col);

                    T[0, col] = BigM;
                    for (int j = 0; j < totalCols; j++)
                        T[0, j] -= BigM * T[i + 1, j];
                    col++;
                }
            }

            int[] basis = (int[])initialBasis.Clone();
            var iterations = new List<double[,]> { (double[,])T.Clone() }; // save the starting tableau too

            int guard = 0; // just a safety limit so we can never loop forever by accident
            while (guard++ < 500)
            {
                // Entering variable: pick the most negative value in the objective
                // row - that column would improve Z the most if it came in.
                int pivotCol = -1;
                double mostNeg = -Eps;
                for (int j = 0; j < totalCols - 1; j++)
                {
                    if (T[0, j] < mostNeg) { mostNeg = T[0, j]; pivotCol = j; }
                }
                if (pivotCol == -1) break; // nothing negative left - we are optimal

                // Leaving variable: the usual ratio test, smallest positive
                // RHS-divided-by-column-value wins.
                int pivotRow = -1;
                double bestRatio = double.PositiveInfinity;
                for (int i = 1; i < totalRows; i++)
                {
                    if (T[i, pivotCol] > Eps)
                    {
                        double ratio = T[i, totalCols - 1] / T[i, pivotCol];
                        if (ratio < bestRatio - Eps)
                        {
                            bestRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }
                if (pivotRow == -1)
                    throw new InvalidOperationException("Model is unbounded.");

                // Pivot: turn the pivot column into a proper unit column.
                double pivotVal = T[pivotRow, pivotCol];
                for (int j = 0; j < totalCols; j++)
                    T[pivotRow, j] /= pivotVal;

                for (int i = 0; i < totalRows; i++)
                {
                    if (i == pivotRow) continue;
                    double factor = T[i, pivotCol];
                    if (Math.Abs(factor) < 1e-12) continue;
                    for (int j = 0; j < totalCols; j++)
                        T[i, j] -= factor * T[pivotRow, j];
                }

                basis[pivotRow - 1] = pivotCol;
                iterations.Add((double[,])T.Clone()); // save a snapshot after every pivot
            }

            // If an artificial (fake) variable is still stuck in the basis with a
            // value above zero, that means we could never fully get rid of it -
            // which means the model was infeasible from the start.
            for (int i = 0; i < m; i++)
            {
                if (artificialCols.Contains(basis[i]) && T[i + 1, totalCols - 1] > 1e-6)
                    throw new InvalidOperationException("Model is infeasible.");
            }

            return new SimplexResult
            {
                Tableau = T,
                Basis = basis,
                InitialBasis = initialBasis,
                NumOriginalVars = n,
                NumSlacks = extraCols,
                NumConstraints = m,
                CVector = c,
                Iterations = iterations,
                IsMax = isMax
            };
        }

        // Turns a tableau into a plain grid of text, all numbers rounded to 3
        // decimal places (the brief asks for 3 decimal places everywhere).
        public static string TableauToString(double[,] T)
        {
            var sb = new StringBuilder();
            int rows = T.GetLength(0);
            int cols = T.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                    sb.Append(Math.Round(T[i, j], 3).ToString().PadLeft(9));
                sb.AppendLine();
            }
            sb.AppendLine(new string('-', 9 * cols));
            return sb.ToString();
        }
    }
}