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
/// A page of the app to navigate to.
/// </summary>
public enum AppDestination
{
    /// <summary>
    /// The page that shows the crash log of the last crash.
    /// </summary>
    Crash,

    /// <summary>
    /// The page to sign in to the local profile.
    /// </summary>
    Login,

    /// <summary>
    /// The page shown when there is no local profile.
    /// </summary>
    Introduction,

    /// <summary>
    /// The page to create the local profile.
    /// </summary>
    CreateProfile,

    /// <summary>
    /// The home page.
    /// </summary>
    Home,

    /// <summary>
    /// The page that previews a decrypted file.
    /// </summary>
    Preview,

    /// <summary>
    /// The page that shows a decrypted file in the app.
    /// </summary>
    View,

    /// <summary>
    /// The page to edit decrypted text.
    /// </summary>
    Edit,

    /// <summary>
    /// The page that shows an encrypted copy.
    /// </summary>
    EncryptResult,

    /// <summary>
    /// The page to encrypt a copy with a password for sharing.
    /// </summary>
    EncryptToShare,

    /// <summary>
    /// My folders.
    /// </summary>
    WorkFolders,

    /// <summary>
    /// The page to enter decryption passwords.
    /// </summary>
    EnterPassword,

    /// <summary>
    /// The page about the app.
    /// </summary>
    About,

    /// <summary>
    /// The page of the third-party licenses.
    /// </summary>
    ThirdPartyLicenses,

    /// <summary>
    /// The debug page.
    /// </summary>
    Debug,

    /// <summary>
    /// The debug page that browses the cache.
    /// </summary>
    CacheBrowser,

    /// <summary>
    /// The page that suggests a password.
    /// </summary>
    SuggestPassword,

    /// <summary>
    /// The recent files.
    /// </summary>
    RecentFiles,

    /// <summary>
    /// The welcome page.
    /// </summary>
    Welcome,

    /// <summary>
    /// The page to verify the password of the profile.
    /// </summary>
    VerifyPassword,

    /// <summary>
    /// The page to change the local profile.
    /// </summary>
    ChangeProfile,

    /// <summary>
    /// The page to reset a forgotten password.
    /// </summary>
    ResetPassword,

    /// <summary>
    /// The page shown while the app starts.
    /// </summary>
    Startup,
}
