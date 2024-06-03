using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConsoleApp1;

public class MyStandardDeviationSampleDbFunctionsExtensions
{
    public static MethodInfo MyStandardDeviationSampleMethod => typeof(MyStandardDeviationSampleDbFunctionsExtensions).GetMethod(nameof(MyStandardDeviationSample))!;

    public static double MyStandardDeviationSample(IEnumerable<double> values)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(MyStandardDeviationSample)));
}

public static class MyStandardDeviationSampleExtensions
{
    public static ModelBuilder AddMyStandardDeviationSampleSupport(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(MyStandardDeviationSampleDbFunctionsExtensions.MyStandardDeviationSampleMethod)
            .HasTranslation(expressions =>
            {
                var source = new EnumerableExpression(expressions[0]);

                if (source.Selector is not SqlExpression sqlExpression)
                    return null;

                return MyStandardDeviationSampleTranslator.AggregateFunction(
                    "stddev_samp",
                    [sqlExpression],
                    source,
                    nullable: true,
                    argumentsPropagateNullability: [false],
                    typeof(double),
                    expressions[0].TypeMapping);
            });

        return modelBuilder;
    }

    public static NpgsqlDbContextOptionsBuilder AddMyStandardDeviationSampleSupportTranslator(this NpgsqlDbContextOptionsBuilder optionsBuilder)
    {
        var coreOptionsBuilder = ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder;

        var extension = coreOptionsBuilder.Options.FindExtension<MyStandardDeviationSampleDbContextOptionsExtension>() ?? new MyStandardDeviationSampleDbContextOptionsExtension();

        ((IDbContextOptionsBuilderInfrastructure)coreOptionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}

public class MyStandardDeviationSampleTranslator : IMethodCallTranslator
{
    private readonly IRelationalTypeMappingSource _typeMappingSource;
    private readonly NpgsqlSqlExpressionFactory _sqlExpressionFactory;

    public MyStandardDeviationSampleTranslator(IRelationalTypeMappingSource typeMappingSource, NpgsqlSqlExpressionFactory sqlExpressionFactory)
    {
        _typeMappingSource = typeMappingSource;
        _sqlExpressionFactory = sqlExpressionFactory;

        //stringTypeMapping = typeMappingSource.FindMapping(typeof(string));
        //jsonbTypeMapping = typeMappingSource.FindMapping("jsonb");
    }

    public SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> expressions, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method != MyStandardDeviationSampleDbFunctionsExtensions.MyStandardDeviationSampleMethod)
            return null;

        var source = new EnumerableExpression(expressions[0]);

        if (source.Selector is not SqlExpression sqlExpression)
            return null;

        return AggregateFunction(
            "stddev_samp",
            [sqlExpression],
            source,
            nullable: true,
            argumentsPropagateNullability: [false],
            typeof(double),
            expressions[0].TypeMapping);
    }

    public static PgFunctionExpression AggregateFunction(
        string name,
        IEnumerable<SqlExpression> arguments,
        EnumerableExpression aggregateEnumerableExpression,
        bool nullable,
        IEnumerable<bool> argumentsPropagateNullability,
        Type returnType,
        RelationalTypeMapping? typeMapping = null)
    {
        var typeMappedArguments = new List<SqlExpression>();

        foreach (var argument in arguments)
        {
            typeMappedArguments.Add(ApplyDefaultTypeMapping(argument));
        }

        return new PgFunctionExpression(
            name,
            typeMappedArguments,
            argumentNames: null,
            argumentSeparators: null,
            aggregateEnumerableExpression.IsDistinct,
            aggregateEnumerableExpression.Predicate,
            aggregateEnumerableExpression.Orderings,
            nullable: nullable,
            argumentsPropagateNullability: argumentsPropagateNullability,
            type: returnType,
            typeMapping: typeMapping);
    }

    public static SqlExpression? ApplyDefaultTypeMapping(SqlExpression? sqlExpression)
    {
        return sqlExpression;
    }
}

public class MyStandardDeviationSampleTranslatorPlugin : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; }

    public MyStandardDeviationSampleTranslatorPlugin(IRelationalTypeMappingSource typeMappingSource, ISqlExpressionFactory sqlExpressionFactory)
    {
        Translators = [new MyStandardDeviationSampleTranslator(typeMappingSource, (NpgsqlSqlExpressionFactory)sqlExpressionFactory)];
    }
}

public class MyStandardDeviationSampleDbContextOptionsExtension : IDbContextOptionsExtension
{
    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    public void Validate(IDbContextOptions options) { }

    void IDbContextOptionsExtension.ApplyServices(IServiceCollection services)
        => services.AddSingleton<IMethodCallTranslatorPlugin, MyStandardDeviationSampleTranslatorPlugin>();

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private new NpgsqlNodaTimeOptionsExtension Extension
            => (NpgsqlNodaTimeOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider
            => false;

        public override int GetServiceProviderHashCode()
            => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => true;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Npgsql:" + nameof(NpgsqlNodaTimeDbContextOptionsBuilderExtensions.UseNodaTime)] = "1";

        public override string LogFragment
            => "using NodaTime ";
    }
}