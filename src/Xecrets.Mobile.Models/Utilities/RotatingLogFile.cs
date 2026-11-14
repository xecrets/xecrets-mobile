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

namespace Xecrets.Mobile.Models.Utilities;

/// <summary>
/// A log file, name.txt in its folder, rotated to name.1.txt, name.2.txt and so on. The current file and the 9 before
/// it are kept.
/// </summary>
/// <param name="directory">The folder of the log files.</param>
/// <param name="name">The name of the log files, without the number and the extension.</param>
public sealed class RotatingLogFile(string directory, string name)
{
    private const int _keptLogCount = 10;

    /// <summary>
    /// The full path of the current log file, which need not exist.
    /// </summary>
    public string FilePath => Path.Combine(directory, $"{name}.txt");

    /// <summary>
    /// Makes the current file the one before it, name.1.txt, and so on, deleting the oldest kept. There is then no
    /// current file.
    /// </summary>
    public void Rotate()
    {
        File.Delete(Path.Combine(directory, $"{name}.{_keptLogCount - 1}.txt"));

        for (int index = _keptLogCount - 2; index >= 1; index--)
        {
            string sourcePath = Path.Combine(directory, $"{name}.{index}.txt");
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, Path.Combine(directory, $"{name}.{index + 1}.txt"));
            }
        }

        if (File.Exists(FilePath))
        {
            File.Move(FilePath, Path.Combine(directory, $"{name}.1.txt"));
        }
    }

    /// <summary>
    /// Deletes the current file and those it was rotated to, and the folder when nothing else is left in it.
    /// </summary>
    public void DeleteAll()
    {
        // The folder is only made when something is logged, and the OS, or the user, may clear the cache at any time.
        if (!Directory.Exists(directory))
        {
            return;
        }

        File.Delete(FilePath);
        for (int index = 1; index < _keptLogCount; index++)
        {
            File.Delete(Path.Combine(directory, $"{name}.{index}.txt"));
        }

        // The folder may be shared with other logs, which then still keep it.
        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }
}
