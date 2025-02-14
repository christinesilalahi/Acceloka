using Acceloka.Entities;
using Acceloka.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Acceloka.Services
{
    public class BookingService
    {
        private readonly AccelokaContext _db;
        private readonly ILogger<TicketService> _logger;

        public BookingService(AccelokaContext db, ILogger<TicketService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<object> BookTicketsAsync(List<BookTicketRequest> tickets)
        {
            _logger.LogInformation("Starting ticket booking process for {Count} tickets.", tickets.Count);

            var errors = new List<string>();
            var bookedTickets = new List<BookedTicketModel>();

            foreach (var item in tickets)
            {
                _logger.LogInformation("Processing ticket with code {TicketCode}.", item.TicketCode);

                var ticket = await _db.Tickets
                    .Include(t => t.Category)
                    .FirstOrDefaultAsync(t => t.TicketCode == item.TicketCode);

                if (ticket == null)
                {
                    errors.Add($"Ticket code '{item.TicketCode}' is not registered.");
                    _logger.LogWarning("Ticket code {TicketCode} is not registered.", item.TicketCode);
                    continue;
                }

                if (ticket.Quota <= 0)
                {
                    errors.Add($"Ticket code '{item.TicketCode}' is sold out.");
                    _logger.LogWarning("Ticket code {TicketCode} is sold out.", item.TicketCode);
                    continue;
                }

                if (item.Quantity > ticket.Quota)
                {
                    errors.Add($"Ticket code '{item.TicketCode}' exceeds available quota.");
                    _logger.LogWarning("Ticket code {TicketCode} exceeds available quota.", item.TicketCode);
                    continue;
                }

                if (ticket.EventDate <= DateTime.UtcNow)
                {
                    errors.Add($"Ticket code '{item.TicketCode}' event date has passed.");
                    _logger.LogWarning("Ticket code {TicketCode} event date has passed.", item.TicketCode);
                    continue;
                }

                ticket.Quota -= item.Quantity;
                _db.Tickets.Update(ticket);
                _logger.LogInformation("Updated quota for ticket {TicketCode} to {Quota}.", ticket.TicketCode, ticket.Quota);

                bookedTickets.Add(new BookedTicketModel
                {
                    TicketId = ticket.TicketId,
                    Quantity = item.Quantity
                });
            }

            if (errors.Any())
            {
                _logger.LogError("Errors encountered during ticket booking: {Errors}", string.Join(", ", errors));
                return new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = "Some errors occurred while processing the request.",
                    Instance = "/api/v1/book-ticket",
                    Extensions = { { "errors", errors } }
                };
            }

            var newBooking = new Booking { BookingDate = DateTime.Now };
            await _db.Bookings.AddAsync(newBooking);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Created new booking with ID {BookingId}.", newBooking.BookingId);

            foreach (var bt in bookedTickets)
            {
                bt.BookingId = newBooking.BookingId;
            }

            var bookedTicketEntities = bookedTickets.Select(bt => new BookedTicket
            {
                BookingId = bt.BookingId,
                TicketId = bt.TicketId,
                Quantity = bt.Quantity
            }).ToList();

            await _db.BookedTickets.AddRangeAsync(bookedTicketEntities);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Saved {Count} booked tickets to the database.", bookedTickets.Count);

            var ticketData = await _db.Tickets
                .Where(t => bookedTickets.Select(bt => bt.TicketId).Contains(t.TicketId))
                .Select(t => new
                {
                    t.TicketId,
                    t.TicketCode,
                    t.TicketName,
                    t.Price,
                    t.Category.CategoryName
                }).ToListAsync();

            var groupedTickets = ticketData
                .GroupBy(t => t.CategoryName)
                .Select(g => new
                {
                    categoryName = g.Key,
                    summaryPrice = g.Sum(x => x.Price),
                    tickets = g.Select(t => new
                    {
                        ticketCode = t.TicketCode,
                        ticketName = t.TicketName,
                        price = t.Price
                    }).ToList()
                })
                .ToList();

            var totalPrice = groupedTickets.Sum(g => g.summaryPrice);
            _logger.LogInformation("Booking process completed successfully. Total price: {TotalPrice}", totalPrice);

            return new
            {
                priceSummary = totalPrice,
                ticketsPerCategories = groupedTickets
            };
        }

        public async Task<object> GetBookedTicketDetailsAsync(int bookedTicketId)
        {
            _logger.LogInformation("Fetching details for booked ticket ID: {BookedTicketId}", bookedTicketId);

            bool exists = await _db.BookedTickets.AnyAsync(bt => bt.BookedTicketId == bookedTicketId);
            if (!exists)
            {
                _logger.LogWarning("Booked ticket ID {BookedTicketId} not found.", bookedTicketId);
                return new ProblemDetails
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = $"BookedTicketId '{bookedTicketId}' is not registered.",
                    Instance = $"/api/v1/get-booked-ticket/{bookedTicketId}"
                };
            }

            _logger.LogInformation("Booked ticket ID {BookedTicketId} found. Fetching ticket details.", bookedTicketId);

            var bookedTickets = await _db.BookedTickets
                .Where(bt => bt.BookedTicketId == bookedTicketId)
                .Include(bt => bt.Ticket)
                    .ThenInclude(t => t.Category)
                .Select(bt => new
                {
                    bt.Ticket.TicketCode,
                    bt.Ticket.TicketName,
                    bt.Ticket.EventDate,
                    bt.Quantity,
                    bt.Ticket.Category.CategoryName
                })
                .ToListAsync();

            _logger.LogInformation("Fetched {Count} ticket(s) for booked ticket ID {BookedTicketId}.", bookedTickets.Count, bookedTicketId);

            var groupedTickets = bookedTickets
                .GroupBy(bt => bt.CategoryName)
                .Select(g => new
                {
                    categoryName = g.Key,
                    qtyPerCategory = g.Sum(x => x.Quantity),
                    tickets = g.Select(bt => new
                    {
                        ticketCode = bt.TicketCode,
                        ticketName = bt.TicketName,
                        eventDate = bt.EventDate.ToString("dd-MM-yyyy HH:mm")
                    }).ToList()
                })
                .ToList();

            _logger.LogInformation("Successfully grouped booked tickets for booked ticket ID {BookedTicketId}.", bookedTicketId);

            return groupedTickets;
        }


        public async Task<object> RevokeTicketAsync(int bookedTicketId, string ticketCode, int qty)
        {
            _logger.LogInformation("[RevokeTicket] Request received for BookedTicketId: {BookedTicketId}, TicketCode: {TicketCode}, Quantity: {Qty}", bookedTicketId, ticketCode, qty);

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var bookedTicket = await _db.BookedTickets
                    .Include(bt => bt.Ticket)
                    .ThenInclude(t => t.Category)
                    .FirstOrDefaultAsync(bt => bt.BookedTicketId == bookedTicketId && bt.Ticket.TicketCode == ticketCode);

                if (bookedTicket == null)
                {
                    _logger.LogWarning("[RevokeTicket] BookedTicketId '{BookedTicketId}' with TicketCode '{TicketCode}' not found.", bookedTicketId, ticketCode);
                    return new ProblemDetails
                    {
                        Status = 404,
                        Title = "Not Found",
                        Detail = $"BookedTicketId '{bookedTicketId}' with TicketCode '{ticketCode}' not found.",
                        Instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{qty}"
                    };
                }

                if (qty > bookedTicket.Quantity)
                {
                    _logger.LogWarning("[RevokeTicket] Requested quantity ({Qty}) exceeds booked quantity ({BookedQuantity}) for TicketCode '{TicketCode}'.", qty, bookedTicket.Quantity, ticketCode);
                    return new ProblemDetails
                    {
                        Status = 400,
                        Title = "Bad Request",
                        Detail = $"Cannot revoke {qty} tickets. Only {bookedTicket.Quantity} are booked.",
                        Instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{qty}"
                    };
                }

                bookedTicket.Quantity -= qty;
                _logger.LogInformation("[RevokeTicket] Reduced booked quantity by {Qty} for TicketCode '{TicketCode}'. Remaining quantity: {RemainingQty}", qty, ticketCode, bookedTicket.Quantity);

                if (bookedTicket.Ticket != null)
                {
                    bookedTicket.Ticket.Quota += qty;
                    _logger.LogInformation("[RevokeTicket] Restored {Qty} tickets to TicketCode '{TicketCode}'. New quota: {NewQuota}", qty, ticketCode, bookedTicket.Ticket.Quota);
                }

                if (bookedTicket.Quantity == 0)
                {
                    _logger.LogInformation("[RevokeTicket] No remaining tickets for BookedTicketId '{BookedTicketId}'. Removing from database.", bookedTicketId);
                    _db.BookedTickets.Remove(bookedTicket);
                }

                await _db.SaveChangesAsync();

                var remainingTickets = await _db.BookedTickets
                    .Where(bt => bt.BookingId == bookedTicket.BookingId)
                    .ToListAsync();

                if (!remainingTickets.Any())
                {
                    var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookedTicket.BookingId);
                    if (booking != null)
                    {
                        _logger.LogInformation("[RevokeTicket] No remaining tickets for BookingId '{BookingId}'. Removing booking record.", bookedTicket.BookingId);
                        _db.Bookings.Remove(booking);
                        await _db.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation("[RevokeTicket] Remaining tickets after revocation: {RemainingTickets}", remainingTickets.Count);

                return remainingTickets.Select(bt => new
                {
                    ticketCode = bt.Ticket.TicketCode,
                    ticketName = bt.Ticket.TicketName,
                    quantity = bt.Quantity,
                    categoryName = bt.Ticket.Category.CategoryName
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RevokeTicket] An error occurred while revoking ticket.");
                await transaction.RollbackAsync();
                return new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = "An error occurred while processing your request.",
                    Instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{qty}"
                };
            }
        }

        public async Task<object> EditBookedTicketAsync(int bookedTicketId, List<EditBookedTicketRequest> tickets)
        {
            _logger.LogInformation("Received request to edit booked tickets for BookedTicketId: {BookedTicketId}", bookedTicketId);

            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var bookedTickets = await _db.BookedTickets
                        .Include(bt => bt.Ticket)
                        .ThenInclude(t => t.Category)
                        .Where(bt => bt.BookedTicketId == bookedTicketId)
                        .ToListAsync();

                    if (!bookedTickets.Any())
                    {
                        _logger.LogWarning("BookedTicketId '{BookedTicketId}' not found.", bookedTicketId);
                        return new ProblemDetails
                        {
                            Status = 404,
                            Title = "Not Found",
                            Detail = $"BookedTicketId '{bookedTicketId}' not found.",
                            Instance = $"/api/v1/edit-booked-ticket/{bookedTicketId}"
                        };
                    }

                    var responseList = new List<object>();

                    foreach (var ticketRequest in tickets)
                    {
                        _logger.LogInformation("Processing TicketCode '{TicketCode}' for BookedTicketId '{BookedTicketId}'", ticketRequest.TicketCode, bookedTicketId);

                        var bookedTicket = bookedTickets.FirstOrDefault(bt => bt.Ticket.TicketCode == ticketRequest.TicketCode);

                        if (bookedTicket == null)
                        {
                            _logger.LogWarning("TicketCode '{TicketCode}' not found in BookedTicketId '{BookedTicketId}'.", ticketRequest.TicketCode, bookedTicketId);
                            return new ProblemDetails
                            {
                                Status = 404,
                                Title = "Not Found",
                                Detail = $"TicketCode '{ticketRequest.TicketCode}' not found in BookedTicket.",
                                Instance = $"/api/v1/edit-booked-ticket/{bookedTicketId}"
                            };
                        }

                        if (ticketRequest.Quantity < 1)
                        {
                            _logger.LogWarning("Invalid quantity '{Quantity}' for TicketCode '{TicketCode}' in BookedTicketId '{BookedTicketId}'.", ticketRequest.Quantity, ticketRequest.TicketCode, bookedTicketId);
                            return new ProblemDetails
                            {
                                Status = 400,
                                Title = "Bad Request",
                                Detail = $"Quantity for TicketCode '{ticketRequest.TicketCode}' must be at least 1.",
                                Instance = $"/api/v1/edit-booked-ticket/{bookedTicketId}"
                            };
                        }

                        var latestTicket = await _db.Tickets
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.TicketCode == ticketRequest.TicketCode);

                        if (latestTicket == null)
                        {
                            _logger.LogWarning("TicketCode '{TicketCode}' not found in Tickets table.", ticketRequest.TicketCode);
                            return new ProblemDetails
                            {
                                Status = 404,
                                Title = "Not Found",
                                Detail = $"TicketCode '{ticketRequest.TicketCode}' not found in Tickets table.",
                                Instance = $"/api/v1/edit-booked-ticket/{bookedTicketId}"
                            };
                        }

                        int latestQuota = latestTicket.Quota;
                        int availableQuota = latestQuota;

                        if (ticketRequest.Quantity > availableQuota)
                        {
                            _logger.LogWarning("Requested quantity '{Quantity}' for TicketCode '{TicketCode}' exceeds available quota '{AvailableQuota}' in BookedTicketId '{BookedTicketId}'.",
                                ticketRequest.Quantity, ticketRequest.TicketCode, availableQuota, bookedTicketId);

                            return new ProblemDetails
                            {
                                Status = 400,
                                Title = "Bad Request",
                                Detail = $"Quantity for TicketCode '{ticketRequest.TicketCode}' exceeds available quota ({availableQuota}).",
                                Instance = $"/api/v1/edit-booked-ticket/{bookedTicketId}"
                            };
                        }

                        bookedTicket.Quantity = ticketRequest.Quantity;

                        latestTicket.Quota = availableQuota - ticketRequest.Quantity;
                        _db.Tickets.Update(latestTicket);

                        _logger.LogInformation("Updated TicketCode '{TicketCode}': New Qty = {NewQty}, Updated Quota = {UpdatedQuota}",
                            ticketRequest.TicketCode, bookedTicket.Quantity, latestTicket.Quota);

                        responseList.Add(new
                        {
                            ticketCode = latestTicket.TicketCode,
                            ticketName = latestTicket.TicketName,
                            quantity = bookedTicket.Quantity,
                            categoryName = latestTicket.Category.CategoryName
                        });
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully updated booked tickets for BookedTicketId '{BookedTicketId}'.", bookedTicketId);
                    return responseList;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Transaction failed: {Message}", ex.Message);
                    return new ProblemDetails
                    {
                        Status = 500,
                        Title = "Internal Server Error",
                        Detail = "An error occurred while processing your request."
                    };
                }
            }
        }

    }
}
