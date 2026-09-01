namespace BingoHud.Core.Tests;

/// <summary>
/// A throwaway directory for tests that need real files on disk.
///
/// <para>
/// Created under the test output directory rather than the system temp folder, so a test run
/// writes nothing outside the project tree, and anything a crashed run leaves behind is cleaned
/// by <c>dotnet clean</c> along with everything else in <c>bin</c>.
/// </para>
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "test-scratch",
            Guid.NewGuid().ToString("n"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>
    /// Writes a file into the directory and returns its full path.
    /// </summary>
    public string WriteFile(string name, string content)
    {
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, content);
        return file;
    }

    /// <summary>
    /// A path inside the directory that nothing has created.
    /// </summary>
    public string PathTo(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A file still held open by a failing test is not worth failing the run over.
        }
    }
}
