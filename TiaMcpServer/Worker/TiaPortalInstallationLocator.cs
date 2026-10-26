using Microsoft.Win32;
using TiaMcpServer.Diagnostics;

namespace TiaMcpServer.Worker;


public sealed record TiaPortalCandidate(
    string Version,
    string Path,
    string InstallationPath,
    string Source,
    bool InstallationPresent,
    bool DirectoryPresent,
    bool AssembliesPresent,
    IReadOnlyList<string> MissingAssemblies);

public sealed record TiaPortalInstallationResult(
    bool Found,
    string? SelectedPath,
    string? SelectedInstallationPath,
    string? Source,
    bool OpennessFound,
    string? SelectedOpennessPath,
    string? OpennessSource,
    IReadOnlyList<TiaPortalCandidate> Candidates);

/// <summary>
/// Locates the TIA Portal V21 Openness API folder from the host (.NET 10) process.
/// Mirrors the precedence of <c>TiaMcpServer.OpennessWorker.Openness.Infrastructure.AssemblyResolver</c>
/// (which lives in the net48 worker and cannot be referenced here):
/// 1. <c>TiaPortalV21Dir</c> environment variable (used as-is, points at the net48 API folder).
/// 2. <c>TiaPortalLocation</c> environment variable (Portal root; <c>PublicAPI\V21\net48</c> appended).
/// 3. Siemens installation registry key <c>INSTALLPATH</c> (64- then 32-bit view).
/// 4. Default install path.
/// The first existing directory is selected as the installation. Assembly completeness is
/// retained separately so the installation and Openness-assembly diagnostics stay distinct.
/// </summary>
public static class TiaPortalInstallationLocator
{
    // Backward-compatible alias used by existing diagnostics tests.
    public static readonly string[] RequiredAssemblies =
    {
        "Siemens.Engineering.Base.dll",
        "Siemens.Engineering.Step7.dll"
    };

    public static readonly string[] RequiredAssembliesV21 =
    {
        "Siemens.Engineering.Base.dll",
        "Siemens.Engineering.Step7.dll"
    };

    public static readonly string[] RequiredAssembliesV20 =
    {
        "Siemens.Engineering.dll"
    };

    private const string TiaPortalV21DirEnvironmentVariable = "TiaPortalV21Dir";
    private const string TiaPortalV20DirEnvironmentVariable = "TiaPortalV20Dir";
    private const string TiaPortalLocationEnvironmentVariable = "TiaPortalLocation";
    private const string TiaPortalV20RegistrySubKey =
        @"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V20";
    private const string TiaPortalV21RegistrySubKey =
        @"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V21";
    private const string RegistryInstallValueName = "INSTALLPATH";
    private const string PublicApiV20Suffix = @"PublicAPI\V20";
    private const string PublicApiSuffix = @"PublicAPI\V21\net48";
    private const string StandardPortalV20InstallPath =
        @"C:\Program Files\Siemens\Automation\Portal V20";
    private const string StandardPortalInstallPath =
        @"C:\Program Files\Siemens\Automation\Portal V21";

    public static TiaPortalInstallationResult Locate(
        IEnvironmentVariableService env,
        IRegistryService registry,
        IFileSystemService fileSystem)
    {
        var candidates = new List<TiaPortalCandidate>();

        var envV20Dir = env.Get(TiaPortalV20DirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envV20Dir))
        {
            var path = envV20Dir!.Trim().Trim('"');
            AddCandidate(candidates, fileSystem, "V20", path, path, $"env:{TiaPortalV20DirEnvironmentVariable}", RequiredAssembliesV20);
        }

        var envV21Dir = env.Get(TiaPortalV21DirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envV21Dir))
        {
            var path = envV21Dir!.Trim().Trim('"');
            AddCandidate(candidates, fileSystem, "V21", path, path, $"env:{TiaPortalV21DirEnvironmentVariable}", RequiredAssembliesV21);
        }

        var envLocation = env.Get(TiaPortalLocationEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envLocation))
        {
            var root = envLocation!.Trim().Trim('"');
            var v20Path = Path.Combine(root, PublicApiV20Suffix);
            AddCandidate(candidates, fileSystem, "V20", root, v20Path, $"env:{TiaPortalLocationEnvironmentVariable}", RequiredAssembliesV20);
            var v21Path = Path.Combine(root, PublicApiSuffix);
            AddCandidate(candidates, fileSystem, "V21", root, v21Path, $"env:{TiaPortalLocationEnvironmentVariable}", RequiredAssembliesV21);
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                var installPathV20 = registry.GetStringValue(
                    RegistryHive.LocalMachine,
                    view,
                    TiaPortalV20RegistrySubKey,
                    RegistryInstallValueName);

                if (!string.IsNullOrWhiteSpace(installPathV20))
                {
                    var root = installPathV20!.Trim().Trim('"');
                    var path = Path.Combine(root, PublicApiV20Suffix);
                    AddCandidate(candidates, fileSystem, "V20", root, path, $"registry:{view}", RequiredAssembliesV20);
                }

                var installPath = registry.GetStringValue(
                    RegistryHive.LocalMachine,
                    view,
                    TiaPortalV21RegistrySubKey,
                    RegistryInstallValueName);

                if (!string.IsNullOrWhiteSpace(installPath))
                {
                    var root = installPath!.Trim().Trim('"');
                    var path = Path.Combine(root, PublicApiSuffix);
                    AddCandidate(candidates, fileSystem, "V21", root, path, $"registry:{view}", RequiredAssembliesV21);
                }
            }
        }

        AddCandidate(
            candidates,
            fileSystem,
            "V20",
            StandardPortalV20InstallPath,
            Path.Combine(StandardPortalV20InstallPath, PublicApiV20Suffix),
            "default",
            RequiredAssembliesV20);

        AddCandidate(
            candidates,
            fileSystem,
            "V21",
            StandardPortalInstallPath,
            Path.Combine(StandardPortalInstallPath, PublicApiSuffix),
            "default",
            RequiredAssembliesV21);

        var selected = candidates.FirstOrDefault(c => c.InstallationPresent);
        var selectedOpenness = candidates.FirstOrDefault(c => c.AssembliesPresent);
        return new TiaPortalInstallationResult(
            selected is not null,
            selected?.Path,
            selected?.InstallationPath,
            selected?.Source,
            selectedOpenness is not null,
            selectedOpenness?.Path,
            selectedOpenness?.Source,
            candidates);
    }

    private static void AddCandidate(
        List<TiaPortalCandidate> candidates,
        IFileSystemService fileSystem,
        string version,
        string installationPath,
        string apiPath,
        string source,
        IReadOnlyList<string> requiredAssemblies)
    {
        var missing = new List<string>();
        var installationPresent = fileSystem.DirectoryExists(installationPath);
        var directoryPresent = fileSystem.DirectoryExists(apiPath);
        var assembliesPresent = directoryPresent;
        if (directoryPresent)
        {
            foreach (var assembly in requiredAssemblies)
            {
                if (!fileSystem.FileExists(Path.Combine(apiPath, assembly)))
                {
                    missing.Add(assembly);
                }
            }
            assembliesPresent = missing.Count == 0;
        }
        else
        {
            missing.AddRange(requiredAssemblies);
        }

        candidates.Add(new TiaPortalCandidate(
            version,
            apiPath,
            installationPath,
            source,
            installationPresent,
            directoryPresent,
            assembliesPresent,
            missing));
    }
}
