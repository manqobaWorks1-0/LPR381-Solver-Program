using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LPR381Solver
{
    // This solves an LP using the Revised Primal Simplex method.
    // Normal (non-revised) simplex keeps one big table and updates the whole thing
    // every step. This version is different: instead of a big table, we only keep
    // track of one small matrix called B^-1 (the "basis inverse"). Every time we
    // do a pivot, we update B^-1 instead of rebuilding the whole table from scratch.
    // This way of updating B^-1 step by step is called "product form".
    // We use B^-1 each round to check every variable that is NOT currently in the
    // basis, and work out which one should enter the basis next (this step is
    // called "pricing out").
    // Note: right now this version only works for <= constraints, same as the
    // simple version of PrimalSimplex.cs before it was extended. If a test case
    // needs >= or = constraints, this file would need to be extended the same
    // way PrimalSimplex.cs was (adding surplus and artificial columns, plus Big-M)
    // inside the setup code at the top of Solve().
    public static class RevisedPrimalSimplex
    {
        private const double Eps = 1e-9;

        public static double[] Solve(double[] c, double[,] A, double[] b, bool isMax, TextWriter output)
        {
            int numRows = A.GetLength(0);
            int numVars = A.GetLength(1);
            int numSlacks = numRows;

            // Build the full constraint matrix (decision variable columns + slack
            // columns) and the full cost vector.
            var fullA = new double[numRows, numVars + numSlacks];
            var fullC = new double[numVars + numSlacks];
            var basis = new int[numRows];

            // If it's a "min" problem, we flip the sign and solve it as a "max"
            // problem instead, then flip the answer back at the end.
            double sign = isMax ? 1 : -1;
            for (int j = 0; j < numVars; j++)
                fullC[j] = sign * c[j];

            for (int i = 0; i < numRows; i++)
            {
                for (int j = 0; j < numVars; j++)
                    fullA[i, j] = A[i, j];
                fullA[i, numVars + i] = 1; // slack column for this row
                basis[i] = numVars + i;    // slack variables start in the basis
            }

            var Binv = Identity(numRows); // B^-1 starts as the identity matrix
            int iteration = 0;

            output.WriteLine("=== Canonical Form (Revised Primal Simplex) ===");
            PrintState(output, basis, Binv, numVars, numRows);

            while (true)
            {
                // Work out the "prices" (y) using the current B^-1.
                var cB = basis.Select(idx => fullC[idx]).ToArray();
                var y = MultiplyRowVector(cB, Binv, numRows);

                // Check every variable NOT in the basis to see if bringing it in
                // would improve the solution. Pick the one with the best improvement.
                int enter = -1;
                double best = -Eps;
                for (int j = 0; j < numVars + numSlacks; j++)
                {
                    if (basis.Contains(j)) continue;

                    var Aj = GetColumn(fullA, j, numRows);
                    double zj = Dot(y, Aj);
                    double reducedCost = zj - fullC[j];

                    if (reducedCost < best)
                    {
                        best = reducedCost;
                        enter = j;
                    }
                }

                if (enter == -1) break; // no variable can improve things - we're done

                // Work out how far the entering variable can increase before some
                // variable currently in the basis would go negative (the ratio test).
                var enterCol = GetColumn(fullA, enter, numRows);
                var d = Multiply(Binv, enterCol, numRows);
                var xB = Multiply(Binv, b, numRows);

                int leaveRow = -1;
                double bestRatio = double.MaxValue;
                for (int i = 0; i < numRows; i++)
                {
                    if (d[i] > Eps)
                    {
                        double ratio = xB[i] / d[i];
                        if (ratio < bestRatio - Eps)
                        {
                            bestRatio = ratio;
                            leaveRow = i;
                        }
                    }
                }

                if (leaveRow == -1)
                {
                    output.WriteLine("The model is UNBOUNDED - stopping here.");
                    return null;
                }

                // Update B^-1 to reflect the new basis (this is the "product form" step).
                Binv = ApplyEtaUpdate(Binv, d, leaveRow, numRows);
                basis[leaveRow] = enter;

                iteration++;
                output.WriteLine($"=== Iteration {iteration} (entering: {ColName(enter, numVars)}, leaving row {leaveRow + 1}) ===");
                PrintState(output, basis, Binv, numVars, numRows);
            }

            // Read off the final solution from B^-1 and b.
            var finalXB = Multiply(Binv, b, numRows);
            var solution = new double[numVars];
            for (int i = 0; i < numRows; i++)
                if (basis[i] < numVars)
                    solution[basis[i]] = finalXB[i];

            var finalCB = basis.Select(idx => fullC[idx]).ToArray();
            double z = Dot(finalCB, finalXB);
            if (!isMax) z = -z; // flip back if this was actually a "min" problem

            output.WriteLine("=== Optimal Solution ===");
            for (int j = 0; j < numVars; j++)
                output.WriteLine($"x{j + 1} = {Math.Round(solution[j], 3)}");
            output.WriteLine($"Z = {Math.Round(z, 3)}");

            return solution;
        }

        // Updates B^-1 for the new basis without rebuilding it from scratch.
        // This "eta update" is what makes the product form approach fast - we only
        // adjust B^-1 using the pivot column (d) and the row that left the basis.
        private static double[,] ApplyEtaUpdate(double[,] Binv, double[] d, int pivotRow, int n)
        {
            var newBinv = new double[n, n];
            double pivotVal = d[pivotRow];

            // Scale the pivot row so the pivot value becomes 1.
            for (int j = 0; j < n; j++)
                newBinv[pivotRow, j] = Binv[pivotRow, j] / pivotVal;

            // Adjust every other row so its entry in the pivot column becomes 0.
            for (int i = 0; i < n; i++)
            {
                if (i == pivotRow) continue;
                for (int j = 0; j < n; j++)
                    newBinv[i, j] = Binv[i, j] - d[i] * newBinv[pivotRow, j];
            }

            return newBinv;
        }

        private static double[,] Identity(int n)
        {
            var m = new double[n, n];
            for (int i = 0; i < n; i++) m[i, i] = 1;
            return m;
        }

        // Multiplies a matrix by a column vector (m * v).
        private static double[] Multiply(double[,] m, double[] v, int n)
        {
            var result = new double[n];
            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int j = 0; j < n; j++) sum += m[i, j] * v[j];
                result[i] = sum;
            }
            return result;
        }

        // Multiplies a row vector by a matrix (v * m). Used to get the "prices" (y).
        private static double[] MultiplyRowVector(double[] v, double[,] m, int n)
        {
            var result = new double[n];
            for (int j = 0; j < n; j++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++) sum += v[i] * m[i, j];
                result[j] = sum;
            }
            return result;
        }

        // Pulls column j out of matrix m as its own array.
        private static double[] GetColumn(double[,] m, int j, int numRows)
        {
            var col = new double[numRows];
            for (int i = 0; i < numRows; i++) col[i] = m[i, j];
            return col;
        }

        private static double Dot(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
            return sum;
        }

        // Turns a column index into a readable name, e.g. x1, x2, s1, s2.
        private static string ColName(int j, int numVars) => j < numVars ? $"x{j + 1}" : $"s{j - numVars + 1}";

        // Prints the current basis and B^-1 so every iteration can be shown in the output.
        private static void PrintState(TextWriter output, int[] basis, double[,] Binv, int numVars, int numRows)
        {
            output.WriteLine("Current basis: " + string.Join(", ", basis.Select(j => ColName(j, numVars))));
            output.WriteLine("B^-1 (product form):");
            for (int i = 0; i < numRows; i++)
            {
                var rowVals = new List<string>();
                for (int j = 0; j < numRows; j++)
                    rowVals.Add(Math.Round(Binv[i, j], 3).ToString());
                output.WriteLine("  " + string.Join("\t", rowVals));
            }
            output.WriteLine();
        }
    }
}
