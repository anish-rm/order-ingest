namespace OrderIngest.Tests;

/// <summary>Reads the shared provider fixtures from the repo's fixtures/ directory.</summary>
public static class TestFixtures
{
    public static string FixturesDir { get; } = Locate();

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(FixturesDir, name));

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
        {
            dir = dir.Parent!;
        }

        return dir is null
            ? throw new DirectoryNotFoundException("fixtures/ not found above test bin directory")
            : Path.Combine(dir.FullName, "fixtures");
    }
}
