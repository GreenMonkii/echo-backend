using SignalRChat.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add CORS policy to allow requests from specified origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                           ?? ["http://localhost:3000"];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add SignalR services to the application
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Use the defined CORS policy
app.UseCors("CorsPolicy");

// Map the root endpoint to return a welcome message
app.MapGet("/", () => "Welcome to the Echo Server! Please use a SignalR client to connect.");

// Add health check endpoint
app.MapHealthChecks("/health");

// Map the SignalR hub endpoint
app.MapHub<ChatHub>("/chat");

// Run the application
app.Run();