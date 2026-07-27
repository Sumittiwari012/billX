namespace MyWPFCRUDApp.Models
{
    public class MCounterUser
    {
        public long Id { get; set; }
        public long CounterId { get; set; }
        public long UserId { get; set; }
        public string Password { get; set; }

        // Joined display-only field, not a DB column.
        public string UserName { get; set; }
    }
}
