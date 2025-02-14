using System;
using System.Collections.Generic;

namespace Acceloka.Entities;

public partial class BookedTicket
{
    public int BookedTicketId { get; set; }

    public int BookingId { get; set; }

    public int TicketId { get; set; }

    public int Quantity { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Ticket Ticket { get; set; } = null!;
}
