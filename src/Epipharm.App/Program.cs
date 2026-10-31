using System;
using System.IO;
using System.Windows.Forms;
using Epipharm.Data;

namespace Epipharm.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var dbPath = DbPaths.ResolveDatabasePath();
            EnsureDatabaseExists(dbPath);

            var repo = new AccountRepository(dbPath);

            using (var login = new LoginForm(repo))
            {
                if (login.ShowDialog() != DialogResult.OK || login.LoggedInAccount == null)
                {
                    return; // user cancelled or closed the window
                }

                var account = login.LoggedInAccount;

                if (account.MustChangePassword)
                {
                    using (var change = new ChangePasswordForm(repo, account))
                    {
                        if (change.ShowDialog() != DialogResult.OK)
                        {
                            return; // must set a real password before going further
                        }
                    }
                }

                Application.Run(new MainForm(account, dbPath));
            }
        }

        private static void EnsureDatabaseExists(string dbPath)
        {
            if (File.Exists(dbPath))
            {
                return;
            }

            MessageBox.Show(
                "No database was found at:\n" + dbPath +
                "\n\nRun the schema + seed scripts first (see db/schema.sql, " +
                "db/accounts_seed.sql) and place the resulting epipharm.sqlite " +
                "next to this program, in a 'data' subfolder.",
                "EPIPHARM — database not found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
