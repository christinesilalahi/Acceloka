using Acceloka.Models;
using Acceloka.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Acceloka.Controllers
{
    [Route("api/v1/")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly BookingService _bookingService;

        public BookingController(BookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost("book-ticket")]
        public async Task<IActionResult> BookTickets([FromBody] List<BookTicketRequest> tickets)
        {
            if (tickets == null || !tickets.Any())
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = "Request body cannot be empty.",
                    Instance = HttpContext.Request.Path
                });
            }

            var result = await _bookingService.BookTicketsAsync(tickets);

            if (result is ProblemDetails problem)
            {
                return BadRequest(problem);
            }

            return Ok(result);
        }

        [HttpGet("get-booked-ticket/{bookedTicketId}")]
        public async Task<IActionResult> GetBookedTicketDetails(int bookedTicketId)
        {
            if (bookedTicketId <= 0)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = 400,
                    Title = "Bad Request",
                    Detail = "BookedTicketId must be a positive integer.",
                    Instance = $"/api/v1/get-booked-ticket/{bookedTicketId}"
                };
                return BadRequest(problemDetails);
            }

            var result = await _bookingService.GetBookedTicketDetailsAsync(bookedTicketId);

            if (result is ProblemDetails problemDetailsResult)
            {
                return NotFound(problemDetailsResult);
            }

            return Ok(result);
        }

        [HttpDelete("revoke-ticket/{bookedTicketId}/{ticketCode}/{qty}")]
        public async Task<IActionResult> RevokeTicket(int bookedTicketId, string ticketCode, int qty)
        {
            if (qty <= 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Invalid Quantity",
                    Detail = "The quantity to revoke must be greater than zero.",
                    Instance = $"/api/v1/revoke-ticket/{bookedTicketId}/{ticketCode}/{qty}"
                });
            }

            var result = await _bookingService.RevokeTicketAsync(bookedTicketId, ticketCode, qty);

            if (result is ProblemDetails problemDetailsResult)
            {
                return StatusCode(problemDetailsResult.Status ?? 400, problemDetailsResult);
            }

            return Ok(result);
        }

        [HttpPut("edit-booked-ticket/{bookedTicketId}")]
        public async Task<IActionResult> EditBookedTicket(int bookedTicketId, [FromBody] List<EditBookedTicketRequest> request)
        {
            var result = await _bookingService.EditBookedTicketAsync(bookedTicketId, request);

            if (result is ProblemDetails problemDetails)
            {
                return StatusCode(problemDetails.Status ?? 400, problemDetails);
            }

            return Ok(result);
        }
    }
}
