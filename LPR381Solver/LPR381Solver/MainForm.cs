using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPR381Solver
{

    /// <summary>
    /// All of the below code is AI generated for the sake of testing the sensitivity analysis.
    /// The 3 new buttons and their click methods at the bottom were added to test
    /// Primal Simplex, Revised Primal Simplex, and Cutting Plane using the test .txt files.
    /// </summary>
    public class MainForm : Form
    {
        private Button runButton;
        private ComboBox iterationSelector;
        private DataGridView tableauGrid;
        private DataGridView bInverseGrid;
        private DataGridView shadowPriceGrid;
        private Label optimalValueLabel;

        // ---- new buttons for testing Member 2's algorithms ----
        private Button testPrimalButton;
        private Button testRevisedButton;
        private Button testCuttingButton;


        private static readonly string TestFilesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles");

        private SimplexResult currentResult;

        public MainForm()
        {
            Text = "LPR381 Solver - Sensitivity Test Harness";
            Width = 1000;
            Height = 700;

            runButton = new Button
            {
                Text = "Run Primal Simplex (test model)",
                Left = 10,
                Top = 10,
                Width = 220,
                Height = 30
            };
            runButton.Click += RunButton_Click;

            // ---- new test buttons, placed in a row under the original button ----
            testPrimalButton = new Button
            {
                Text = "Test: Primal Simplex (file)",
                Left = 10,
                Top = 610,
                Width = 220,
                Height = 30
            };
            testPrimalButton.Click += TestPrimalSimplex_Click;

            testRevisedButton = new Button
            {
                Text = "Test: Revised Simplex (file)",
                Left = 240,
                Top = 610,
                Width = 220,
                Height = 30
            };
            testRevisedButton.Click += TestRevisedSimplex_Click;

            testCuttingButton = new Button
            {
                Text = "Test: Cutting Plane (file)",
                Left = 470,
                Top = 610,
                Width = 220,
                Height = 30
            };
            testCuttingButton.Click += TestCuttingPlane_Click;

            iterationSelector = new ComboBox
            {
                Left = 240,
                Top = 12,
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            iterationSelector.SelectedIndexChanged += IterationSelector_SelectedIndexChanged;

            tableauGrid = new DataGridView
            {
                Left = 10,
                Top = 50,
                Width = 960,
                Height = 300,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersWidth = 60,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var bInverseLabel = new Label { Left = 10, Top = 360, Width = 200, Text = "B-inverse:" };
            bInverseGrid = new DataGridView
            {
                Left = 10,
                Top = 380,
                Width = 300,
                Height = 150,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false
            };

            var shadowLabel = new Label { Left = 330, Top = 360, Width = 200, Text = "Shadow Prices:" };
            shadowPriceGrid = new DataGridView
            {
                Left = 330,
                Top = 380,
                Width = 300,
                Height = 150,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false
            };

            optimalValueLabel = new Label
            {
                Left = 10,
                Top = 550,
                Width = 400,
                Height = 25,
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                Text = "Optimal Value: -"
            };

            Controls.Add(runButton);
            Controls.Add(iterationSelector);
            Controls.Add(tableauGrid);
            Controls.Add(bInverseLabel);
            Controls.Add(bInverseGrid);
            Controls.Add(shadowLabel);
            Controls.Add(shadowPriceGrid);
            Controls.Add(optimalValueLabel);

            // ---- add the new test buttons to the form ----
            Controls.Add(testPrimalButton);
            Controls.Add(testRevisedButton);
            Controls.Add(testCuttingButton);

            // the form was only tall enough for the original controls - make it
            // a bit taller so the new buttons at Top=610 are actually visible
            Height = 700;
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            try
            {
                // ---- Hardcoded test model (swap for the real parser's output later) ----
                // max 3x1 + 5x2
                // s.t. x1        <= 4
                //           2x2  <= 12
                //      3x1 + 2x2 <= 18
                double[] c = { 3, 5 };
                double[,] A =
                {
                    { 1, 0 },
                    { 0, 2 },
                    { 3, 2 }
                };
                double[] b = { 4, 12, 18 };

                currentResult = PrimalSimplex.Solve(c, A, b);

                iterationSelector.Items.Clear();
                for (int i = 0; i < currentResult.Iterations.Count; i++)
                    iterationSelector.Items.Add($"Iteration {i}");
                iterationSelector.Enabled = true;
                iterationSelector.SelectedIndex = currentResult.Iterations.Count - 1; // show final by default

                PopulateBInverseGrid();
                PopulateShadowPriceGrid();
                optimalValueLabel.Text = $"Optimal Value: {Math.Round(currentResult.GetOptimalValue(), 3)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Solve failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================== NEW: test buttons for Member 2's algorithms ==================

        // Loads test2_mixed_relations.txt (has <=, >= and = together) and solves it
        // with the normal Primal Simplex, then shows the final tableau and Z value.
        private void TestPrimalSimplex_Click(object sender, EventArgs e)
        {
            try
            {
                string path = Path.Combine(TestFilesFolder, "test2_mixed_relations.txt");
                var model = TestFileReader.ReadFromFile(path);

                var result = PrimalSimplex.Solve(model.C, model.A, model.B, model.Relations, model.IsMax);

                string tableauText = PrimalSimplex.TableauToString(result.Tableau);
                string zText = $"Z = {Math.Round(result.GetOptimalValue(), 3)}";
                MessageBox.Show(tableauText + "\n" + zText, "Primal Simplex test result");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Test failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Loads test1_simple_le.txt (plain <= only) and solves it with Revised
        // Primal Simplex, showing every iteration's B-inverse in a popup.
        private void TestRevisedSimplex_Click(object sender, EventArgs e)
        {
            try
            {
                string path = Path.Combine(TestFilesFolder, "test1_simple_le.txt");
                var model = TestFileReader.ReadFromFile(path);

                using (var writer = new StringWriter())
                {
                    RevisedPrimalSimplex.Solve(model.C, model.A, model.B, model.IsMax, writer);
                    MessageBox.Show(writer.ToString(), "Revised Primal Simplex test result");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Test failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Loads test3_integer_cuttingplane.txt (both variables must be int) and
        // solves it with Cutting Plane, showing every cut it added along the way.
        private void TestCuttingPlane_Click(object sender, EventArgs e)
        {
            try
            {
                string path = Path.Combine(TestFilesFolder, "test3_integer_cuttingplane.txt");
                var model = TestFileReader.ReadFromFile(path);

                using (var writer = new StringWriter())
                {
                    CuttingPlane.Solve(model.C, model.A, model.B, model.Relations,
                        model.SignRestrictions, model.IsMax, writer);
                    MessageBox.Show(writer.ToString(), "Cutting Plane test result");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Test failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================== end of new test buttons ==================

        private void IterationSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (currentResult == null || iterationSelector.SelectedIndex < 0) return;
            var tableau = currentResult.Iterations[iterationSelector.SelectedIndex];
            PopulateTableauGrid(tableau);
        }

        private void PopulateTableauGrid(double[,] tableau)
        {
            tableauGrid.Columns.Clear();
            tableauGrid.Rows.Clear();

            int n = currentResult.NumOriginalVars;
            int m = currentResult.NumConstraints;
            int rows = tableau.GetLength(0);
            int cols = tableau.GetLength(1);

            // Column headers: x1..xn, s1..sm, RHS
            for (int j = 0; j < cols; j++)
            {
                string header;
                if (j < n) header = $"x{j + 1}";
                else if (j < n + m) header = $"s{j - n + 1}";
                else header = "RHS";
                tableauGrid.Columns.Add($"col{j}", header);
            }

            // Rows: row 0 = Z, rows 1..m = constraints (labelled by current basic var if known)
            for (int i = 0; i < rows; i++)
            {
                var values = new object[cols];
                for (int j = 0; j < cols; j++)
                    values[j] = Math.Round(tableau[i, j], 3);
                int rowIdx = tableauGrid.Rows.Add(values);

                if (i == 0)
                {
                    tableauGrid.Rows[rowIdx].HeaderCell.Value = "Z";
                }
                else
                {
                    int basicCol = currentResult.Basis[i - 1];
                    string label = basicCol < n ? $"x{basicCol + 1}" : $"s{basicCol - n + 1}";
                    tableauGrid.Rows[rowIdx].HeaderCell.Value = label;
                }
            }
        }

        private void PopulateBInverseGrid()
        {
            bInverseGrid.Columns.Clear();
            bInverseGrid.Rows.Clear();

            var bInv = currentResult.GetBInverse();
            int size = currentResult.NumConstraints;

            for (int j = 0; j < size; j++)
                bInverseGrid.Columns.Add($"b{j}", $"Col {j + 1}");

            for (int i = 0; i < size; i++)
            {
                var values = new object[size];
                for (int j = 0; j < size; j++)
                    values[j] = Math.Round(bInv[i, j], 3);
                bInverseGrid.Rows.Add(values);
            }
        }

        private void PopulateShadowPriceGrid()
        {
            shadowPriceGrid.Columns.Clear();
            shadowPriceGrid.Rows.Clear();

            shadowPriceGrid.Columns.Add("constraint", "Constraint");
            shadowPriceGrid.Columns.Add("price", "Shadow Price");

            var shadow = currentResult.GetShadowPrices();
            for (int i = 0; i < shadow.Length; i++)
                shadowPriceGrid.Rows.Add($"Constraint {i + 1}", Math.Round(shadow[i], 3));
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(1007, 660);
            this.Name = "MainForm";
            this.Text = "Main Form";
            this.ResumeLayout(false);

        }
    }
}
