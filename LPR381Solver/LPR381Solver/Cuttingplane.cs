using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LPR381Solver
{
    // This solves an Integer Programming model using the Cutting Plane algorithm.
    // The idea in plain terms:
    //   1. Solve the model as a normal LP first, ignoring the "must be a whole
    //      number" requirement. This is called the "relaxed" LP.
    //   2. Look at the answer. If every variable that needs to be an integer
    //      already came out as a whole number, we're done.
    //   3. If not, pick one variable that came out fractional (e.g. x2 = 3.4) and
    //      build a new constraint (a "Gomory cut") straight from that variable's
    //      row in the tableau. This cut removes the fractional answer we just
    //      found, but it's built in a way that can never remove a genuine whole
    //      number answer, so nothing valid is ever lost.
    //   4. Solve again with the new constraint added, and repeat from step 2.
    // We reuse PrimalSimplex.Solve() to actually solve each relaxed LP - this
    // class is really just a loop around that, plus the logic for picking a row
    // and building the cut from it.
    public static class CuttingPlane
    {
        private const double Eps = 1e-6;
        private const int MaxCuts = 25; // a safety limit so we can't loop forever if something goes wrong

        // signRestrictions holds one entry per decision variable, e.g. "int",
        // "bin", "+", "-" or "urs" - in the same order as the objective function.
        // Only variables marked "int" or "bin" are forced to come out as whole
        // numbers here; the rest are allowed to be fractional.
        public static double[] Solve(double[] c, double[,] A, double[] b, string[] relations,
            string[] signRestrictions, bool isMax, TextWriter output)
        {
            // We copy the constraints into lists instead of fixed-size arrays,
            // because every cut we add means one more row - lists make that easy.
            int n = c.Length;
            var rows = new List<double[]>();
            for (int i = 0; i < A.GetLength(0); i++)
            {
                var row = new double[n];
                for (int j = 0; j < n; j++) row[j] = A[i, j];
                rows.Add(row);
            }
            var rhs = b.ToList();
            var rels = relations.ToList();

            int cutCount = 0;
            while (true)
            {
                output.WriteLine($"=== Cutting Plane: solving relaxed LP (cut {cutCount}) ===");

                double[,] currentA = RowsToMatrix(rows, n);

                SimplexResult result;
                try
                {
                    result = PrimalSimplex.Solve(c, currentA, rhs.ToArray(), rels.ToArray(), isMax);
                }
                catch (Exception ex)
                {
                    output.WriteLine($"Could not solve the relaxed LP: {ex.Message}");
                    return null;
                }

                double[] solution = ExtractSolution(result, n);
                output.WriteLine(PrimalSimplex.TableauToString(result.Tableau));

                // Find a row in the tableau whose basic variable needs to be a
                // whole number but currently isn't.
                int fracRow = FindFractionalRow(result, n, signRestrictions);
                if (fracRow == -1)
                {
                    output.WriteLine("=== All integer-restricted variables are whole numbers - done ===");
                    for (int j = 0; j < n; j++)
                        output.WriteLine($"x{j + 1} = {Math.Round(solution[j], 3)}");
                    output.WriteLine($"Z = {Math.Round(result.GetOptimalValue(), 3)}");
                    return solution;
                }

                if (cutCount >= MaxCuts)
                {
                    output.WriteLine("Reached the maximum number of cuts without an integer answer - stopping.");
                    return solution;
                }

                // ---- Build the Gomory cut from this row ----
                // Every number in a row can be split into a whole part and a
                // fraction part, e.g. 3.75 = 3 + 0.75. The Gomory cut takes just
                // the fraction parts of the row (for the original x1..xn columns)
                // and says: these fractions, added up, must be at least the
                // fraction part of the row's own right-hand-side. Any true whole
                // number answer already satisfies this automatically, but the
                // fractional point we just found does not - so this new
                // constraint removes exactly that bad point and nothing else.
                var cutRow = new double[n];
                int rhsCol = result.Tableau.GetLength(1) - 1;
                for (int j = 0; j < n; j++)
                    cutRow[j] = FractionPart(result.Tableau[fracRow, j]);
                double cutRhs = FractionPart(result.Tableau[fracRow, rhsCol]);

                rows.Add(cutRow);
                rhs.Add(cutRhs);
                rels.Add(">=");

                cutCount++;
                int cutVarCol = result.Basis[fracRow - 1];
                string cutVarName = cutVarCol < n ? $"x{cutVarCol + 1}" : $"column {cutVarCol}";
                output.WriteLine($"Added Gomory cut #{cutCount}, built from the row for {cutVarName} " +
                    $"(its value was {Math.Round(result.Tableau[fracRow, rhsCol], 3)})");
                output.WriteLine();
            }
        }

        // Turns our list-of-rows back into a normal 2D array, since that's what
        // PrimalSimplex.Solve() expects.
        private static double[,] RowsToMatrix(List<double[]> rows, int n)
        {
            var matrix = new double[rows.Count, n];
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < n; j++)
                    matrix[i, j] = rows[i][j];
            return matrix;
        }

        // Reads the x-values back out of the final tableau, using the basis to
        // know which row holds the value for each variable.
        private static double[] ExtractSolution(SimplexResult result, int n)
        {
            var solution = new double[n];
            int rhsCol = result.Tableau.GetLength(1) - 1;
            for (int i = 0; i < result.Basis.Length; i++)
            {
                int col = result.Basis[i];
                if (col < n)
                    solution[col] = result.Tableau[i + 1, rhsCol];
            }
            return solution;
        }

        // Looks through every row of the final tableau for one whose basic
        // variable (a) is one of our original decision variables, (b) is
        // supposed to be a whole number, and (c) currently isn't one. Returns
        // the tableau row index (remember row 0 is the objective row, so
        // constraint rows start at index 1), or -1 if nothing fractional is left.
        private static int FindFractionalRow(SimplexResult result, int n, string[] signRestrictions)
        {
            int rhsCol = result.Tableau.GetLength(1) - 1;
            for (int i = 0; i < result.Basis.Length; i++)
            {
                int col = result.Basis[i];
                if (col >= n) continue; // this row's basic variable is a slack/artificial, skip it

                bool mustBeInteger = signRestrictions != null && signRestrictions.Length > col &&
                    (signRestrictions[col] == "int" || signRestrictions[col] == "bin");
                if (!mustBeInteger) continue;

                double value = result.Tableau[i + 1, rhsCol];
                double frac = FractionPart(value);
                if (frac > Eps && frac < 1 - Eps)
                    return i + 1; // +1 because row 0 is the objective row
            }
            return -1;
        }

        // The "true" fractional part of a number - always comes out between 0
        // and 1, even for negative numbers (e.g. FractionPart(-1.3) = 0.7,
        // because -1.3 = -2 + 0.7). This is what the Gomory cut formula needs.
        private static double FractionPart(double x) => x - Math.Floor(x);
    }
}
