namespace KosovaPOS.Web.Services;

/// <summary>
/// The shop-PC install package this server is handing out, staged into <c>agent-package/</c>
/// by <c>tools/build-release.sh</c>. It is a build artifact, not source: a deploy that skipped
/// the build step serves no package at all, and <see cref="Available"/> says so rather than
/// letting /pajisjet advertise a download that 404s.
/// </summary>
public sealed class AgentPackage
{
    public AgentPackage(IWebHostEnvironment env)
    {
        var dir = Environment.GetEnvironmentVariable("POS_AGENT_PACKAGE_DIR")
                  ?? Path.Combine(env.ContentRootPath, "agent-package");

        var zip = Path.Combine(dir, "agjenti.zip");
        if (!File.Exists(zip)) return;

        Available = true;
        SizeMb = new FileInfo(zip).Length / 1024d / 1024d;

        var version = Path.Combine(dir, "version.txt");
        if (File.Exists(version))
            Version = File.ReadAllText(version).Trim();
    }

    public bool Available { get; }
    public string? Version { get; }
    public double SizeMb { get; }
}
