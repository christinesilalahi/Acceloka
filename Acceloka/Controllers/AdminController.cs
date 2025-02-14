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
        private readonly BookingReportService _bookingReportService;

        public AdminController(TicketService ticketService, BookingReportService bookingReportService)
        {
            _ticketService = ticketService;
            _bookingReportService = bookingReportService;
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

        [HttpGet("data-report")]
        public async Task<IActionResult> GetBookingReportData()
        {
            var data = await _bookingReportService.GetBookingReportDataAsync();
            return Ok(new { message = "Booking data retrieved successfully.", data });
        }

        [HttpGet("pdf-report")]
        public async Task<IActionResult> DownloadPdfReport()
        {
            var pdfBytes = await _bookingReportService.GeneratePdfReportAsync();
            return File(pdfBytes, "application/pdf", "BookingReportAcceloka.pdf");
        }

        [HttpGet("excel-report")]
        public async Task<IActionResult> DownloadExcelReport()
        {
            var excelBytes = await _bookingReportService.GenerateExcelReportAsync();
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BookingReportAcceloka.xlsx");
        }

    }

}
