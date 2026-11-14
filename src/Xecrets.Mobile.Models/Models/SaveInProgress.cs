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

namespace Xecrets.Mobile.Models.Models;

/// <summary>
/// A save of a complete result in one of My folders that has begun and is not done.
/// </summary>
public sealed record SaveInProgress
{
    /// <summary>
    /// The id of the local profile that saves the result.
    /// </summary>
    public string UserId { get; init; } = "";

    /// <summary>
    /// The folder the result is saved in, as given by <see cref="Abstractions.IFileAccess.GetFolderOfFileAsync"/>.
    /// </summary>
    public string FolderId { get; init; } = "";

    /// <summary>
    /// The name the result is saved under.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// The id of the file the result is made from.
    /// </summary>
    public string SourceId { get; init; } = "";

    /// <summary>
    /// True if the source file is kept under another name while the result is saved, and gets <see cref="Name"/> back
    /// when the save is undone, false otherwise.
    /// </summary>
    public bool RestoresSource { get; init; }
}
