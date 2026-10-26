using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TiaMcpServer.OpennessWorker.Openness.Infrastructure;

public static class AssemblyResolver
{
    private const string TiaPortalLocationEnvironmentVariable = "TiaPortalLocation";

    private const string TiaPortalV21DirEnvironmentVariable = "TiaPortalV21Dir";
    private const string TiaPortalV20DirEnvironmentVariable = "TiaPortalV20Dir";

    private const string TiaPortalV21RegistrySubKey =
        @"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V21";
    private const string TiaPortalV20RegistrySubKey =
        @"SOFTWARE\Siemens\Automation\InstalledApps\Totally Integrated Automation Portal V20";

    private const string PublicApiV21Suffix = @"PublicAPI\V21\net48";
    private const string PublicApiV20Suffix = @"PublicAPI\V20";

    private const string StandardV21OpennessInstallPath =
        @"C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48";
    private const string StandardV20OpennessInstallPath =
        @"C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20";

    private static readonly string[] RequiredAssembliesV21 =
    {
        "Siemens.Engineering.Base.dll",
        "Siemens.Engineering.Step7.dll"
    };

    private static readonly string[] RequiredAssembliesV20 =
    {
        "Siemens.Engineering.dll"
    };

    private sealed class OpennessProfile
    {
        public string Name { get; set; } = string.Empty;
        public string EnvDirVariable { get; set; } = string.Empty;
        public string RegistrySubKey { get; set; } = string.Empty;
        public string PublicApiSuffix { get; set; } = string.Empty;
        public string DefaultApiPath { get; set; } = string.Empty;
        public string[] RequiredAssemblies { get; set; } = Array.Empty<string>();
    }

    private static readonly OpennessProfile V21Profile = new()
    {
        Name = "V21",
        EnvDirVariable = TiaPortalV21DirEnvironmentVariable,
        RegistrySubKey = TiaPortalV21RegistrySubKey,
        PublicApiSuffix = PublicApiV21Suffix,
        DefaultApiPath = StandardV21OpennessInstallPath,
        RequiredAssemblies = RequiredAssembliesV21
    };

    private static readonly OpennessProfile V20Profile = new()
    {
        Name = "V20",
        EnvDirVariable = TiaPortalV20DirEnvironmentVariable,
        RegistrySubKey = TiaPortalV20RegistrySubKey,
        PublicApiSuffix = PublicApiV20Suffix,
        DefaultApiPath = StandardV20OpennessInstallPath,
        RequiredAssemblies = RequiredAssembliesV20
    };

    public static void Register()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    internal static string ExpandTiaPortalLocation(string tiaPortalRoot)
    {
        // Für bestehende Tests/Callsites weiterhin V21 als Default beibehalten.
        return ExpandTiaPortalLocation(tiaPortalRoot, PublicApiV21Suffix);
    }

    internal static string ExpandTiaPortalLocation(string tiaPortalRoot, string publicApiSuffix)
    {
        return Path.Combine(tiaPortalRoot.Trim().Trim('"'), publicApiSuffix);
    }

    internal static IEnumerable<string> CreateCandidatePaths(
        string? tiaPortalDir,
        string? tiaPortalLocation,
        IEnumerable<string?> registryInstallPaths,
        string defaultPath)
    {
        // Für bestehende Tests/Callsites weiterhin V21 als Default beibehalten.
        return CreateCandidatePaths(tiaPortalDir, tiaPortalLocation, registryInstallPaths, defaultPath, PublicApiV21Suffix);
    }

    internal static IEnumerable<string> CreateCandidatePaths(
        string? tiaPortalDir,
        string? tiaPortalLocation,
        IEnumerable<string?> registryInstallPaths,
        string defaultPath,
        string publicApiSuffix)
    {
        if (!string.IsNullOrWhiteSpace(tiaPortalDir))
        {
            yield return tiaPortalDir.Trim().Trim('"');
        }

        if (!string.IsNullOrWhiteSpace(tiaPortalLocation))
        {
            yield return ExpandTiaPortalLocation(tiaPortalLocation, publicApiSuffix);
        }

        foreach (var registryInstallPath in registryInstallPaths)
        {
            if (!string.IsNullOrWhiteSpace(registryInstallPath))
            {
                yield return ExpandTiaPortalLocation(registryInstallPath, publicApiSuffix);
            }
        }

        yield return defaultPath;
    }

    internal static string? SelectFirstCompleteCandidate(
        IEnumerable<string> candidates,
        Func<string, bool> isComplete,
        Action<string>? inspected = null)
    {
        foreach (var candidate in candidates)
        {
            inspected?.Invoke(candidate);
            if (isComplete(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var requestedName = new AssemblyName(args.Name);

        if (!IsSiemensEngineeringAssembly(requestedName.Name))
        {
            return null;
        }

        var profile = GetActiveProfile();
        var opennessInstallPath = GetOpennessInstallPath(profile);
        var assemblyPath = Path.Combine(opennessInstallPath, $"{requestedName.Name}.dll");

        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        var loadedAssembly = Assembly.LoadFrom(assemblyPath);

        // Nicht hart auf FullName/version scheitern lassen:
        // Patch-/Build-Unterschiede zwischen Referenz und installiertem TIA dürfen den Worker nicht killen.
        if (!string.Equals(requestedName.Name, loadedAssembly.GetName().Name, StringComparison.Ordinal))
        {
            return null;
        }

        return loadedAssembly;
    }

    private static bool IsSiemensEngineeringAssembly(string? assemblyName)
    {
        return assemblyName is not null &&
               (string.Equals(assemblyName, "Siemens.Engineering", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Siemens.Engineering.", StringComparison.Ordinal));
    }

    private static OpennessProfile GetActiveProfile()
    {
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

        if (entryAssemblyName is not null &&
            entryAssemblyName.EndsWith(".V20", StringComparison.Ordinal))
        {
            return V20Profile;
        }

        return V21Profile;
    }

    private static string GetOpennessInstallPath(OpennessProfile profile)
    {
        var checkedLocations = new List<string>();
        var environmentPath = Environment.GetEnvironmentVariable(profile.EnvDirVariable);
        var tiaPortalLocation = Environment.GetEnvironmentVariable(TiaPortalLocationEnvironmentVariable);
        var registryInstallPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetRegistryInstallPaths(profile, checkedLocations)
            : Enumerable.Empty<string?>();

        var candidates = CreateCandidatePaths(
            environmentPath,
            tiaPortalLocation,
            registryInstallPaths,
            profile.DefaultApiPath,
            profile.PublicApiSuffix);

        var selected = SelectFirstCompleteCandidate(
            candidates,
            path => ContainsRequiredAssemblies(path, profile.RequiredAssemblies),
            path => checkedLocations.Add(path));

        if (selected is not null)
        {
            return selected;
        }

        throw new FileNotFoundException(
            $"TIA Portal {profile.Name} Openness assemblies were not found. Checked locations: " +
            string.Join("; ", checkedLocations) +
            $". Set {profile.EnvDirVariable} to the folder containing Siemens.Engineering*.dll files.");
    }

    private static IEnumerable<string?> GetRegistryInstallPaths(OpennessProfile profile, List<string> checkedLocations)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield break;
        }

        foreach (var registryView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var registryPath = $@"HKLM\{profile.RegistrySubKey} ({registryView})";
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            using var key = baseKey.OpenSubKey(profile.RegistrySubKey);

            if (key is null)
            {
                checkedLocations.Add($"{registryPath}: key not found");
                continue;
            }

            var installPath = key.GetValue("INSTALLPATH") as string;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                checkedLocations.Add($"{registryPath}: INSTALLPATH not set");
                continue;
            }

            yield return installPath;
        }
    }

    private static bool ContainsRequiredAssemblies(string directoryPath, IEnumerable<string> requiredAssemblies)
    {
        return Directory.Exists(directoryPath) &&
               requiredAssemblies.All(assemblyName => File.Exists(Path.Combine(directoryPath, assemblyName)));
    }
}
