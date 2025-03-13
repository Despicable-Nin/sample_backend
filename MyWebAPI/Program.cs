using Application.Interfaces;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Retrieve connection string and flag from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
bool useRawSQL = builder.Configuration.GetValue<bool>("UseRawSQL");

if (!useRawSQL)
{
    // Register EF DbContext when not using raw SQL
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Register repository based on configuration flag
if (useRawSQL)
{
    // For raw SQL, register RepositoryRawSQL as an open generic type.
    // Its constructor will receive IConfiguration automatically.
    builder.Services.AddScoped(typeof(IRepository<>), typeof(RepositoryRawSQL<>));
}
else
{
    builder.Services.AddScoped(typeof(IRepository<>), typeof(RepositoryEF<>));
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Swashbuckle.AspNetCore enables AddSwaggerGen()

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
