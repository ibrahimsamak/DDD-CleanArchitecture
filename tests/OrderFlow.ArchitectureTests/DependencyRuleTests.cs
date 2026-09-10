namespace OrderFlow.ArchitectureTests;

using System.Reflection;
using FluentAssertions;
using Xunit;

/// <summary>
/// Project references stop <c>Application -> Infrastructure</c> at compile time. They do not stop a
/// controller from injecting <c>OrderDbContext</c> directly, because Api legitimately references
/// Infrastructure. These tests catch that leak.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(OrderFlow.Domain.Orders.Order).Assembly;
    private static readonly Assembly Application = typeof(OrderFlow.Application.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(OrderFlow.Api.Controllers.OrdersController).Assembly;

    private static IEnumerable<string> ReferencedProjectsOf(Assembly a) =>
        a.GetReferencedAssemblies()
         .Select(r => r.Name!)
         .Where(n => n.StartsWith("OrderFlow.", StringComparison.Ordinal));

    [Fact]
    public void Domain_references_no_other_project()
    {
        ReferencedProjectsOf(Domain).Should().BeEmpty(
            "the Domain is the core — it must not know about any other layer");
    }

    [Fact]
    public void Domain_references_no_third_party_packages()
    {
        Domain.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Should().OnlyContain(n => n.StartsWith("System.", StringComparison.Ordinal)
                                    || n == "netstandard"
                                    || n == "System.Runtime",
                "the Domain must depend on the BCL and nothing else");
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure()
    {
        ReferencedProjectsOf(Application).Should().NotContain("OrderFlow.Infrastructure",
            "dependencies point inward — Infrastructure implements Application's interfaces, never the reverse");
    }

    [Fact]
    public void Only_the_composition_root_touches_Infrastructure()
    {
        // Api references Infrastructure so Program.cs can call AddInfrastructure().
        // Nothing else in Api is allowed to.
        var offenders = Api.GetTypes()
            .Where(t => t.Name != "Program" && !t.Name.StartsWith('<'))
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType.Namespace?.StartsWith("OrderFlow.Infrastructure",
                                                                StringComparison.Ordinal) == true))
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "controllers must depend on Application interfaces, not on EF Core types");
    }
}
