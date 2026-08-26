using System;
using System.IO;
using System.Linq;

namespace LPR381Solver
{
    // This is a small, test-only helper for reading the input .txt file format
    // described in the brief, straight into the plain arrays my Solve() methods
    // expect (c, A, b, relations, signRestrictions). It's here so I can test my
    // own three algorithms with real files while the group's real parser isn't
    // built yet - if someone else adds a proper shared parser later, this can be
    // swapped out for that instead.
    public static class TestFileReader
    {
        public class ParsedModel
        {
            public bool IsMax;
            public double[] C;
            public double[,] A;
            public double[] B;
            public string[] Relations;
            public string[] SignRestrictions;
        }

        public static ParsedModel ReadFromFile(string path)
        {
            // read every line, skip blank ones
            var lines = File.ReadAllLines(path)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToArray();

            // ---- first line: max/min, then the signed objective coefficients ----
            var firstTokens = lines[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool isMax = firstTokens[0].Trim().ToLower() == "max";
            var cTokens = firstTokens.Skip(1).ToArray();
            var c = cTokens.Select(double.Parse).ToArray();
            int numVars = c.Length;

            // ---- middle lines: one constraint per line ----
            int numConstraints = lines.Length - 2; // minus the first line and the last (sign restrictions) line
            var A = new double[numConstraints, numVars];
            var b = new double[numConstraints];
            var relations = new string[numConstraints];

            for (int i = 0; i < numConstraints; i++)
            {
                // work with the raw line text here instead of splitting by space,
                // because the brief's own example writes the relation and the
                // right-hand-side stuck together with no space, like "<=40"
                string line = lines[i + 1].Trim();

                string relation;
                int relPos;
                if (line.Contains("<="))
                {
                    relation = "<=";
                    relPos = line.IndexOf("<=");
                }
                else if (line.Contains(">="))
                {
                    relation = ">=";
                    relPos = line.IndexOf(">=");
                }
                else
                {
                    relation = "=";
                    relPos = line.IndexOf('=');
                }

                string beforeRelation = line.Substring(0, relPos).Trim();
                string afterRelation = line.Substring(relPos + relation.Length).Trim();

                var coeffTokens = beforeRelation.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < numVars; j++)
                    A[i, j] = double.Parse(coeffTokens[j]);

                relations[i] = relation;
                b[i] = double.Parse(afterRelation);
            }

            // ---- last line: sign restrictions ----
            var signRestrictions = lines[lines.Length - 1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return new ParsedModel
            {
                IsMax = isMax,
                C = c,
                A = A,
                B = b,
                Relations = relations,
                SignRestrictions = signRestrictions
            };
        }
    }
}
