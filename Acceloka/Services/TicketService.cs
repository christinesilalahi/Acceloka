using Acceloka.Entities;
using Acceloka.Models;
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
    }
}
