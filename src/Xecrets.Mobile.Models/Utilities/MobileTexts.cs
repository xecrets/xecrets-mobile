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

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.Utilities;

public static class MobileTexts
{
    #region Untranslated texts

    public static string AboutPageThirdPartyNotice => "Built with .NET MAUI and CommunityToolkit.Maui. Other components are provided under their respective licenses, reproduced in full under Third-Party Licenses in the menu. All third-party product names and trademarks are the property of their respective owners. Their use does not imply affiliation, sponsorship or endorsement.";

    // Not AppTexts.ButtonClose, that is "Close all" and this closes the current file only.
    public static string LabelClose => "Close";

    public static string LabelContinue => "Continue";

    // The breadcrumb segments, here and in the translated region below, are joined by
    // BreadcrumbSeparator to form the trail shown at the top of the pages that are shared between
    // the encrypted and decrypt flows.
    public static string BreadcrumbCopyToShare => "Copy to share";

    public static string BreadcrumbHome => "Home";

    // "My folders"
    public static string BreadcrumbMyFolders => WorkFoldersContentTitle;

    public static string BreadcrumbPreview => "Preview";

    public static string BreadcrumbReceivedFile => "Received file";

    // "Recent files"
    public static string BreadcrumbRecentFiles => RecentFilesContentTitle;

    public static string BreadcrumbResult => "Result";

    public static string BreadcrumbSeparator => " › ";

    public static string ButtonDecryptAs => "Decrypt copy and…";

    public static string ButtonEncryptAs => "Encrypt copy and…";

    public static string ButtonEncryptToShare => "Encrypt copy with…";

    public static string ButtonOpenIn => "Open in…";

    public static string ButtonReport => "Report";

    public static string ButtonSave => "Save";

    public static string ButtonSendTo => "Send to…";

    public static string ButtonView => "View";

    public static string CrashPageAppleAdditionalInformation => "Apple may have additional crash information. You can report it manually or enable Share with App Developers under Analytics & Improvements.";

    public static string CrashPageExplanation => "There was a problem. We have copied the following log to the clipboard. Use the button to report it. Paste the log in the message.";

    public static string CrashPageTitle => "Sorry, we crashed!";

    public static string WorkFolderDescription => "Select or add a folder, then the file to encrypt or decrypt.";

    public static string DialogTextAddUnknownWorkFolder => "Do you want to add this folder?";

    public static string DialogTextFolderNoAccess => "The selected folder could not be accessed. Please select another folder.";

    public static string DialogTextSelectFolderFirst => "You selected a file in the folder. Select and add the folder first.";

    public static string DialogTextAlreadyEncrypted => "This file is already encrypted.";

    public static string DialogTextIncomingFileAccessDenied => "The app that sent this file did not grant access to it.";

    public static string DialogTextConfirmOverwrite => "The file \"{0}\" already exists. Overwrite it?";

    public static string DialogTextFileEncrypted => "The file was encrypted.";

    public static string DialogTextFileDecrypted => "The file was decrypted.";

    public static string DialogTextFileSaved => "The file was saved.";

    public static string DialogTextFileDeleted => "The file was deleted.";

    public static string DialogTextRecentFileNotFound => "The file no longer exists.";

    public static string DialogTextRecentFileNoAccess => "Access to the folder of this file has been lost. Add the folder again in \"my folders\".";

    public static string DialogTextCopiedToClipboard => "Copied to the clipboard.";

    public static string DialogTextExceptionFormat => "An unexpected error \"{0}\" occurred.";

    public static string DialogTextFolderName => "Enter a label for this folder";

    public static string DialogTextResult => "The file is only saved locally in the app. Choose an action for what to do with it next.";

    public static string DialogTextSelfHandoffRejected => "Xecrets Ez can't handle files from itself.";

    public static string EncryptToShareDescription => "Enter a separate password for the encrypted copy. Share the password through a different channel.";

    public static string HomeContentTitle => "Actions";

    public static string HomeDescription => "Choose an action. Work with files where they are stored in \"my folders\". Work with copies in the app and select what to do next. Encrypt a copy with a separate password.";

    public static string MenuInfo => "Xecrets home";

    // "Third-Party Licenses"
    public static string MenuThirdPartyLicenses => ThirdPartyLicensesPageTitle;

    // "Recent files"
    public static string MenuRecentFiles => RecentFilesContentTitle;

    public static string MenuXecretsDesktop => "Xecrets desktop";

    public static string RecentFilesContentTitle => "Recent files";

    public static string RecentFilesDecrypted => "Decrypted files";

    public static string RecentFilesDescription => "Files recently encrypted or decrypted in \"my folders\", most recent first. Choose to show decrypted or encrypted files, and use the lock button to encrypt or decrypt a file.";

    public static string RecentFilesEncrypted => "Encrypted files";

    public static string SuggestPasswordDescription => "Suggested passwords that are strong, and easy to type and remember. Use » for a new suggestion, and the copy button to copy it to the clipboard.";

    public static string ThirdPartyLicensesPageExplanation => "The application includes the following third-party material, under the terms reproduced here.";

    // Also used as the overflow menu item text, see MenuThirdPartyLicenses.
    public static string ThirdPartyLicensesPageTitle => "Third-party Licenses";

    public static string WorkFoldersContentTitle => "My folders";

    public static string MobileHelpUrl => "https://www.axantum.com/help/mobile";

    public static string MenuWelcome => "Welcome";

    // The first line is the heading. The placeholders such as "(c)" must never be translated, and must stay at the
    // start of their line. Each is replaced at runtime by an icon, making the line a bullet. The same placeholder may be
    // used more than once.
    public static string WelcomeText => """
        Welcome to Xecrets Ez

        Encrypt and decrypt your files on Windows, Linux, macOS, Android and iOS. On your phone you can:

        (c) Create an entirely local profile.
        (e) Encrypt and decrypt files in place.
        (f) Work with favorite folders.
        (v) Decrypt and share, view or edit files.
        (s) Encrypt and send or share files.
        (p) Suggest strong pronounceable passwords.
        (w) Securely overwrite and delete files.
        (o) Always work offline.

        ...and more! Get the desktop app to work with encrypted files there too.
        """;

    #endregion Untranslated texts

    public static IReadOnlyList<WelcomeLine> WelcomeLines(IReadOnlyDictionary<string, string> iconMap) =>
    [
        .. WelcomeText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ToWelcomeLine(line, iconMap)),
    ];

    private static WelcomeLine ToWelcomeLine(string line, IReadOnlyDictionary<string, string> iconMap)
    {
        foreach ((string placeholder, string icon) in iconMap)
        {
            if (line.StartsWith(placeholder, StringComparison.Ordinal))
            {
                return new WelcomeLine(icon, line[placeholder.Length..].Trim());
            }
        }

        return new WelcomeLine(string.Empty, line);
    }

    #region Translated texts

    public static string BreadcrumbDecrypt => AppTexts.ButtonDecrypt;

    public static string BreadcrumbEncrypt => AppTexts.ButtonEncrypt;

    // "Password"
    public static string BreadcrumbPassword => WatermarkPassword;

    // "Create"
    public static string ButtonCreate => AppTexts.LabelCreate;

    // "Edit"
    public static string ButtonEdit => AppTexts.ButtonEdit;

    public static string ButtonExit => AppTexts.ButtonExit;

    // "Help", also used as the overflow menu item text, see MenuHelp.
    public static string ButtonHelp => AppTexts.ButtonHelp;

    // "Ok"
    public static string ButtonOk => AppTexts.LabelOk;

    // "Save As…"
    public static string ButtonSaveAs => AppTexts.ButtonSaveAs.ToSentenceCase();

    public static string ButtonWipe => AppTexts.ButtonWipe;

    // "Add a decryption password"
    public static string DialogTextAddPassword => AppTexts.DialogTextAddPassword;

    public static string DialogTextInsufficientRights => AppTexts.DialogTextInsufficientRights;

    // "Canceled"
    public static string DialogTextOperationNotCompleted => AppTexts.FilesCanceledMessage;

    public static string DialogTextWrongPasswordOpen => AppTexts.DialogTextWrongPasswordOpen;

    // "Encrypt copy to share"
    public static string DialogTitleEncryptCopyFor => AppTexts.DialogTitleEncryptCopyFor;

    public static string DialogTitleSelectFilesToEncrypt => AppTexts.DialogTitleSelectFilesToEncrypt;

    public static string DialogTitleSelectFilesToWipe => AppTexts.DialogTitleSelectFilesToWipe;

    public static string DialogTitleSelectFileToOpen => AppTexts.DialogTitleSelectFileToOpen;

    public static string DialogValidationAlreadySignedIn => AppTexts.DialogValidationAlreadySignedIn;

    public static string DialogValidationConfirmPassword => AppTexts.DialogValidationConfirmPassword;

    public static string DialogValidationInvalidEmail => AppTexts.DialogValidationInvalidEmail;

    public static string DialogValidationWrongPassword => AppTexts.DialogValidationWrongPassword;

    public static string DisplayNameProgram => AppTexts.DisplayNameProgram;

    // "Don't show this again"
    public static string DontShowAgain => AppTexts.DontShowAgain;

    public static string FileEncryptionUrl => AppTexts.FileEncryptionUrl;

    // "Set up a local profile"
    public static string HeadingCreateUserFirstTimeSetup => AppTexts.HeadingCreateUserFirstTimeSetup;

    public static string LabelCancel => AppTexts.LabelCancel;

    public static string LabelNo => AppTexts.LabelNo;

    public static string LabelYes => AppTexts.LabelYes;

    // "About"
    public static string MenuAbout => AppTexts.ButtonAbout;

    public static string MenuDebug => AppTexts.MenuDebug;

    // "Suggest password", also used as the page title.
    public static string MenuSuggestPassword => AppTexts.DialogTitleSuggestPassword;

    // "Help"
    public static string MenuHelp => ButtonHelp;

    public static string MessageTextConfirmWipe => AppTexts.MessageTextConfirmWipe;

    // "Sign in to Xecrets Ez"
    public static string SignInHeading => AppTexts.SignInHeading;

    public static string SiteUrl => AppTexts.SiteUrl;

    // "Adding a password is only for decryption, when you receive files from someone else, or you have used another
    // password previously. It does not affect encryption."
    public static string ToolTipAddPassword => AppTexts.ToolTipAddPassword;

    // "Not used for sending emails. It's an identifier for your local profile in the software and for sharing with others."
    public static string ToolTipEmail => AppTexts.ToolTipEmail;

    // "Your master password. Make it strong, write it down and keep it safe."
    public static string ToolTipMasterPassword => AppTexts.ToolTipMasterPassword;

    // "Signing in ensures you are using the correct password for encryption, and that you don't need to retype the
    // password each time. This is the master password for all encryption. The sign in is only local to the app."
    public static string ToolTipSignIn => AppTexts.ToolTipSignIn;

    // "Confirm password"
    public static string WatermarkConfirmPassword => AppTexts.WatermarkConfirmPassword;

    // "Email"
    public static string WatermarkEmail => AppTexts.WatermarkEmail;

    // "Password"
    public static string WatermarkPassword => AppTexts.WatermarkPassword;

    // "Password to share"
    public static string WatermarkPasswordShare => AppTexts.WatermarkPasswordShare;

    public static string XecretsHelpUrl => AppTexts.XecretsHelpUrl();

    #endregion Translated texts
}
