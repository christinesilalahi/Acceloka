using Acceloka.Models;
using Acceloka.Services;
using Microsoft.AspNetCore.Mvc;

namespace Acceloka.Controllers
{
    [Route("api/v1/get-available-ticket")]
    [ApiController]
    public class TicketController : ControllerBase
    {
        private readonly TicketService _service;

        public TicketController(TicketService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTickets(
            string? categoryName,
            string? ticketCode,
            string? ticketName,
            decimal? price,
            DateTime? minEventDate,
            DateTime? maxEventDate,
            string? orderBy = "TicketCode",
            string? orderState = "ASC")
        {
            try
            {
                var tickets = await _service.GetAvailableTicket(categoryName, ticketCode, ticketName, price, minEventDate, maxEventDate, orderBy, orderState);

                if (tickets == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = 404,
                        Title = "Not Found",
                        Detail = "No tickets found matching the criteria.",
                        Instance = HttpContext.Request.Path
                    });
                }

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
        }
    }
}
