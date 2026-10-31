using System;
using System.Data;
using System.Data.SQLite;

namespace Epipharm.Data
{
    public class AccountRepository
    {
        private readonly string _connectionString;

        public AccountRepository(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;Foreign Keys=True;";
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Returns the account if username/password match, else null.
        /// Does not throw on bad credentials — a wrong username and a wrong
        /// password must look identical to the caller, so the login screen
        /// can't be used to find out which usernames exist.
        /// </summary>
        public Account TryLogin(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, username, legacy_rights, role, password_hash, password_salt, " +
                    "must_change_password FROM accounts WHERE username = @username;";
                cmd.Parameters.AddWithValue("@username", username);

                using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    var storedHash = (byte[])reader["password_hash"];
                    var storedSalt = (byte[])reader["password_salt"];

                    if (!PasswordHasher.Matches(storedHash, password, storedSalt))
                    {
                        return null;
                    }

                    return new Account
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Username = (string)reader["username"],
                        LegacyRights = (string)reader["legacy_rights"],
                        Role = (string)reader["role"],
                        MustChangePassword = Convert.ToInt64(reader["must_change_password"]) != 0
                    };
                }
            }
        }

        public void SetPassword(int accountId, string newPassword)
        {
            var salt = PasswordHasher.NewSalt();
            var hash = PasswordHasher.Hash(newPassword, salt);

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "UPDATE accounts SET password_hash = @hash, password_salt = @salt, " +
                    "pbkdf2_iterations = @iterations, must_change_password = 0 WHERE id = @id;";
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.Parameters.AddWithValue("@salt", salt);
                cmd.Parameters.AddWithValue("@iterations", PasswordHasher.Iterations);
                cmd.Parameters.AddWithValue("@id", accountId);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
