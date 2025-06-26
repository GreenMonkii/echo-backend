using SignalRChat.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for production deployments behind load balancers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Cache preflight for 10 minutes
    });
});

// Add SignalR services to the application
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();

    if (builder.Environment.IsProduction())
    {
        // Shorter intervals for production to handle load balancers and proxies
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    }
    else
    {
        // Development settings
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    }

    // Enable detailed errors only in development
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();

    // Maximum message size
    options.MaximumReceiveMessageSize = 32 * 1024;
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Use forwarded headers middleware
app.UseForwardedHeaders();

// WebSockets configuration
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10),
    AllowedOrigins = { "*" }
});

// Use the defined CORS policy
app.UseCors("CorsPolicy");

// Map the root endpoint to return a welcome message
app.MapGet("/", () => "Welcome to the Echo Server! Please use a SignalR client to connect.");

// Add health check endpoint
app.MapHealthChecks("/health");

// Map the SignalR hub endpoint
app.MapHub<ChatHub>("/chat");

app.Run();