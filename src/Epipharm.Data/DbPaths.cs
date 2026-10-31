using System;
using System.IO;

namespace Epipharm.Data
{
    /// <summary>
    /// Resolves where epipharm.sqlite lives.
    ///
    /// IMPORTANT — unresolved architecture question, flagged here rather than
    /// silently defaulted: SQLite is a single local file. If this hospital
    /// pharmacy runs on more than one PC (the legacy app's RESEAUX/Reseau
    /// folders suggest it did — a shared-network setup existed), several
    /// people opening the same .sqlite file concurrently over a network
    /// share is unreliable and can corrupt the database. Before rollout,
    /// confirm how many PCs need write access at once:
    ///   - one PC only -> local file is fine, nothing to change.
    ///   - several PCs -> this needs a real server backend (e.g. the same
    ///     schema in SQL Server Express / PostgreSQL, or an API server this
    ///     app talks to) before go-live, not a network-shared .sqlite file.
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
