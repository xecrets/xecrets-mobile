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

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// Saves edited text of the file being previewed, encrypted for what the file is encrypted for. Once saved, the saved
/// text is the text of the file being previewed.
/// </summary>
public interface IEditSaveService
{
    /// <summary>
    /// Saves the text in place of the source file of the flow, in its folder. The source file of the flow then refers
    /// to the saved file, which replaces the source file in the recent files. The source file is kept under its backup
    /// name while saving, and gets its own name back if saving fails.
    /// </summary>
    /// <param name="text">The edited text.</param>
    /// <returns>True if the text is saved, false if nothing is saved, since there already is a file with the backup
    /// name of the source file in its folder.</returns>
    Task<bool> SaveAsync(string text);

    /// <summary>
    /// Saves the text as a copy where the user chooses. A copy saved in one of My folders becomes the source file of
    /// the flow and is added to the recent files.
    /// </summary>
    /// <param name="text">The edited text.</param>
    /// <returns>True if the copy is saved, false if the user cancels.</returns>
    Task<bool> SaveCopyAsync(string text);
}
