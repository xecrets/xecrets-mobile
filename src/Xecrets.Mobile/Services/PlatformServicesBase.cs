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

using Microsoft.Maui.Controls;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;

namespace Xecrets.Mobile.Services;

/// <summary>
/// The parts of <see cref="IPlatformServices"/> that are shared by the platforms.
/// </summary>
internal abstract class PlatformServicesBase : IPlatformServices
{
    /// <inheritdoc/>
    /// <remarks>
    /// There is none, unless a platform overrides this.
    /// </remarks>
    public virtual string CrashPageAdditionalInformation => string.Empty;

    /// <inheritdoc/>
    /// <remarks>
    /// There are none, unless a platform overrides this.
    /// </remarks>
    public virtual void RegisterCrashHandlers(ICrashLogService crashLogService)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Fails fast, unless a platform overrides this.
    /// </remarks>
    public virtual void CrashNative()
    {
        Environment.FailFast("Artificial native crash.");
    }

    /// <inheritdoc/>
    public abstract (double Left, double Right) GetWindowGaps(VisualElement view);

    /// <inheritdoc/>
    /// <remarks>
    /// Does nothing, unless a platform overrides this.
    /// </remarks>
    public virtual void IsSensitiveContentVisible(bool isShown)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Turns off spell checking, which MAUI maps to no suggestions on every platform, and sets the purpose with
    /// <see cref="PasswordEntryProperties.SetPurpose"/> for the handler mappings of each platform.
    /// </remarks>
    public virtual void ConfigurePasswordEntry(Entry entry, PasswordEntryPurpose purpose)
    {
        // TextFlagNoSuggestions (Android) and its equivalents are already applied by MAUI's own
        // IsSpellCheckEnabled mapping, on every IsPassword change, on every platform - no custom
        // platform code needed for that part.
        entry.IsSpellCheckEnabled = false;

        // Marks this entry as an opted-in password entry and records its purpose, for the
        // per-platform handler-mapper customizations to pick up (mapper customization is global,
        // per control type, so it needs a per-instance marker to know which entries to touch).
        PasswordEntryProperties.SetPurpose(entry, purpose);
    }
}
