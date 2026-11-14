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

using Microsoft.Maui.Controls;

using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Abstractions;

/// <summary>
/// The services that differ between the platforms.
/// </summary>
internal interface IPlatformServices
{
    /// <summary>
    /// Information about crashes on the platform to show on the crash page, or "" if there is none.
    /// </summary>
    string CrashPageAdditionalInformation { get; }

    /// <summary>
    /// Configures an entry for a password, or for the user name next to one, so that the platform neither suggests nor
    /// remembers what is typed, and password managers treat it as the purpose tells.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="purpose">What the entry is for.</param>
    void ConfigurePasswordEntry(Entry entry, PasswordEntryPurpose purpose);

    /// <summary>
    /// Registers the handlers of the platform that write a crash log, in addition to those of
    /// <see cref="ICrashLogService.RegisterHandlers"/>.
    /// </summary>
    /// <param name="crashLogService">Writes the crash logs.</param>
    void RegisterCrashHandlers(ICrashLogService crashLogService);

    /// <summary>
    /// Ends the app with a native crash, without running any managed handler.
    /// </summary>
    void CrashNative();

    /// <summary>
    /// Gets the distances from a laid out view to the left and right edges of its window, measured on the platform
    /// view.
    /// </summary>
    /// <param name="view">The laid out view.</param>
    /// <returns>The distances in device-independent units.</returns>
    (double Left, double Right) GetWindowGaps(VisualElement view);

    /// <summary>
    /// Tells whether sensitive content is shown, which is then kept out of the snapshot of the app that the system
    /// saves for switching between apps.
    /// </summary>
    /// <param name="isShown">True while sensitive content is shown, false otherwise.</param>
    void IsSensitiveContentVisible(bool isShown);
}
