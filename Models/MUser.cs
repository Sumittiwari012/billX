namespace MyWPFCRUDApp.Models
{
    public class MUser: BaseEntity
    {
        public long Id { get; set; }
        public string UserName { get; set; }
        public long UserTypeId { get; set; }

        public long MobileNumber { get; set; }

        // Display-only, populated via JOIN in GetUsers(); not written back to DB directly
        public string UserTypeName { get; set; }
    }
}