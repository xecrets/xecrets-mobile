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

using CoreGraphics;

using UIKit;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// The view above the keyboard of a text field with a button that only closes the keyboard, in place of the one that
/// MAUI adds to entries, which also completes the entry as the return key does. It looks as that of MAUI: on iOS 26 and
/// later a floating close button that lets taps beside it through, and before that a toolbar with a done button.
/// </summary>
internal sealed class KeyboardCloseAccessoryView : UIView
{
    private const double AccessoryHeight = 44;

    private const double GlassButtonBottomSpacing = 4;

    private readonly UIButton? _glassButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardCloseAccessoryView"/> class.
    /// </summary>
    /// <param name="textField">The text field, whose keyboard the button closes.</param>
    public KeyboardCloseAccessoryView(UITextField textField)
        : base(new CGRect(0, 0, UIScreen.MainScreen.Bounds.Width, AccessoryHeight))
    {
        // The text field holds this view, so it is held weakly here.
        WeakReference<UITextField> weakTextField = new(textField);
        void Close(object? sender, EventArgs e)
        {
            if (weakTextField.TryGetTarget(out UITextField? target))
            {
                target.ResignFirstResponder();
            }
        }

        if (OperatingSystem.IsIOSVersionAtLeast(26))
        {
            _glassButton = CreateGlassButton(Close);
            ClipsToBounds = false;
            AddSubview(_glassButton);
            NSLayoutConstraint.ActivateConstraints(
            [
                _glassButton.WidthAnchor.ConstraintEqualTo((nfloat)AccessoryHeight),
                _glassButton.HeightAnchor.ConstraintEqualTo((nfloat)AccessoryHeight),
                _glassButton.CenterYAnchor.ConstraintEqualTo(CenterYAnchor, -(nfloat)GlassButtonBottomSpacing),
                _glassButton.TrailingAnchor.ConstraintEqualTo(LayoutMarginsGuide.TrailingAnchor),
            ]);
            return;
        }

        UIToolbar toolbar = new(Bounds)
        {
            Translucent = true,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
        };
        toolbar.SetItems(
            [
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                new UIBarButtonItem(UIBarButtonSystemItem.Done, Close),
            ],
            false);
        AddSubview(toolbar);
    }

    // The floating button takes only the taps on it, so that the text behind the rest of this view can be tapped.
    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        UIView? hitView = base.HitTest(point, uievent);
        return _glassButton is null || (hitView is not null && hitView.IsDescendantOfView(_glassButton))
            ? hitView
            : null;
    }

    [SupportedOSPlatform("ios26.0")]
    [SupportedOSPlatform("maccatalyst26.0")]
    private static UIButton CreateGlassButton(EventHandler close)
    {
        UIButton button = UIButton.FromType(UIButtonType.System);
        button.ClipsToBounds = false;
        button.TranslatesAutoresizingMaskIntoConstraints = false;
        button.AccessibilityLabel = UIKitLocalizedDone;
        button.TouchUpInside += close;
        UIButtonConfiguration configuration = UIButtonConfiguration.GlassButtonConfiguration;
        configuration.Image = UIImage.GetSystemImage("xmark");
        configuration.CornerStyle = UIButtonConfigurationCornerStyle.Capsule;
        configuration.ButtonSize = UIButtonConfigurationSize.Medium;
        configuration.BaseForegroundColor = UIColor.Label;
        button.Configuration = configuration;
        return button;
    }

    // The label of the done item of UIKit, in the language of the user, which VoiceOver reads for the button.
    private static string UIKitLocalizedDone =>
        Foundation.NSBundle.FromIdentifier("com.apple.UIKit")!.GetLocalizedString("Done");
}
