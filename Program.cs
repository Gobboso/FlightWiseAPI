using FlightWiseAPI.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace FlightWiseAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHttpClient<GeminiAIService>();
            builder.Services.AddHttpClient<FlightsService>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("frontend", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("Fixed", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 3; // Aumentar a 10
                    limiterOptions.Window = TimeSpan.FromSeconds(10); // Aumentar a 10 segundos
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0; // No queuing, reject immediately
                });
            });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // ✅ CORS PRIMERO (antes que otros middleware)
            app.UseCors("frontend");

            // app.UseHttpsRedirection(); // Deshabilitado temporalmente para desarrollo
            app.UseRouting();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapControllers();

            app.Run();
        }
    }
}