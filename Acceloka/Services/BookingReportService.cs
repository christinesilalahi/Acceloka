using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using Microsoft.EntityFrameworkCore;
using Acceloka.Entities;
using Acceloka.Models;

public class BookingReportService
{
    private readonly AccelokaContext _db;
    private readonly ILogger<BookingReportService> _logger;

    public BookingReportService(AccelokaContext db, ILogger<BookingReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<BookingReportDTO>> GetBookingReportDataAsync()
    {
        try
        {
            _logger.LogInformation("Mengambil data booking dari database...");

            var data = await _db.Bookings
                .Join(_db.BookedTickets,
                    b => b.BookingId,
                    bt => bt.BookingId,
                    (b, bt) => new { b, bt })
                .Join(_db.Tickets,
                    bt => bt.bt.TicketId,
                    t => t.TicketId,
                    (bt, t) => new BookingReportDTO
                    {
                        BookingId = bt.b.BookingId,
                        TicketId = t.TicketId,
                        TicketCode = t.TicketCode,
                        TicketName = t.TicketName,
                        EventDate = t.EventDate,
                        Quantity = bt.bt.Quantity,
                        TotalPrice = t.Price * bt.bt.Quantity,
                        BookingDate = bt.b.BookingDate
                    })
                .ToListAsync();

            _logger.LogInformation("Berhasil mengambil {Count} data booking.", data.Count);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal mengambil data booking dari database.");
            throw;
        }
    }

    public async Task<byte[]> GeneratePdfReportAsync()
    {
        try
        {
            _logger.LogInformation("Memulai pembuatan laporan PDF...");

            var bookings = await GetBookingReportDataAsync();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Header().Text("Booking Report").FontSize(20).SemiBold().AlignCenter();
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.ConstantColumn(50);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("ID").SemiBold();
                            header.Cell().Text("Ticket Code").SemiBold();
                            header.Cell().Text("Ticket Name").SemiBold();
                            header.Cell().Text("Event Date").SemiBold();
                            header.Cell().Text("Qty").SemiBold();
                            header.Cell().Text("Total Price").SemiBold();
                            header.Cell().Text("Booking Date").SemiBold();
                        });

                        foreach (var b in bookings)
                        {
                            table.Cell().Text(b.BookingId.ToString());
                            table.Cell().Text(b.TicketCode);
                            table.Cell().Text(b.TicketName);
                            table.Cell().Text(b.EventDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture));
                            table.Cell().Text(b.Quantity.ToString());
                            table.Cell().Text(b.TotalPrice.ToString("N2"));
                            table.Cell().Text(b.BookingDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture));
                        }
                    });
                });
            });

            _logger.LogInformation("Laporan PDF berhasil dibuat.");
            return document.GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membuat laporan PDF.");
            throw;
        }
    }

    public async Task<byte[]> GenerateExcelReportAsync()
    {
        try
        {
            _logger.LogInformation("Memulai pembuatan laporan Excel...");

            var bookings = await GetBookingReportDataAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Booking Report");

            worksheet.Cell(1, 1).Value = "Booking ID";
            worksheet.Cell(1, 2).Value = "Ticket ID";
            worksheet.Cell(1, 3).Value = "Ticket Code";
            worksheet.Cell(1, 4).Value = "Ticket Name";
            worksheet.Cell(1, 5).Value = "Event Date";
            worksheet.Cell(1, 6).Value = "Quantity";
            worksheet.Cell(1, 7).Value = "Total Price";
            worksheet.Cell(1, 8).Value = "Booking Date";

            for (int i = 0; i < bookings.Count; i++)
            {
                var b = bookings[i];
                worksheet.Cell(i + 2, 1).Value = b.BookingId;
                worksheet.Cell(i + 2, 2).Value = b.TicketId;
                worksheet.Cell(i + 2, 3).Value = b.TicketCode;
                worksheet.Cell(i + 2, 4).Value = b.TicketName;
                worksheet.Cell(i + 2, 5).Value = b.EventDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
                worksheet.Cell(i + 2, 6).Value = b.Quantity;
                worksheet.Cell(i + 2, 7).Value = b.TotalPrice;
                worksheet.Cell(i + 2, 8).Value = b.BookingDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            _logger.LogInformation("Laporan Excel berhasil dibuat.");
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membuat laporan Excel.");
            throw;
        }
    }
}
