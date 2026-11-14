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

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Models;

/// <summary>
/// The extensions for logging.
/// </summary>
public static class EzLoggingExtensions
{
    extension(IEzLogging log)
    {
        /// <summary>
        /// Logs a text at the <see cref="EzLogLevel.Error"/> level, as <see cref="IEzLogging.Log"/> does.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Error(string text) => log.Log(EzLogLevel.Error, text);

        /// <summary>
        /// Logs a text at the <see cref="EzLogLevel.Warning"/> level, as <see cref="IEzLogging.Log"/> does.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Warning(string text) => log.Log(EzLogLevel.Warning, text);

        /// <summary>
        /// Logs a text at the <see cref="EzLogLevel.Information"/> level, as <see cref="IEzLogging.Log"/> does.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Information(string text) => log.Log(EzLogLevel.Information, text);

        /// <summary>
        /// Logs a text at the <see cref="EzLogLevel.Debug"/> level, as <see cref="IEzLogging.Log"/> does.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Debug(string text) => log.Log(EzLogLevel.Debug, text);
    }

    /// <summary>
    /// The path with the file name, without its extension, replaced by its first two characters followed by "***", or
    /// by just "***" when it has no more than two characters. The folders and the extension are kept as they are.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The masked path.</returns>
    public static string MaskPath(this string path)
    {
        string fileName = Path.GetFileName(path);
        string name = Path.GetFileNameWithoutExtension(fileName);
        string maskedName = name.Length > 2 ? $"{name[..2]}***" : "***";
        return $"{path[..^fileName.Length]}{maskedName}{Path.GetExtension(fileName)}";
    }
}
