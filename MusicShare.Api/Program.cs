using MassTransit;
using MusicShare.Api.Services;
using MusicShare.Contracts;
using MusicShare.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

// Add URL normalizer service
builder.Services.AddSingleton<UrlNormalizer>();

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
