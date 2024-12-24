using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

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
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 1. Global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Middleware to validate API key
app.UseMiddleware<ApiKeyMiddleware>();

// 3. Token authentication middleware
app.UseMiddleware<TokenAuthenticationMiddleware>();

// 4. Middleware to log requests and responses
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// User endpoints
app.MapGet("/users", async (IUserService userService) => await userService.GetAllUsersAsync())
   .WithName("GetAllUsers");

app.MapGet("/users/{id}", async (IUserService userService, int id) =>
{
    var user = await userService.GetUserByIdAsync(id);
    if (user == null)
    {
        return Results.NotFound(new { Message = "User not found" });
    }
    return Results.Ok(user);
}).WithName("GetUserById");

app.MapPost("/users", async (IUserService userService, UserDto userDto) =>
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
    return Results.Ok(await userService.AddUserAsync(user));
}).WithName("AddUser");

app.MapPut("/users/{id}", async (IUserService userService, int id, UserDto updatedUserDto) =>
{
    if (string.IsNullOrWhiteSpace(updatedUserDto.Name))
    {
        return Results.BadRequest(new { Message = "User name cannot be empty." });
    }

    if (!IsValidEmail(updatedUserDto.Email))
    {
        return Results.BadRequest(new { Message = "Invalid email address." });
    }

    var updatedUser = new User { Id = updatedUserDto.Id, Name = updatedUserDto.Name, Email = updatedUserDto.Email };
    return Results.Ok(await userService.UpdateUserAsync(id, updatedUser));
}).WithName("UpdateUser");

app.MapDelete("/users/{id}", async (IUserService userService, int id) =>
{
    if (await userService.DeleteUserAsync(id))
    {
        return Results.Ok(new { Message = "User deleted successfully" });
    }
    return Results.NotFound(new { Message = "User not found" });
}).WithName("DeleteUser");

app.Run();

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
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> GetUserByIdAsync(int id);
    Task<User> AddUserAsync(User user);
    Task<User> UpdateUserAsync(int id, User updatedUser);
    Task<bool> DeleteUserAsync(int id);
}

public class UserService : IUserService
{
    private readonly List<User> _users = new List<User>();

    public Task<IEnumerable<User>> GetAllUsersAsync() => Task.FromResult<IEnumerable<User>>(_users);

    public Task<User> GetUserByIdAsync(int id) => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User> AddUserAsync(User user)
    {
        _users.Add(user);
        return Task.FromResult(user);
    }

    public Task<User> UpdateUserAsync(int id, User updatedUser)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;
        }
        return Task.FromResult(user);
    }

    public Task<bool> DeleteUserAsync(int id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _users.Remove(user);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}