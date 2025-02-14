using Acceloka.Entities;
using Acceloka.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var licenseType = configuration["QuestPDF:LicenseType"];
QuestPDF.Settings.License = licenseType == "Community" ? LicenseType.Community : LicenseType.Enterprise;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var logFolder = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
if (!Directory.Exists(logFolder))
{
    Directory.CreateDirectory(logFolder);
}

builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);
});

// Configure sql server
builder.Services.AddDbContextPool<AccelokaContext>(options =>
{
    var conString = configuration.GetConnectionString("SQLServerDB");
    options.UseSqlServer(conString);
});

builder.Services.AddTransient<TicketService>();
builder.Services.AddTransient<BookingService>();
builder.Services.AddTransient<BookingReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

Log.Information("Application has started.");

app.Run();
