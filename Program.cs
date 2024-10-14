using SignalRChat.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add CORS policy to allow requests from specified origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://echo-anonymous-chat.vercel.app")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add SignalR services to the application
builder.Services.AddSignalR();

var app = builder.Build();

// Use the defined CORS policy
app.UseCors("CorsPolicy");

// Map the root endpoint to return a welcome message
app.MapGet("/", () => "Welcome to the Echo Server! Please use a SignalR client to connect.");

// Map the SignalR hub endpoint
app.MapHub<ChatHub>("/chat");

// Run the application
app.Run();