using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IUserService, UserService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Middleware to validate API key
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var extractedApiKey))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("API Key was not provided.");
        return;
    }

    var apiKey = builder.Configuration["ApiKey"];

    if (!apiKey.Equals(extractedApiKey))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized client.");
        return;
    }

    await next();
});

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

app.MapPost("/users", (IUserService userService, User user) => userService.AddUser(user))
   .WithName("AddUser");

app.MapPut("/users/{id}", (IUserService userService, int id, User updatedUser) => userService.UpdateUser(id, updatedUser))
   .WithName("UpdateUser");

app.MapDelete("/users/{id}", (IUserService userService, int id) => userService.DeleteUser(id))
   .WithName("DeleteUser");

app.Run();

class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

interface IUserService
{
    IEnumerable<User> GetAllUsers();
    User GetUserById(int id);
    User AddUser(User user);
    User UpdateUser(int id, User updatedUser);
    bool DeleteUser(int id);
}

class UserService : IUserService
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