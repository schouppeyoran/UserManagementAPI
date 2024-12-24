using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// General use method to validate email address
bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch
    {
        return false;
    }
}

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Middleware to validate API key
app.UseMiddleware<ApiKeyMiddleware>();

// Global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// User endpoints
app.MapGet("/users", (IUserService userService) => userService.GetAllUsers())
   .WithName("GetAllUsers");

app.MapGet("/users/{id}", (IUserService userService, int id) =>
{
    var user = userService.GetUserById(id);
    if (user == null)
    {
        return Results.NotFound(new { Message = "User not found" });
    }
    return Results.Ok(user);
}).WithName("GetUserById");

app.MapPost("/users", (IUserService userService, UserDto userDto) =>
{
    if (string.IsNullOrWhiteSpace(userDto.Name))
    {
        return Results.BadRequest(new { Message = "User name cannot be empty." });
    }

    if (!IsValidEmail(userDto.Email))
    {
        return Results.BadRequest(new { Message = "Invalid email address." });
    }

    var user = new User { Id = userDto.Id, Name = userDto.Name, Email = userDto.Email };
    return Results.Ok(userService.AddUser(user));
}).WithName("AddUser");

app.MapPut("/users/{id}", (IUserService userService, int id, UserDto updatedUserDto) =>
{
    var updatedUser = new User { Id = updatedUserDto.Id, Name = updatedUserDto.Name, Email = updatedUserDto.Email };
    return Results.Ok(userService.UpdateUser(id, updatedUser));
}).WithName("UpdateUser");

app.MapDelete("/users/{id}", (IUserService userService, int id) =>
{
    if (userService.DeleteUser(id))
    {
        return Results.Ok(new { Message = "User deleted successfully" });
    }
    return Results.NotFound(new { Message = "User not found" });
}).WithName("DeleteUser");

app.Run();

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-API-KEY", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key was not provided.");
            return;
        }

        var apiKey = _configuration["ApiKey"];

        if (!apiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized client.");
            return;
        }

        await _next(context);
    }
}

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("An unexpected error occurred. Please try again later.");
        }
    }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public interface IUserService
{
    IEnumerable<User> GetAllUsers();
    User GetUserById(int id);
    User AddUser(User user);
    User UpdateUser(int id, User updatedUser);
    bool DeleteUser(int id);
}

public class UserService : IUserService
{
    private readonly List<User> _users = new List<User>();

    public IEnumerable<User> GetAllUsers() => _users;

    public User GetUserById(int id) => _users.FirstOrDefault(u => u.Id == id);

    public User AddUser(User user)
    {
        _users.Add(user);
        return user;
    }

    public User UpdateUser(int id, User updatedUser)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;
        }
        return user;
    }

    public bool DeleteUser(int id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _users.Remove(user);
            return true;
        }
        return false;
    }
}