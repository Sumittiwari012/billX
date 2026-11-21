namespace MyWPFCRUDApp.Models
{
    public class Store
    {
        public long Id { get; set; }
        public string StoreName { get; set; } = "";
        public long? GodownId { get; set; }
        public string? GodownName { get; set; }   // display only (JOIN)
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
