using System.ComponentModel.DataAnnotations;

namespace Acceloka.Models
{
    public class AddTicketRequest
    {
        [Required]
        public int CategoryId { get; set; }
        [Required]
        public string TicketCode { get; set; } = string.Empty;
        [Required]
        public string TicketName { get; set; } = string.Empty;
        [Required]
        public string EventDate { get; set; } = string.Empty;
        [Required]
        public decimal Price { get; set; }
        [Required]
        public int Quota { get; set; }
    }
}
