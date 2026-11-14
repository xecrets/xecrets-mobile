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

using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Models.Abstractions;

/// <summary>
/// The user interface of the app: dialogs, messages, navigation between pages, the browser and the clipboard.
/// </summary>
public interface IUserInterfaceService
{
    /// <summary>
    /// True if the app shell is shown in the window of the app, false otherwise.
    /// </summary>
    bool IsShellAvailable { get; }

    /// <summary>
    /// True if a file received from another app can be handled now, false if it waits.
    /// </summary>
    bool CanProcessIncomingFiles { get; }

    /// <summary>
    /// True if a file received from another app is accepted now, false if it is ignored.
    /// </summary>
    bool CanReceiveIncomingFiles { get; }

    /// <summary>
    /// Where the user is in the app, as the location of the page shown.
    /// </summary>
    string CurrentLocation { get; }

    /// <summary>
    /// True while a navigation through this service is not done, false otherwise, such as when the user goes back.
    /// </summary>
    bool IsNavigating { get; }

    /// <summary>
    /// Maps text placeholders, such as "(c)", to icon glyphs.
    /// </summary>
    IReadOnlyDictionary<string, string> IconMap { get; }

    /// <summary>
    /// Runs an action on the main thread.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>A task that completes when the action does.</returns>
    Task InvokeOnMainThreadAsync(Func<Task> action);

    /// <summary>
    /// Shows a message in a dialog, until the user dismisses it.
    /// </summary>
    /// <param name="message">The message.</param>
    Task DisplayMessageAsync(string message);

    /// <summary>
    /// Asks the user to confirm in a dialog.
    /// </summary>
    /// <param name="message">The question.</param>
    /// <param name="accept">The text of the button that confirms.</param>
    /// <param name="cancel">The text of the button that cancels.</param>
    /// <returns>True if the user confirms, false otherwise.</returns>
    Task<bool> DisplayConfirmationAsync(string message, string accept, string cancel);

    /// <summary>
    /// Asks the user whether to save, discard or keep unsaved changes. Dismissing the question keeps them.
    /// </summary>
    /// <param name="message">The question.</param>
    /// <returns>The choice of the user, which is <see cref="SaveChangesChoice.Cancel"/> to keep the changes.</returns>
    Task<SaveChangesChoice> DisplaySaveChangesAsync(string message);

    /// <summary>
    /// Asks the user for a line of text.
    /// </summary>
    /// <param name="message">The question.</param>
    /// <param name="initialValue">The text to start from.</param>
    /// <returns>The text, or null if the user cancels.</returns>
    Task<string?> DisplayPromptAsync(string message, string initialValue);

    /// <summary>
    /// Shows a message for a while, without the user having to dismiss it.
    /// </summary>
    /// <param name="message">The message.</param>
    Task DisplayTransientMessageAsync(string message);

    /// <summary>
    /// Navigates to a page. Without a parameter, nothing is done if the current page already is the destination.
    /// </summary>
    /// <param name="destination">The page to navigate to.</param>
    /// <param name="parameter">The parameter passed to the page as <see cref="NavigationParameter.Payload"/>, or null
    /// for none.</param>
    Task NavigateToAsync(AppDestination destination, object? parameter);

    /// <summary>
    /// Navigates to a page in place of the current page, so that going back skips the current page.
    /// </summary>
    /// <param name="destination">The page to navigate to.</param>
    /// <param name="parameter">The parameter passed to the page as <see cref="NavigationParameter.Payload"/>, or null
    /// for none.</param>
    Task NavigateReplacingAsync(AppDestination destination, object? parameter);

    /// <summary>
    /// Navigates back to the previous page.
    /// </summary>
    /// <param name="parameter">The parameter passed to the page as <see cref="NavigationParameter.Payload"/>, or null
    /// for none.</param>
    Task GoBackAsync(object? parameter);

    /// <summary>
    /// Opens a page of the site of the app in the browser.
    /// </summary>
    /// <param name="url">The production URL of the page, rooted at https://www.axantum.com.</param>
    Task OpenBrowserAsync(string url);

    /// <summary>
    /// Copies text, such as a password or a crash report, to the clipboard, marked as sensitive where the platform
    /// supports it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="confirm">True to tell the user that the text is copied, unless the platform does so, false
    /// otherwise.</param>
    Task SetSensitiveClipboardTextAsync(string text, bool confirm);
}
