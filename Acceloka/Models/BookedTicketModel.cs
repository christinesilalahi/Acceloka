namespace Acceloka.Models
{
    public class BookedTicketModel
    {
        public int BookedTicketId { get; set; }
        public int BookingId { get; set; }
        public int TicketId { get; set; }
        public int Quantity { get; set; }
    }
}
