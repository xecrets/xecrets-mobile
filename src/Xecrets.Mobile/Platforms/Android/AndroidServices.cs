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
using System.Runtime.Versioning;

using Android.Runtime;
using Android.Text;
using Android.Graphics;
using AndroidContentCaptureImportance = Android.Views.ViewImportantForContentCapture;
using AndroidImportantForAutofill = Android.Views.ImportantForAutofill;
using AndroidView = Android.Views.View;
using AndroidWindow = Android.Views.Window;
using AndroidWindowManagerFlags = Android.Views.WindowManagerFlags;

using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Handlers;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Abstractions;
using Xecrets.Mobile.Services;
using Xecrets.Mobile.Utilities;

namespace Xecrets.Mobile.Platforms.Android;

/// <summary>
/// The services of Android.
/// </summary>
[SupportedOSPlatform("android28.0")]
internal class AndroidServices : PlatformServicesBase
{
    /// <inheritdoc/>
    /// <remarks>
    /// Writes a crash log for an unhandled exception of the Android runtime.
    /// </remarks>
    public override void RegisterCrashHandlers(ICrashLogService crashLogService)
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            crashLogService.WriteCrashLog("Unhandled Android runtime exception", args.Exception);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Sends SIGABRT to the process.
    /// </remarks>
    public override void CrashNative()
    {
        global::Android.OS.Process.SendSignal(
            global::Android.OS.Process.MyPid(),
            (global::Android.OS.Signal)6);
    }

    /// <inheritdoc/>
    public override (double Left, double Right) GetWindowGaps(VisualElement view)
    {
        AndroidView platformView = (AndroidView)view.Handler!.PlatformView!;

        int[] location = new int[2];
        platformView.GetLocationInWindow(location);
        double density = DeviceDisplay.Current.MainDisplayInfo.Density;
        return (location[0] / density, (platformView.RootView!.Width - location[0] - platformView.Width) / density);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The window is made secure, which leaves it out of the recents snapshot, and also of screenshots, screen
    /// recording and casting.
    /// </remarks>
    public override void IsSensitiveContentVisible(bool isShown)
    {
        AndroidWindow window = Platform.CurrentActivity!.Window!;
        if (isShown)
        {
            window.AddFlags(AndroidWindowManagerFlags.Secure);
        }
        else
        {
            window.ClearFlags(AndroidWindowManagerFlags.Secure);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AndroidServices"/> class, and adds the handler mappings of Android
    /// for the font weight and the password entries. Requires a single instance for the app.
    /// </summary>
    /// <remarks>
    /// AppendToMapping runs each action after the mapping of MAUI for the same key, so the order of events does not
    /// matter. Entries opt in through <see cref="PasswordEntryProperties"/>.
    /// </remarks>
    public AndroidServices()
    {
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
            ApplyFontWeight(view, handler.PlatformView));
        ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (handler, view) =>
            ApplyFontWeight(view, handler.PlatformView));

        // One-time setup, applied whenever a password entry's handler (re)connects.
        EntryHandler.Mapper.AppendToMapping(PasswordEntryProperties.ConfigureMapperKey, (handler, view) =>
        {
            Entry entry = (Entry)view;
            if (PasswordEntryProperties.GetPurpose(entry) is not { } purpose)
            {
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                handler.PlatformView.SetAutofillHints(purpose switch
                {
                    PasswordEntryPurpose.NewPassword => "newPassword",
                    PasswordEntryPurpose.SharePassword => "notApplicable",
                    PasswordEntryPurpose.VerifyPassword => "notApplicable",
                    PasswordEntryPurpose.ExistingPassword => "password",
                    PasswordEntryPurpose.UserName => "username",
                    _ => throw new InvalidOperationException($"Invalid password entry purpose '{purpose}'."),
                });

                // The hint is only advice to the autofill service; these keep the entry out of autofill entirely,
                // so nothing is suggested and no save is offered. Not being important for autofill does that up to
                // Android 13, but Android 14 and later autofill such views too, unless they have no autofill type.
                if (purpose is PasswordEntryPurpose.SharePassword or PasswordEntryPurpose.VerifyPassword)
                {
                    if (handler.PlatformView is not PasswordEditText passwordEditText)
                    {
                        throw new InvalidOperationException(
                            $"Password entry purpose '{purpose}' requires a {nameof(PasswordEditText)}.");
                    }

                    handler.PlatformView.ImportantForAutofill = AndroidImportantForAutofill.NoExcludeDescendants;
                    passwordEditText.IsAutofillExcluded = true;
                }
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                handler.PlatformView.ImportantForContentCapture = (int)AndroidContentCaptureImportance.NoExcludeDescendants;
            }
        });

        // MAUI's own IsPassword mapping only applies the password input-type variation when
        // IsPassword is true; when the user reveals the password, force the visible-password
        // variation instead of the platform's plain-text default. TextFlagNoSuggestions is already
        // handled by MAUI's own IsSpellCheckEnabled mapping (set via PlatformServicesBase), so it
        // doesn't need to be repeated here.
        EntryHandler.Mapper.AppendToMapping(nameof(IEntry.IsPassword), (handler, view) =>
        {
            Entry entry = (Entry)view;
            if (entry.IsPassword || PasswordEntryProperties.GetPurpose(entry) is null or PasswordEntryPurpose.UserName)
            {
                return;
            }

            handler.PlatformView.InputType = (handler.PlatformView.InputType & ~InputTypes.MaskVariation)
                | InputTypes.TextVariationVisiblePassword;
        });
    }

    private static void ApplyFontWeight(IView view, global::Android.Widget.TextView platformView)
    {
        if (Typography.IsSemibold(view))
        {
            platformView.Typeface = Typeface.Create(platformView.Typeface, 600, false);
        }
    }
}
