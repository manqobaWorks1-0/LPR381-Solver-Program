# LPR381 - Linear & Integer Programming Solver

A menu-driven .NET WinForms application (`solve.exe`) that solves Linear Programming (LP) and Integer Programming (IP) models, displays full canonical forms and tableau iterations, and performs sensitivity analysis on the optimal solution.

**Module:** Linear Programming 381 (LPR381)
**Institution:** Belgium Campus iTversity

---

## 📋 Project Overview

This program reads a mathematical LP/IP model from an input text file, solves it using a chosen algorithm, and writes the canonical form plus every tableau/iteration to an output text file. It also supports a full suite of post-optimal sensitivity analysis and duality operations.

### Core capabilities
- Solve normal max **Linear Programming** models
- Solve **binary Integer Programming** models (Knapsack-style)
- Identify and resolve **infeasible** or **unbounded** models
- *(Bonus)* Solve a simple non-linear problem, e.g. f(x) = x²

### Algorithms
| Algorithm | Requirement |
|---|---|
| Primal Simplex | Display canonical form + all tableau iterations |
| Revised Primal Simplex | Display canonical form + all Product Form / Price Out iterations |
| Branch & Bound Simplex (or Revised) | Backtracking, all sub-problems, all fathomed nodes, all iterations, best candidate shown |
| Cutting Plane (or Revised) | Display canonical form + all Product Form / Price Out iterations |
| Branch & Bound Knapsack | Backtracking, all sub-problems, all fathomed nodes, all iterations, best candidate shown |

### Sensitivity Analysis
- Range and apply-change for a selected **Non-Basic Variable**
- Range and apply-change for a selected **Basic Variable**
- Range and apply-change for a selected **constraint RHS**
- Range and apply-change for a selected **variable in a Non-Basic column**
- Add a new **activity** to an optimal solution
- Add a new **constraint** to an optimal solution
- Display **shadow prices**
- **Duality**: derive the dual model, solve it, and verify strong/weak duality

---

## 📥 Input File Format

Line 1 - objective function:
```
<max|min> <sign><coeff> <sign><coeff> ...
```

One line per constraint:
```
<sign><coeff> <sign><coeff> ... <=|>=|=> <RHS>
```

Final line - sign restrictions (`+`, `-`, `urs`, `int`, `bin`) per decision variable, in order.

### Example (Knapsack IP)
```
max +2 +3 +3 +5 +2 +4
+11 +8 +6 +14 +10 +10 <=40
bin bin bin bin bin bin
```

## 📤 Output File Format
- Canonical form of the model
- Every tableau/iteration of the selected algorithm
- All decimal values rounded to **3 decimal places**

---

## 🛠️ Tech Stack
- **Language:** C# (.NET Framework 4.7.2) - Visual Studio project
- **Output:** `solve.exe`, menu-driven WinForms interface

---

## 📁 Repository Structure
```
LPR381Solver/
├── LPR381Solver/              # Main WinForms solver project
├── Member1Verification/       # Dependency-free Member 1 checks
└── LPR381Solver.slnx          # Visual Studio solution
Examples/                      # Valid sample model files
CONTRIBUTORS.md                # Individual contribution record
```

### Member 1 shared integration contract

- Algorithm modules receive a validated `LinearProgrammingModel` and its `CanonicalForm`.
- Each algorithm implements `IModelSolver` and returns a `SolverExecution` containing a `SolverRunReport`.
- Register completed algorithm adapters in `SolverRegistry.CreateDefault()`; no solver should parse input files or write output files directly.
- The Member 1 workflow owns file selection, validation, canonical display, algorithm selection, error presentation, and output export.

---

## 👥 Team & Work Allocation
member allocation to be determined

| Member | Roll | Focus Area | Responsibilities |
|---|---|---|---|
| Dewald Allers | **1 – Core & Interface** | I/O, UI, error handling | Input file parser, output file writer, canonical form generator, menu-driven console UI, infeasible/unbounded detection & handling, project outline/docs |
| Tshiamo Maise | **2 – Simplex & Cutting Plane** | Primal methods | Primal Simplex, Revised Primal Simplex (Product Form & Price Out), Cutting Plane / Revised Cutting Plane algorithm |
| Daluvuyo Magagane | **3 – Branch & Bound (LP)** | Integer programming | Branch & Bound Simplex (or Revised), backtracking, sub-problem generation, fathoming, best-candidate reporting |
| Manqoba Prosper Siyabonga Mbambo | **4 – Knapsack & Bonus** | Integer programming | Branch & Bound Knapsack algorithm (backtracking, sub-problems, fathoming, best candidate), bonus non-linear solver (f(x)=x²) |
| Rivan Maritz | **5 – Sensitivity Analysis** | Post-optimality | All sensitivity analysis operations (ranging, apply-change, add activity/constraint), shadow prices, duality (dual model, strong/weak duality) |

**Shared responsibilities (all members):**
- Code review of one another's modules before merging
- Contributing to the **video walkthrough** (marks are only awarded for the video, not the code - every member's part should be demoed)
- Reporting any non-contributing member in writing, per the assignment brief

> Tip from the brief: build and demo against the given Knapsack IP example first, then vary values to show every criterion.

---

## ✅ Getting Started
```bash
git clone <repo-url>
cd LPR381-Solver
# Open the .sln in Visual Studio and build
```
Run `solve.exe`, choose an input file, select an algorithm from the menu, and view results in the generated output file.
