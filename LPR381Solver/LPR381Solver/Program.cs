using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPR381Solver
{
    // Member 1 application startup and global error boundary - Contributor: Dewald Allers
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, args) => ShowFatalError(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                ShowFatalError(args.ExceptionObject as Exception ?? new Exception("An unknown fatal error occurred."));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void ShowFatalError(Exception exception)
        {
            MessageBox.Show(
                "The solver encountered an unexpected error and cannot continue safely.\n\n" + exception.Message,
                "LPR381 Solver Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
