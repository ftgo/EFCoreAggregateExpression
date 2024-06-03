using ConsoleApp1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

await builder.InitializeAsync();

var host = builder.Build();

await host.InitializeAsync();

await host.RunAsync();

public class FooService(FooContext context) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine(JsonSerializer.Serialize(await context.Foos.Skip(5).FirstAsync(cancellationToken: stoppingToken)));

        Console.WriteLine(await context.Foos
            .GroupBy(e1 => e1.IntValue)
            .Select(g => EF.Functions.StandardDeviationSample(g.Select(e2 => e2.DoubleValue)))
            .FirstAsync());

        Console.WriteLine(await context.Foos
            .GroupBy(e1 => e1.IntValue)
            .Select(g => MyStandardDeviationSampleDbFunctionsExtensions.MyStandardDeviationSample(g.Select(e2 => e2.DoubleValue)))
            .FirstAsync());
    }
}

public static class FooExtensions
{
    public static async Task InitializeAsync(this HostApplicationBuilder builder)
    {
        await Task.Run(() =>
        {
            builder.Services.AddDbContext<FooContext>();
            builder.Services.AddScoped<FooInitializer>();
            builder.Services.AddHostedService<FooService>();

            //builder.Services.AddEntityFrameworkNpgsql();
            //builder.Services.AddEntityFrameworkNpgsqlNodaTime();
        });
    }

    public static async Task InitializeAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FooInitializer>().InitializeAsync();
    }
}

public class Foo
{
    [Key]
    public long Id { get; set; }
    public int IntValue { get; set; }
    public double DoubleValue { get; set; }
}

public class FooContext : DbContext
{
    public DbSet<Foo> Foos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .LogTo(Console.WriteLine)
            .EnableSensitiveDataLogging()
            .UseNpgsql("Host=localhost;Database=foodb;Username=admin;Password=password",
                npgsqlBuilder => npgsqlBuilder
                    .UseNodaTime()
                    .AddMyStandardDeviationSampleSupportTranslator());
}

public class FooInitializer
{
    private readonly FooContext _context;

    public FooInitializer(FooContext webappContext)
    {
        _context = webappContext;
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
        await _context.Foos.ExecuteDeleteAsync();

        for (int i = 0; i < 10; i++)
            _context.Foos.Add(new Foo
            {
                IntValue = i % 2,
                DoubleValue = i
            });

        await _context.SaveChangesAsync();
    }
}