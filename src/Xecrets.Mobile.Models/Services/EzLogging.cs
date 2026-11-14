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

using System.Text;

using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Services;

/// <summary>
/// Logs to log.txt in the given folder, which is rotated before it grows past 1 MB.
/// </summary>
public sealed class EzLogging(string directory) : IEzLogging
{
    private const long _maxLogLength = 1024 * 1024;

    // The length of the longest level name, "Information", so that the texts are aligned.
    private const int _levelWidth = 11;

    private readonly Lock _lock = new();

    private readonly RotatingLogFile _logFile = new(directory, "log");

    private EzLogLevel _level = EzLogLevel.Information;

    /// <inheritdoc/>
    public void DeleteAll()
    {
        lock (_lock)
        {
            try
            {
                _logFile.DeleteAll();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort, as for logging.
            }
        }
    }

    /// <inheritdoc/>
    public void SetLevel(EzLogLevel level)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(level, EzLogLevel.Unknown);

        _level = level;
    }

    /// <inheritdoc/>
    public void Log(EzLogLevel level, string text)
    {
        string line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z {LevelName(level),-_levelWidth} {text.ReplaceLineEndings(" → ")}{Environment.NewLine}";

        lock (_lock)
        {
            if (level > _level)
            {
                return;
            }

            try
            {
                // The OS, or the user, may clear the cache at any time.
                Directory.CreateDirectory(directory);
                FileInfo current = new(_logFile.FilePath);
                if (current.Exists && current.Length + Encoding.UTF8.GetByteCount(line) > _maxLogLength)
                {
                    _logFile.Rotate();
                }

                File.AppendAllText(_logFile.FilePath, line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort - logging never changes what is done.
            }
        }
    }

    private static string LevelName(EzLogLevel level) => level switch
    {
        EzLogLevel.Error => "Error",
        EzLogLevel.Warning => "Warning",
        EzLogLevel.Information => "Information",
        EzLogLevel.Debug => "Debug",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
