using System.Diagnostics;
using StarPie.Plugin;

namespace WinPieGestures;

/// <summary>显式启动权限失败时不回退到宿主权限。</summary>
internal static class ProcessLaunchExecutor
{
    internal static bool Start(ProcessStartInfo info, ProcessLaunchMode mode) =>
        Start(info, mode,
            (file, arguments, directory) => ActionExecutor.TryLaunchUnelevatedViaExplorer(file, arguments, directory),
            startInfo => { using Process? process = Process.Start(startInfo); return true; });

    internal static bool Start(ProcessStartInfo info, ProcessLaunchMode mode,
        Func<string, string, string, bool> startStandardUser, Func<ProcessStartInfo, bool> startProcess)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == ProcessLaunchMode.Default) return startProcess(info);
        string directory = string.IsNullOrEmpty(info.WorkingDirectory) ? Environment.CurrentDirectory : info.WorkingDirectory;
        if (mode == ProcessLaunchMode.StandardUser)
            return startStandardUser(info.FileName, info.Arguments, directory);
        return startProcess(new ProcessStartInfo
        {
            FileName = info.FileName, Arguments = info.Arguments, WorkingDirectory = directory,
            UseShellExecute = true, Verb = "runas", WindowStyle = info.WindowStyle,
        });
    }
}
