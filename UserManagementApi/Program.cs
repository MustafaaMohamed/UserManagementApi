
using System.Text.Json;
using System.Text.RegularExpressions;
namespace UserManagementApi
{
	public static class MiddlewareExtensions
	{
		public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
		{
			return app.UseMiddleware<ErrorHandlingMiddleware>();
		}

		public static IApplicationBuilder UseTokenAuthentication(this IApplicationBuilder app)
		{
			return app.UseMiddleware<TokenAuthenticationMiddleware>();
		}

		public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
		{
			return app.UseMiddleware<RequestResponseLoggingMiddleware>();
		}
	}

	public class ErrorHandlingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ErrorHandlingMiddleware> _logger;

		public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task Invoke(HttpContext context)
		{
			try
			{
				await _next(context);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unhandled exception occurred.");
				context.Response.StatusCode = 500;
				context.Response.ContentType = "application/json";
				var errorResponse = new { error = "Internal server error." };
				await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
			}
		}
	}

	public class TokenAuthenticationMiddleware
	{
		private readonly RequestDelegate _next;

		public TokenAuthenticationMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task Invoke(HttpContext context)
		{
			var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
			// Replace with your token validation logic (demo: token == "valid-token")
			if (string.IsNullOrEmpty(token) || token != "valid-token")
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				context.Response.ContentType = "application/json";
				var errorResponse = new { error = "Unauthorized" };
				await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
				return;
			}
			await _next(context);
		}
	}

	public class RequestResponseLoggingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

		public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task Invoke(HttpContext context)
		{
			// Log request
			var requestMethod = context.Request.Method;
			var requestPath = context.Request.Path;
			_logger.LogInformation("Incoming Request: {Method} {Path}", requestMethod, requestPath);

			// Capture response status code after next middleware runs
			await _next(context);

			var responseStatusCode = context.Response.StatusCode;
			_logger.LogInformation("Outgoing Response: {StatusCode} for {Method} {Path}", responseStatusCode, requestMethod, requestPath);
		}
	}
	public record User
	{
		public int Id { get; set; }
		public string Name { get; set; } = "";
		public string Email { get; set; } = "";
		public string? Details { get; set; }
	}
	public class Program
    {
        public static void Main(string[] args)
        {
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();
			builder.Services.AddLogging();

			var app = builder.Build();

			app.UseErrorHandling();
			app.UseTokenAuthentication();      
			app.UseRequestResponseLogging();   

											   // In-memory user store
			var users = new List<User>();
			var nextId = 1;

			app.UseSwagger();
			app.UseSwaggerUI();

			// Email validation regex for demonstration
			bool IsValidEmail(string email) =>
				!string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

			app.MapGet("/users", (int? page, int? pageSize) =>
			{
				try
				{
					// Pagination for performance
					int currentPage = page ?? 1;
					int currentPageSize = pageSize ?? 10;
					var pagedUsers = users
						.Skip((currentPage - 1) * currentPageSize)
						.Take(currentPageSize)
						.ToList();
					return Results.Ok(pagedUsers);
				}
				catch (Exception ex)
				{
					return Results.Problem($"Unexpected error: {ex.Message}");
				}
			});

			app.MapGet("/users/{id:int}", (int id) =>
			{
				try
				{
					var user = users.FirstOrDefault(u => u.Id == id);
					return user is not null ? Results.Ok(user) : Results.NotFound();
				}
				catch (Exception ex)
				{
					return Results.Problem($"Unexpected error: {ex.Message}");
				}
			});

			app.MapPost("/users", (User user) =>
			{
				try
				{
					if (string.IsNullOrWhiteSpace(user.Name))
						return Results.BadRequest("Name is required.");
					if (!IsValidEmail(user.Email))
						return Results.BadRequest("Invalid email format.");

					user.Id = nextId++;
					users.Add(user);
					return Results.Created($"/users/{user.Id}", user);
				}
				catch (Exception ex)
				{
					return Results.Problem($"Unexpected error: {ex.Message}");
				}
			});

			app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
			{
				try
				{
					var user = users.FirstOrDefault(u => u.Id == id);
					if (user is null) return Results.NotFound();

					if (string.IsNullOrWhiteSpace(updatedUser.Name))
						return Results.BadRequest("Name is required.");
					if (!IsValidEmail(updatedUser.Email))
						return Results.BadRequest("Invalid email format.");

					user.Name = updatedUser.Name;
					user.Email = updatedUser.Email;
					user.Details = updatedUser.Details;
					return Results.Ok(user);
				}
				catch (Exception ex)
				{
					return Results.Problem($"Unexpected error: {ex.Message}");
				}
			});

			app.MapDelete("/users/{id:int}", (int id) =>
			{
				try
				{
					var user = users.FirstOrDefault(u => u.Id == id);
					if (user is null) return Results.NotFound();
					users.Remove(user);
					return Results.NoContent();
				}
				catch (Exception ex)
				{
					return Results.Problem($"Unexpected error: {ex.Message}");
				}
			});

			app.Run();
        }
	}
}
