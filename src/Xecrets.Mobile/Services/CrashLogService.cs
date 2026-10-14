#region Copyright and GPL License

/*
 * Xecrets Ez Mobile - Copyright © 2026 Svante Seleborg, All Rights Reserved.
 *
 * This code file is part of Xecrets Ez Mobile, an application that uses the Xecrets.Net library, parts of which in turn
 * are derived from AxCrypt as licensed under GPL v3 or later. This code is not derived from AxCrypt. It is separately
 * authored and copyrighted, and licensed only as follows unless explicitly licensed otherwise.
 *
 * Xecrets Ez Mobile is free software: you can redistribute it and/or modify it under the terms of the GNU General
 * Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any
 * later version.
 *
 * No additional permission is granted beyond that license. If you incorporate this code into a larger work and
 * distribute that work to others, you are responsible for complying with the GNU General Public License version 3 or
 * later. See https://www.gnu.org/licenses/ for more information.
 *
 * Xecrets Ez Mobile is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the
 * implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more
 * details.
 *
 * You should have received a copy of the GNU General Public License along with Xecrets Ez Mobile. If not, see
 * <https://www.gnu.org/licenses/>.
 *
 * The source repository can be found at https://github.com/xecrets/xecrets-mobile please go there for more information,
 * suggestions and contributions. You may also visit https://www.axantum.com for more information about the author.
 */

#endregion Copyright and GPL License

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Services;

internal sealed class CrashLogService : ICrashLogService
{
    private const string _crashLogDirectoryName = "XecretsCrashLogs";

    private const string _crashLogName = "crashlog";

    private const string _swallowedLogName = "swallowed";

    // As for crash logs, the current log and the 9 before it are kept, one exception in each.
    private const int _keptLogCount = 10;

    private readonly Lock _swallowedLock = new();

    public bool HasPendingCrashLog => File.Exists(CurrentPath);

    private static string CurrentDirectory =>
        Path.Combine(FileSystem.Current.CacheDirectory, _crashLogDirectoryName);

    private static string CurrentPath => GetCurrentPath(_crashLogName);

    public void RegisterHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog("Unhandled managed exception", args.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, args) =>
            WriteCrashLog("Unobserved task exception", args.Exception);
    }

    public string ReadCurrent()
    {
        string report = File.ReadAllText(CurrentPath);
        Rotate(_crashLogName);
        return report;
    }

    private static string GetCurrentPath(string name) => Path.Combine(CurrentDirectory, $"{name}.txt");

    // The current log, name.txt, becomes name.1.txt and so on, the oldest being removed.
    private static void Rotate(string name)
    {
        string directory = CurrentDirectory;
        File.Delete(Path.Combine(directory, $"{name}.{_keptLogCount - 1}.txt"));

        for (int index = _keptLogCount - 2; index >= 1; index--)
        {
            string sourcePath = Path.Combine(directory, $"{name}.{index}.txt");
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, Path.Combine(directory, $"{name}.{index + 1}.txt"));
            }
        }

        string currentPath = GetCurrentPath(name);
        if (File.Exists(currentPath))
        {
            File.Move(currentPath, Path.Combine(directory, $"{name}.1.txt"));
        }
    }

    public void WriteCrashLog(string source, object? crash)
    {
        Directory.CreateDirectory(CurrentDirectory);

        StringBuilder report = new();
        report.AppendLine(source);
        report.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        report.AppendLine($"Application: {AppInfo.Current.Name} {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})");
        report.AppendLine($"Platform: {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
        report.AppendLine($"Device: {DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}");
        report.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        report.AppendLine();
        report.AppendLine(crash?.ToString() ?? "No managed exception information was available.");

        File.WriteAllText(CurrentPath, report.ToString());
    }

    public void WriteSwallowedException(
        Exception exception,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // The path is that of the build machine, which may not use the separator of the device.
        string fileName = filePath[(filePath.LastIndexOfAny(['/', '\\']) + 1)..];

        StringBuilder report = new();
        report.AppendLine($"Swallowed in {fileName}:{lineNumber} ({memberName})");
        report.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        report.AppendLine();
        report.AppendLine(exception.ToString());

        lock (_swallowedLock)
        {
            try
            {
                Directory.CreateDirectory(CurrentDirectory);
                Rotate(_swallowedLogName);
                File.WriteAllText(GetCurrentPath(_swallowedLogName), report.ToString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort - logging never changes what is done about the exception.
            }
        }
    }
}
