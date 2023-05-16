using Info21v3.Models.Interfaces;
using Info21v3.Models.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Postgres")!);
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<ILoggerService, LoggerService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Data}/{action=Tables}/{tableName?}");

app.Run();