namespace Epipharm.Data
{
    public class Account
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string LegacyRights { get; set; }   // raw 8-char flags, see MENU_MAP.md "unconfirmed"
        public string Role { get; set; }            // "admin" | "user"
        public bool MustChangePassword { get; set; }

        public bool IsAdmin => Role == "admin";
    }
}
