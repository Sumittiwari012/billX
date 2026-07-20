namespace MyWPFCRUDApp.Models
{
    public class MCounter: BaseEntity
    {
        public long Id { get; set; }
        public string CounterName { get; set; }
        public long UserId { get; set; }
        public string Password { get; set; }
        
        // Display-only, populated via JOIN in GetCounters(); not written back to DB directly
        public string UserName { get; set; }
    }
}