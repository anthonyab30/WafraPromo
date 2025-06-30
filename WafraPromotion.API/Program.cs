using Microsoft.EntityFrameworkCore;
using WafraPromotion.API.Data;
using WafraPromotion.API.Services; // Moved to top

var builder = WebApplication.CreateBuilder(args);

using Microsoft.AspNetCore.Identity; // Added for Identity

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseInMemoryDatabase("WafraPromotionDb"));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => {
    // Configure identity options here if needed (e.g., password complexity)
    options.SignIn.RequireConfirmedAccount = false; // For simplicity, disable email confirmation
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6; // Simple password for demo
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI(); // Adds default Identity UI pages like Login, Register

builder.Services.AddScoped<ImageProcessingService>(); // Register the service
builder.Services.AddControllers();
builder.Services.AddRazorPages(); // Add support for Razor Pages
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

using WafraPromotion.API.Middleware; // For ApiExceptionMiddleware

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ApiExceptionMiddleware>(); // Add global API exception handler

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // Good for Razor Pages debugging
}
else
{
    // For Razor Pages, you might want a custom error page for production
    // app.UseExceptionHandler("/Error");
    // app.UseHsts(); // If using HTTPS strictly
}

using Microsoft.Extensions.FileProviders; // For PhysicalFileProvider

app.UseHttpsRedirection();

app.UseStaticFiles(); // For serving static files from wwwroot
app.UseStaticFiles(new StaticFileOptions // For serving uploaded images
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "UploadedImages")),
    RequestPath = "/UploadedImages"
});

app.UseRouting(); // Routing middleware

app.UseAuthentication(); // Add Authentication middleware (before Authorization)
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages(); // Map Razor Page endpoints

// Seed admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<Program>>(); // Get logger for Program
        await WafraPromotion.API.Data.SeedData.InitializeAdminUser(services, configuration, logger);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
