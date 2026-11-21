namespace MyWPFCRUDApp.Models
{
    public class Godown
    {
        public long Id { get; set; }
        public string GodownName { get; set; } = "";
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
