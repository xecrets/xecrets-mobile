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

using Xecrets.Mobile.Models;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Services;

/// <summary>
/// Keeps the crash logs and the logs of swallowed exceptions in a folder, one report in each file, rotated as new ones
/// are written.
/// </summary>
/// <param name="directory">The folder of the log files.</param>
/// <param name="log">The log that each crash and swallowed exception is also written to.</param>
internal sealed class CrashLogService(string directory, IEzLogging log) : ICrashLogService
{
    private readonly RotatingLogFile _crashLog = new(directory, "crashlog");

    private readonly RotatingLogFile _swallowedLog = new(directory, "swallowed");

    private readonly Lock _swallowedLock = new();

    /// <inheritdoc/>
    public bool HasPendingCrashLog => File.Exists(_crashLog.FilePath);

    /// <inheritdoc/>
    public void RegisterHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog("Unhandled managed exception", args.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, args) =>
            WriteCrashLog("Unobserved task exception", args.Exception);
    }

    /// <inheritdoc/>
    public string ReadCurrent()
    {
        string report = File.ReadAllText(_crashLog.FilePath);
        _crashLog.Rotate();
        return report;
    }

    /// <inheritdoc/>
    public void WriteCrashLog(string source, object? crash)
    {
        // An artificial native crash has no managed exception.
        log.Error($"{source}: {crash?.GetType().Name ?? "no managed exception"}");
        CreateDirectory();

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

        File.WriteAllText(_crashLog.FilePath, report.ToString());
    }

    /// <inheritdoc/>
    public void DeleteAll()
    {
        try
        {
            DeleteLegacyDirectory();
            _crashLog.DeleteAll();
            _swallowedLog.DeleteAll();
        }
        catch (Exception)
        {
            // Best effort, and a failure cannot be logged as swallowed, since it is those logs that are deleted.
        }
    }

    /// <inheritdoc/>
    public void WriteSwallowedException(
        Exception exception,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // The path is that of the build machine, which may not use the separator of the device.
        string fileName = filePath[(filePath.LastIndexOfAny(['/', '\\']) + 1)..];
        log.Warning($"Swallowed {exception.GetType().Name} in {fileName}:{lineNumber} ({memberName})");

        StringBuilder report = new();
        report.AppendLine($"Swallowed in {fileName}:{lineNumber} ({memberName})");
        report.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        report.AppendLine();
        report.AppendLine(exception.ToString());

        lock (_swallowedLock)
        {
            try
            {
                CreateDirectory();
                _swallowedLog.Rotate();
                File.WriteAllText(_swallowedLog.FilePath, report.ToString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort - logging never changes what is done about the exception.
            }
        }
    }

    private void CreateDirectory()
    {
        DeleteLegacyDirectory();
        Directory.CreateDirectory(directory);
    }

    // Earlier beta versions kept the crash logs in XecretsCrashLogs, which is no longer used. This can be deleted
    // 2027-01-01.
    private static void DeleteLegacyDirectory()
    {
        string legacyDirectory = Path.Combine(FileSystem.Current.CacheDirectory, "XecretsCrashLogs");
        if (Directory.Exists(legacyDirectory))
        {
            Directory.Delete(legacyDirectory, recursive: true);
        }
    }
}
