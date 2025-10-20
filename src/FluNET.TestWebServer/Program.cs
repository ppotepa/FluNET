var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on a specific port for testing
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8765);
});

builder.Services.AddControllers();

var app = builder.Build();

// Enable static files serving from wwwroot
app.UseStaticFiles();

app.MapControllers();

app.Run();
