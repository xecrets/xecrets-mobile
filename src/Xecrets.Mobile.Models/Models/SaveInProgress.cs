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
/// A complete result being saved in one of My folders, which is kept on the device while it is saved. The app may be
/// ended before it is done, leaving the file saved in part, which is undone when the user next signs in. The source is
/// what keeps what the user had, until the result is saved, so it is what the file saved in part may be removed for.
/// </summary>
public sealed record SaveInProgress
{
    /// <summary>
    /// The local profile signed in, since My folders and their grants are those of a profile.
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
    /// The file the result is made from, which is not removed until the result is saved.
    /// </summary>
    public string SourceId { get; init; } = "";

    /// <summary>
    /// Whether the source is given the name again once the file saved in part is removed, as the original of edits is,
    /// which is kept under its backup name while they are saved.
    /// </summary>
    public bool RestoresSource { get; init; }
}
