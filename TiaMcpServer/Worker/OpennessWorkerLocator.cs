using TiaMcpServer.Diagnostics;

namespace TiaMcpServer.Worker;

public sealed record WorkerCandidate(string Path, bool Exists);

public sealed record WorkerLocationResult(
    bool Found,
    string? SelectedPath,
    IReadOnlyList<WorkerCandidate> Candidates);

public static class OpennessWorkerLocator
{
    private const string WorkerExecutableV21Name = "TiaMcpServer.OpennessWorker.exe";
    private const string WorkerExecutableV20Name = "TiaMcpServer.OpennessWorker.V20.exe";

    private const string WorkerFolderName = "openness-worker";
    private const string WorkerV20FolderName = "V20";
    private const string WorkerV21VersionName = "V21";

    private static bool TrySelectWorkerVersion(string? projectPath, out string workerVersion)
    {
        var extension = Path.GetExtension(projectPath ?? string.Empty);

        if (string.Equals(extension, ".ap20", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".amc20", StringComparison.OrdinalIgnoreCase))
        {
            workerVersion = WorkerV20FolderName;
            return true;
        }

        if (string.Equals(extension, ".ap21", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".amc21", StringComparison.OrdinalIgnoreCase))
        {
            workerVersion = WorkerV21VersionName;
            return true;
        }

        workerVersion = string.Empty;
        return false;
    }

    private static string SelectWorkerExecutableName(string workerVersion)
        => string.Equals(workerVersion, WorkerV20FolderName, StringComparison.Ordinal)
            ? WorkerExecutableV20Name
            : WorkerExecutableV21Name;

    private static void AddCandidate(List<WorkerCandidate> candidates, IFileSystemService fileSystem, string path)
        => candidates.Add(new WorkerCandidate(path, fileSystem.FileExists(path)));

    private static List<WorkerCandidate> BuildCandidates(
        string baseDirectory,
        IFileSystemService fileSystem,
        string workerVersion)
    {
        var executableName = SelectWorkerExecutableName(workerVersion);
        var candidates = new List<WorkerCandidate>();

        // Packaged layout(s):
        // - legacy/flat: openness-worker\...
        // - current:     openness-worker\[V20\]net48\...
        if (string.Equals(workerVersion, WorkerV20FolderName, StringComparison.Ordinal))
        {
            AddCandidate(candidates, fileSystem, Path.Combine(baseDirectory, WorkerFolderName, WorkerV20FolderName, "net48", executableName));
            AddCandidate(candidates, fileSystem, Path.Combine(baseDirectory, WorkerFolderName, WorkerV20FolderName, executableName));
        }
        else
        {
            AddCandidate(candidates, fileSystem, Path.Combine(baseDirectory, WorkerFolderName, WorkerV21VersionName, "net48", executableName));
            AddCandidate(candidates, fileSystem, Path.Combine(baseDirectory, WorkerFolderName, WorkerV21VersionName, executableName));
            AddCandidate(candidates, fileSystem, Path.Combine(baseDirectory, WorkerFolderName, "net48", executableName));
            AddCandidate(candidates, fileSystem, Path.Combine(baseDirectory, WorkerFolderName, executableName));
        }

        // Dev layout:
        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                AddCandidate(
                    candidates,
                    fileSystem,
                    Path.Combine(
                        directory.FullName,
                        "TiaMcpServer.OpennessWorker",
                        "bin",
                        workerVersion,
                        configuration,
                        "net48",
                        executableName));
            }

            directory = directory.Parent;
        }

        return candidates;
    }

    public static WorkerLocationResult Locate(string baseDirectory, IFileSystemService fileSystem, string? projectPath = null)
    {
        if (!TrySelectWorkerVersion(projectPath, out var workerVersion))
        {
            var allCandidates = BuildCandidates(baseDirectory, fileSystem, WorkerV20FolderName)
                .Concat(BuildCandidates(baseDirectory, fileSystem, WorkerV21VersionName))
                .GroupBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return new WorkerLocationResult(false, null, allCandidates);
        }

        var candidates = BuildCandidates(baseDirectory, fileSystem, workerVersion);
        var selected = candidates.FirstOrDefault(c => c.Exists);

        return new WorkerLocationResult(
            selected is not null,
            selected?.Path,
            candidates);
    }

    public static WorkerLocationResult LocateAny(string baseDirectory, IFileSystemService fileSystem)
    {
        var allCandidates = BuildCandidates(baseDirectory, fileSystem, WorkerV20FolderName)
            .Concat(BuildCandidates(baseDirectory, fileSystem, WorkerV21VersionName))
            .GroupBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var selected = allCandidates.FirstOrDefault(c => c.Exists);
        return new WorkerLocationResult(
            selected is not null,
            selected?.Path,
            allCandidates);
    }

    public static string LocateOrThrow(string baseDirectory, IFileSystemService fileSystem, string? projectPath = null)
    {
        if (!TrySelectWorkerVersion(projectPath, out var workerVersion))
        {
            var anyResult = LocateAny(baseDirectory, fileSystem);
            if (anyResult.Found && anyResult.SelectedPath is not null)
            {
                return anyResult.SelectedPath;
            }

            throw new InvalidOperationException(
                "TIA worker version could not be determined from project path and no worker executable was found. " +
                "Use an absolute .ap20/.amc20 or .ap21/.amc21 path (for example via bind_project or configured project path) before project operations, " +
                "or ensure the openness-worker folder contains the V20/V21 worker binaries.");
        }

        var result = Locate(baseDirectory, fileSystem, projectPath);
        if (result.Found)
        {
            return result.SelectedPath!;
        }

        var executableName = SelectWorkerExecutableName(workerVersion);
        var inspected = string.Join("; ", result.Candidates.Select(c => c.Path));
        throw new FileNotFoundException(
            $"TIA Openness worker executable for {workerVersion} was not found. Build the solution and ensure the openness-worker folder is beside the MCP server executable. Inspected: {inspected}",
            result.Candidates.FirstOrDefault()?.Path ?? executableName);
    }
}
