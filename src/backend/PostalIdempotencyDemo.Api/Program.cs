using Serilog;
using PostalIdempotencyDemo.Api.Data;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services;
using PostalIdempotencyDemo.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register database and repositories
builder.Services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

// Register services
builder.Services.AddScoped<PostalIdempotencyDemo.Api.Services.Interfaces.IShipmentService, PostalIdempotencyDemo.Api.Services.ShipmentService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
