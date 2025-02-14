namespace Acceloka.Models
{
    public class BookingReportDTO
    {
        public int BookingId { get; set; }
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string TicketName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime BookingDate { get; set; }
    }
}
