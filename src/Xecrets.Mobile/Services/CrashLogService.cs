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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Services;

public sealed class CrashLogService : ICrashLogService
{
    private const string _crashLogDirectoryName = "XecretsCrashLogs";

    private const string _crashLogFileName = "crashlog.txt";

    public bool HasPendingCrashLog => File.Exists(CurrentPath);

    private static string CurrentDirectory =>
        Path.Combine(FileSystem.Current.CacheDirectory, _crashLogDirectoryName);

    private static string CurrentPath => Path.Combine(CurrentDirectory, _crashLogFileName);

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
        Rotate();
        return report;
    }

    private static void Rotate()
    {
        string directory = CurrentDirectory;
        string oldestPath = Path.Combine(directory, "crashlog.9.txt");
        File.Delete(oldestPath);

        for (int index = 8; index >= 1; index--)
        {
            string sourcePath = Path.Combine(directory, $"crashlog.{index}.txt");
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, Path.Combine(directory, $"crashlog.{index + 1}.txt"));
            }
        }

        File.Move(CurrentPath, Path.Combine(directory, "crashlog.1.txt"));
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
}
