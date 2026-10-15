using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductManagement.Models;
using ProductManagement.Models.Repositories;

namespace ProductManagement
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContextPool<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("ProductDBConnection")
                ));

            // ASP.NET Identity
            builder.Services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();

            builder.Services.Configure<IdentityOptions>(options =>
            {
                // Default Password settings.
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            });

            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            builder.Services.AddScoped<ICategorieRepository, CategoryRepository>();
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<ProductManagement.Services.IRecommendationService, ProductManagement.Services.RecommendationService>();
            builder.Services.AddScoped<ProductManagement.Services.IChatbotService, ProductManagement.Services.ChatbotService>();

            var app = builder.Build();

            // Seed database with categories, products with images and sample orders
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<AppDbContext>();
                    DbInitializer.Seed(context);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Une erreur est survenue lors de l'initialisation de la base de données.");
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            // Authentication + Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}