namespace Acceloka.Models
{
    public class TicketModel
    {
        public string EventDate { get; set; } = string.Empty;
        public int Quota { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string TicketName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
