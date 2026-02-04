using MusicShare.Api.Services;
using MusicShare.MusicAdapters.Services.Extensions;
using MusicShare.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Add services to the container
builder.Services.AddControllers();

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

// Add services
builder.Services.AddScoped<IShareRequestService, ShareRequestService>();
builder.AddMusicServices();

// Configure MassTransit with RabbitMQ
builder.AddMessageAccess(typeof(Program).Assembly);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();
