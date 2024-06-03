using ConsoleApp1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Initialize();

var host = builder.Build();

host.Initialize();

//await host.RunAsync();
host.Execute();

public class FooService(FooContext context) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await Task.Run(Execute);

    public void Execute()
    {
        Console.WriteLine(JsonSerializer.Serialize(context.Foos.Skip(5).First()));

        Console.WriteLine(context.Foos
            .GroupBy(e1 => e1.IntValue)
            .Select(g => EF.Functions.StandardDeviationSample(g.Select(e2 => e2.DoubleValue)))
            .First());

        Console.WriteLine(context.Foos
            .GroupBy(e1 => e1.IntValue)
            .Select(g => MyStandardDeviationSampleDbFunctionsExtensions.MyStandardDeviationSample(g.Select(e2 => e2.DoubleValue)))
            .First());
    }
}

public static class FooExtensions
{
    public static void Initialize(this HostApplicationBuilder builder)
    {
            builder.Services.AddDbContext<FooContext>();
            builder.Services.AddScoped<FooInitializer>();
            builder.Services.AddHostedService<FooService>();

            //builder.Services.AddEntityFrameworkNpgsql();
            //builder.Services.AddEntityFrameworkNpgsqlNodaTime();
    }

    public static void Initialize(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FooInitializer>().Initialize();
    }

    public static void Execute(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetService<FooService>()!.Execute();
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

    public void Initialize()
    {
        _context.Database.EnsureCreated();
        _context.Foos.ExecuteDelete();

        for (int i = 0; i < 10; i++)
            _context.Foos.Add(new Foo
            {
                IntValue = i % 2,
                DoubleValue = i
            });

        _context.SaveChanges();
    }
}