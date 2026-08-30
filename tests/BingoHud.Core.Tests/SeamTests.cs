using System.Reflection;
using System.Runtime.Versioning;

namespace BingoHud.Core.Tests;

/// <summary>
/// Guards the one architectural rule the project rests on: Core decides, App draws.
///
/// These are fence tests. They pass the day they are written, by design — their value is
/// failing later, if someone gives Core a reason to know about WPF. That fence was verified
/// during task 1.3 by temporarily retargeting Core at net9.0-windows and watching the build
/// and these tests break.
/// </summary>
public class SeamTests
{
    private static Assembly Core => Assembly.Load("BingoHud.Core");

    [Fact]
    public void CoreDoesNotReferenceWpf()
    {
        var wpfAssemblies = new[]
        {
            "PresentationFramework",
            "PresentationCore",
            "WindowsBase",
            "System.Xaml",
        };

        var referenced = Core.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null)
            .ToArray();

        var violations = referenced
            .Intersect(wpfAssemblies, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CoreTargetsAPlatformNeutralFramework()
    {
        // This is the fence itself rather than a restatement of it. WPF requires a
        // Windows-specific target framework, so a Core that is not Windows-specific cannot
        // reference WPF even if someone tries.
        var targetFramework = Core.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        Assert.NotNull(targetFramework);
        Assert.DoesNotContain("windows", targetFramework, StringComparison.OrdinalIgnoreCase);
    }
}
