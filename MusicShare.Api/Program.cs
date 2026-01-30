using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.MusicAdapters.Services.Extensions;
using MusicShare.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add persistence layer
builder.AddPersistence();

// Add services
builder.Services.AddScoped<IShareRequestService, ShareRequestService>();
builder.AddMusicServices();

// Configure MassTransit with RabbitMQ
builder.AddMessageAccess(typeof(Program).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();
