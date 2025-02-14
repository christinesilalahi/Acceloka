using Acceloka.Entities;
using Acceloka.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Acceloka.Services
{
    public class TicketService
    {
        private readonly AccelokaContext _db;
        private readonly ILogger<TicketService> _logger;

        public TicketService(AccelokaContext db, ILogger<TicketService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<object> GetAvailableTicket(
                string? categoryName,
                string? ticketCode,
                string? ticketName,
                decimal? price,
                DateTime? minEventDate,
                DateTime? maxEventDate,
                string? orderBy = "TicketCode",
                string? orderState = "ASC",
                int page = 1,
                int pageSize = 10)
        {
            _logger.LogInformation("Fetching available tickets with filters: CategoryName={CategoryName}, TicketCode={TicketCode}, TicketName={TicketName}, Price={Price}, MinEventDate={MinEventDate}, MaxEventDate={MaxEventDate}, OrderBy={OrderBy}, OrderState={OrderState}, Page={Page}, PageSize={PageSize}",
                categoryName, ticketCode, ticketName, price, minEventDate, maxEventDate, orderBy, orderState, page, pageSize);

            var query = _db.Tickets
                .Where(t => t.Quota > 0)
                .Join(_db.Categories,
                    ticket => ticket.CategoryId,
                    category => category.CategoryId,
                    (ticket, category) => new TicketModel
                    {
                        TicketCode = ticket.TicketCode,
                        TicketName = ticket.TicketName,
                        CategoryName = category.CategoryName,
                        EventDate = ticket.EventDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
                        Price = ticket.Price,
                        Quota = ticket.Quota
                    }).AsQueryable();

            if (!string.IsNullOrEmpty(categoryName))
            {
                _logger.LogInformation("Filtering by CategoryName: {CategoryName}", categoryName);
                query = query.Where(q => q.CategoryName.Contains(categoryName));
            }

            if (!string.IsNullOrEmpty(ticketCode))
            {
                _logger.LogInformation("Filtering by TicketCode: {TicketCode}", ticketCode);
                query = query.Where(q => q.TicketCode.Contains(ticketCode));
            }

            if (!string.IsNullOrEmpty(ticketName))
            {
                _logger.LogInformation("Filtering by TicketName: {TicketName}", ticketName);
                query = query.Where(q => q.TicketName.Contains(ticketName));
            }

            if (price.HasValue)
            {
                _logger.LogInformation("Filtering by Price: {Price}", price);
                query = query.Where(q => q.Price <= price.Value);
            }

            if (minEventDate.HasValue)
            {
                _logger.LogInformation("Filtering by MinEventDate: {MinEventDate}", minEventDate);
                query = query.Where(q => DateTime.ParseExact(q.EventDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) >= minEventDate.Value);
            }

            if (maxEventDate.HasValue)
            {
                _logger.LogInformation("Filtering by MaxEventDate: {MaxEventDate}", maxEventDate);
                query = query.Where(q => DateTime.ParseExact(q.EventDate, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) <= maxEventDate.Value);
            }

            bool isDescending = orderState?.ToUpper() == "DESC";
            _logger.LogInformation("Sorting by {OrderBy} in {OrderState} order", orderBy, orderState);

            query = orderBy?.ToLower() switch
            {
                "eventdate" => isDescending ? query.OrderByDescending(q => q.EventDate) : query.OrderBy(q => q.EventDate),
                "quota" => isDescending ? query.OrderByDescending(q => q.Quota) : query.OrderBy(q => q.Quota),
                "ticketcode" => isDescending ? query.OrderByDescending(q => q.TicketCode) : query.OrderBy(q => q.TicketCode),
                "ticketname" => isDescending ? query.OrderByDescending(q => q.TicketName) : query.OrderBy(q => q.TicketName),
                "categoryname" => isDescending ? query.OrderByDescending(q => q.CategoryName) : query.OrderBy(q => q.CategoryName),
                "price" => isDescending ? query.OrderByDescending(q => q.Price) : query.OrderBy(q => q.Price),
                _ => query.OrderBy(q => q.TicketCode)
            };

            int totalTickets = await query.CountAsync();
            _logger.LogInformation("Total tickets found: {TotalTickets}", totalTickets);

            var tickets = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            _logger.LogInformation("Returning {TicketCount} tickets", tickets.Count);

            return new
            {
                tickets,
                totalTickets
            };
        }

        public async Task<object> AddTicketAsync(AddTicketRequest request)
        {
            try
            {
                _logger.LogInformation("Admin is adding a new ticket: {TicketName}", request.TicketName);

                var category = await _db.Categories.FindAsync(request.CategoryId);
                if (category == null)
                {
                    _logger.LogWarning("CategoryId '{CategoryId}' not found.", request.CategoryId);
                    return new ProblemDetails
                    {
                        Status = 400,
                        Title = "Bad Request",
                        Detail = $"CategoryId '{request.CategoryId}' not found.",
                        Instance = "/api/v1/admin/tickets"
                    };
                }

                bool ticketExists = await _db.Tickets.AnyAsync(t => t.TicketCode == request.TicketCode);
                if (ticketExists)
                {
                    _logger.LogWarning("TicketCode '{TicketCode}' already exists.", request.TicketCode);
                    return new ProblemDetails
                    {
                        Status = 400,
                        Title = "Bad Request",
                        Detail = $"TicketCode '{request.TicketCode}' already exists.",
                        Instance = "/api/v1/admin/tickets"
                    };
                }

                if (!DateTime.TryParseExact(request.EventDate, "dd-MM-yyyy HH:mm",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out DateTime parsedEventDate))
                {
                    return new ProblemDetails
                    {
                        Status = 400,
                        Title = "Bad Request",
                        Detail = "Invalid date format. Use 'dd-MM-yyyy HH:mm' (e.g., 01-02-2026 13:00).",
                        Instance = "/api/v1/admin/tickets"
                    };
                }

                if (request.Price <= 0)
                {
                    return new ProblemDetails
                    {
                        Status = 400,
                        Title = "Bad Request",
                        Detail = "Price must be greater than 0.",
                        Instance = "/api/v1/admin/tickets"
                    };
                }

                if (request.Quota <= 0)
                {
                    return new ProblemDetails
                    {
                        Status = 400,
                        Title = "Bad Request",
                        Detail = "Quota must be greater than 0.",
                        Instance = "/api/v1/admin/tickets"
                    };
                }

                var ticket = new Ticket
                {
                    CategoryId = request.CategoryId,
                    TicketCode = request.TicketCode,
                    TicketName = request.TicketName,
                    EventDate = parsedEventDate,
                    Price = request.Price,
                    Quota = request.Quota
                };

                await _db.Tickets.AddAsync(ticket);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Successfully added ticket: {TicketName}", request.TicketName);
                return new
                {
                    message = "Ticket added successfully",
                    ticketId = ticket.TicketId,
                    categoryId = ticket.CategoryId,
                    ticketCode = ticket.TicketCode,
                    ticketName = ticket.TicketName,
                    EventDate = parsedEventDate,
                    price = ticket.Price,
                    quota = ticket.Quota
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while adding a ticket.");

                return new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Instance = "/api/v1/admin/tickets"
                };
            }
        }

        public async Task<object> DeleteTicketAsync(int ticketId)
        {
            try
            {
                _logger.LogInformation("Admin is attempting to delete ticket with ID: {TicketId}", ticketId);

                var ticket = await _db.Tickets.FindAsync(ticketId);
                if (ticket == null)
                {
                    _logger.LogWarning("TicketId '{TicketId}' not found.", ticketId);
                    return new ProblemDetails
                    {
                        Status = 404,
                        Title = "Not Found",
                        Detail = $"TicketId '{ticketId}' not found.",
                        Instance = $"/api/v1/admin/tickets/{ticketId}"
                    };
                }

                _db.Tickets.Remove(ticket);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted ticket with ID: {TicketId}", ticketId);
                return new
                {
                    message = "Ticket deleted successfully",
                    ticketId = ticketId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting the ticket.");

                return new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Instance = $"/api/v1/admin/tickets/{ticketId}"
                };
            }
        }

    }
}
