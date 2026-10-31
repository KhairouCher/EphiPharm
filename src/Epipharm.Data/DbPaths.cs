using System;
using System.IO;

namespace Epipharm.Data
{
    /// <summary>
    /// Resolves where epipharm.sqlite lives.
    ///
    /// Confirmed with the hospital: a single PC for now, so a local SQLite
    /// file is the right call and nothing more is needed here. If a second
    /// PC is ever added, re-open this: concurrent writers over a network
    /// share are unreliable with SQLite, and it would need a real server
    /// backend instead (e.g. this same schema in SQL Server Express /
    /// PostgreSQL, or an API server this app talks to).
    /// </summary>
    public static class DbPaths
    {
        public static string ResolveDatabasePath()
        {
            var overridePath = Environment.GetEnvironmentVariable("EPIPHARM_DB_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return overridePath;
            }

            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(exeDir, "data");
            Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, "epipharm.sqlite");
        }
    }
}
