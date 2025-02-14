using System;
using System.Collections.Generic;

namespace Acceloka.Entities;

public partial class Ticket
{
    public int TicketId { get; set; }

    public int CategoryId { get; set; }

    public string TicketCode { get; set; } = null!;

    public string TicketName { get; set; } = null!;

    public DateTime EventDate { get; set; }

    public decimal Price { get; set; }

    public int Quota { get; set; }

    public virtual ICollection<BookedTicket> BookedTickets { get; set; } = new List<BookedTicket>();

    public virtual Category Category { get; set; } = null!;
}
