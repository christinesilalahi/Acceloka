using Acceloka.Models;
using Acceloka.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Acceloka.Controllers
{
    [Route("api/v1/admin/")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly TicketService _ticketService;

        public AdminController(TicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpPost("add-tickets")]
        public async Task<IActionResult> AddTicket([FromBody] AddTicketRequest request)
        {
            var result = await _ticketService.AddTicketAsync(request);
            if (result is ProblemDetails problem)
            {
                return BadRequest(problem);
            }
               
            return Ok(result);
        }

        [HttpDelete("delete-tickets/{ticketId}")]
        public async Task<IActionResult> DeleteTicket(int ticketId)
        {
            var result = await _ticketService.DeleteTicketAsync(ticketId);

            if (result is ProblemDetails problem)
            {
                return BadRequest(problem);
            }

            return Ok(result);
        }

    }

}
