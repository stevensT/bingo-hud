namespace BingoHud.Core.Tests;

/// <summary>
/// Every call into Win32 lives in one file.
///
/// <para>
/// A fence test, in the same spirit as <see cref="SeamTests"/> and
/// <see cref="NoProcessLaunchTests"/>: it passes the day it is written, and its value is failing
/// later. Interop is where a WPF app rots quietest. A single <c>DllImport</c> added beside the
/// code that needs it looks entirely reasonable in review, and three years of that leaves a
/// shell where nobody can say what the app asks of the operating system, or check it against a
/// Windows version.
/// </para>
/// <para>
/// Asserted by reading the shell's source rather than by reflecting over its assembly. This
/// project references Core alone, on purpose, and taking a reference on the WPF app in order to
/// inspect it would breach the seam that <see cref="SeamTests"/> exists to hold.
/// </para>
/// </summary>
public class InteropSeamTests
{
    /// <summary>
    /// The one file allowed to know what Win32 is.
    /// </summary>
    private const string TheInteropFile = "NativeMethods.cs";

    private static string SourceDirectory =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "app-sources");

    private static IReadOnlyList<string> ShellSources() =>
        Directory.GetFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories);

    private static IEnumerable<string> FilesContaining(string text) =>
        ShellSources()
            .Where(f => File.ReadAllText(f).Contains(text, StringComparison.Ordinal))
            .Select(System.IO.Path.GetFileName)
            .Select(name => name!)
            .Order();

    [Fact]
    public void TheShellSourcesAreActuallyReachable()
    {
        // Without this, every assertion below passes by finding nothing at all, and the fence
        // would silently stop guarding anything the moment the build stopped copying the files.
        var sources = ShellSources();

        Assert.NotEmpty(sources);
        Assert.Contains(TheInteropFile, sources.Select(System.IO.Path.GetFileName));
    }

    [Fact]
    public void EveryPlatformInvokeDeclarationIsInTheInteropFile()
    {
        Assert.Equal([TheInteropFile], FilesContaining("DllImport"));
    }

    [Fact]
    public void TheSourceGeneratedFormOfPlatformInvokeIsCoveredToo()
    {
        // LibraryImport is the modern spelling of the same thing. Nothing uses it today, and the
        // point of naming it is that a future migration cannot quietly escape this fence.
        Assert.DoesNotContain(FilesContaining("LibraryImport"), f => f != TheInteropFile);
    }

    [Fact]
    public void OnlyTheInteropFileImportsTheInteropNamespace()
    {
        // The marshalling API travels with hand-written interop: structure layouts, sizes,
        // handles. A second file reaching for it is the first sign of a second interop file.
        Assert.Equal([TheInteropFile], FilesContaining("using System.Runtime.InteropServices;"));
    }

    [Fact]
    public void TheWindowsThatOwnAHandleAreTheOnlyOnesThatAskForOne()
    {
        // Not a breach, and worth writing down so a later reader does not think it is one. A
        // window handle is obtained through WPF, not through Win32, and something has to obtain
        // one to pass to the interop file. Keeping that in the window classes is what stops the
        // interop file needing to know about WPF at all — the opposite of the rule above, and
        // the reason it is not simply moved.
        Assert.Equal(
            ["DetailPanelWindow.xaml.cs", "HudWindow.xaml.cs"],
            FilesContaining("WindowInteropHelper"));
    }
}
