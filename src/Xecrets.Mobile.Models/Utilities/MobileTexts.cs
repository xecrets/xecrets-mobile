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
using Xecrets.Texts;

using AppTexts = Xecrets.Texts.Texts;

namespace Xecrets.Mobile.Models.Utilities;

public static class MobileTexts
{
    #region Untranslated texts

    public static string AboutPageThirdPartyNotice => "Built with .NET MAUI and CommunityToolkit.Maui. Other components are provided under their respective licenses, reproduced in full under Third-party licenses in the menu. All third-party product names and trademarks are the property of their respective owners. Their use does not imply affiliation, sponsorship or endorsement.";

    // The breadcrumb segments, here and in the translated region below, are joined by
    // BreadcrumbSeparator to form the trail shown at the top of the pages that are shared between
    // the encrypted and decrypt flows.

    // "Edit"
    public static string BreadcrumbEdit => EditContentTitle;

    public static string BreadcrumbEncryptedCopy => "Encrypted copy";

    public static string BreadcrumbEncryptToShare => ButtonEncryptToShare.StripEllipsis();

    public static string BreadcrumbHome => "Home";

    // "My folders"
    public static string BreadcrumbMyFolders => WorkFoldersContentTitle;

    // "Preview"
    public static string BreadcrumbPreview => PreviewContentTitle;

    public static string BreadcrumbReceivedFile => "Received file";

    // "Recent files"
    public static string BreadcrumbRecentFiles => RecentFilesContentTitle;

    public static string BreadcrumbSeparator => " › ";

    // "View"
    public static string BreadcrumbView => ViewContentTitle;

    // "Overwrite and delete"
    public static string BreadcrumbWipe => ButtonWipe;

    public static string ButtonBrowseElsewhere => "Browse anywhere";

    public static string ButtonDiscard => "Discard";

    public static string ButtonEncryptToShare => "Encrypt to share";

    public static string ButtonOpenIn => "Open in…";

    public static string ButtonReport => "Report";

    public static string ButtonSave => "Save";

    public static string ButtonSendTo => "Send to…";

    public static string ButtonView => "View";

    public static string ButtonWipe => "Overwrite and delete";

    public static string ChangeProfileDescription => "Change the email and password of your local profile. Files encrypted before can still be opened.";

    public static string CrashPageAppleAdditionalInformation => "Apple may have additional crash information. You can report it manually or enable Share with App Developers under Analytics & Improvements.";

    public static string CrashPageExplanation => "There was a problem. We have copied the following log to the clipboard. Use the button to report it. Paste the log into the message.";

    public static string CrashPageTitle => "Sorry, we crashed!";

    public static string DialogTextAccessDeniedOrNotSupported => "Access denied or not supported.";

    public static string DialogTextAllowFolderAccessFormat => "Do you want to allow the app to access the folder where \"{0}\" is located?";

    public static string DialogTextAlreadyEncrypted => "This file is already encrypted.";

    public static string DialogTextConfirmOverwrite => "The file \"{0}\" already exists. Overwrite it?";

    public static string DialogTextBackupNameInUse => "Not saved, since there is already a file named \"{0}\" in the same folder. Remove it, and save again.";

    public static string DialogTextOriginalNotRemoved => "The operation finished, but the original could not be removed.";

    public static string DialogTextCopiedToClipboard => "Copied to the clipboard.";

    public static string DialogTextExceptionFormat => "An unexpected error \"{0}\" occurred.";

    public static string DialogTextFileDecrypted => "The file was decrypted.";

    public static string DialogTextFileDeleted => "The file was only deleted, not overwritten. It may still be recoverable.";

    public static string DialogTextFileEncrypted => "The file was encrypted.";

    public static string DialogTextFileNotAccessibleNow => "The file was not found or is not available right now.";

    public static string DialogTextFileOverwritten => "The file was overwritten and deleted. It may still be recoverable.";

    public static string DialogTextFileSaved => "The file was saved.";

    public static string DialogTextSavedAs => "The file was saved as \"{0}\".";

    public static string DialogTextFolderName => "Enter a label for this folder";

    public static string DialogTextFolderNoAccess => "The folder could not be accessed. Please select another folder.";

    public static string DialogTextIncomingFileAccessDenied => "Access to the file was denied.";

    public static string DialogTextNoAppToOpenFile => "The file could not be opened.";

    public static string DialogTextNotEncrypted => "This file is not encrypted.";

    public static string DialogTextProfileChanged => "Your profile was changed.";

    public static string DialogTextResult => "The file is only saved temporarily in the app. Choose what to do with it next.";

    public static string DialogTextSaveChanges => "Save changes before leaving?";

    public static string DialogTextSelectFolderFirst => "You selected a file. Select and add its folder first.";

    public static string DialogTextSelfHandoffRejected => "The app can't handle files from itself.";

    // "Edit"
    public static string EditContentTitle => ButtonEdit;

    public static string EditDescription => "Edit the text. Click the save icon to encrypt and save changes.";

    public static string EncryptResultContentTitle => "Copy to share";

    // "Password"
    public static string EncryptToShareContentTitle => WatermarkPassword;

    public static string EncryptToShareDescription =>"Enter a new password for the encrypted copy. Share the password through a different channel.";

    public static string FileSizeFormat => "{0} bytes";

    public static string FileStateAll => "All";

    public static string FileStateDecrypted => "Decrypted";

    public static string FileStateEncrypted => "Encrypted";

    // An ellipsis rather than a word, to save space in the segmented control.
    public static string FileStateOther => "…";

    public static string GitHubUrl => "https://github.com/xecrets/xecrets-mobile";

    public static string HomeButtonDelete => "Delete";

    public static string HomeButtonEncryptDecryptCopy => "Encrypt / Decrypt: Copy";

    public static string HomeButtonEncryptDecryptOriginal => "Encrypt / Decrypt: Original";

    public static string HomeContentTitle => "Actions";

    public static string HomeDescription => "Choose an action. Encrypt or decrypt a file where it is stored, replacing the original, or work with a temporary copy in the app and select what to do next. Encrypt a file with a new password to share.";

    public static string HomeDetailCopy => "Work with a copy in the app";

    public static string HomeDetailDelete => "When supported also overwrite";

    public static string HomeDetailRecentFiles => "Quick access to recent operations";

    public static string HomeDetailReplace => "Replace the original file";

    public static string HomeDetailShare => "Share a file with a password";

    public static string LabelContinue => "Continue";

    public static string MenuChangeProfile => "Change local profile";

    public static string MenuGitHub => "View source";

    public static string MenuInfo => "Xecrets home";

    // "Recent files"
    public static string MenuRecentFiles => RecentFilesContentTitle;

    public static string MenuSupport => "Support";

    // "Third-party licenses"
    public static string MenuThirdPartyLicenses => ThirdPartyLicensesPageTitle;

    public static string MenuWelcome => "Welcome";

    public static string MenuXecretsDesktop => "Xecrets desktop";

    public static string MobileHelpUrl => "https://www.axantum.com/help/mobile";

    public static string PreviewContentTitle => "Preview";

    public static string RecentFileNameUnknown => "???";

    public static string RecentFilesContentTitle => "Recent files";

    public static string RecentFilesDescription => "Recently used files. Click the icon to choose an action. Click the file name to preview. Long-press to see the full name and path. Use the add button to add a file from \"My folders\".";

    public static string SuggestPasswordDescription => "Suggested passwords that are strong and easy to type and remember. Use » for a new suggestion, and the copy button to copy it to the clipboard.";

    public static string ThirdPartyLicensesPageExplanation => "The app includes the following third-party material, under the terms reproduced here.";

    // Also used as the overflow menu item text, see MenuThirdPartyLicenses.
    public static string ThirdPartyLicensesPageTitle => "Third-party licenses";

    // "View"
    public static string ViewContentTitle => ButtonView;

    public static string VerifyPasswordDescription => "Enter your current password to confirm that it's you.";

    public static string VerifyPasswordHeading => "Verify your password";

    public static string ViewDescription => "Shows the text or image in the file. Go back to choose something else to do with it.";

    public static string WorkFolderDescription => "Add a folder to allow the app access to it, then click a folder button to encrypt or decrypt a file. Some folders are not supported. Use \"Browse anywhere\" for a file in any other place, and save the result where you choose.";

    public static string WorkFolderIntentDelete => "Choose the file to overwrite and delete.";

    public static string WorkFoldersContentTitle => "My folders";

    // The first line is the heading. The placeholders such as "(c)" must never be translated, and must stay at the
    // start of their line. Each is replaced at runtime by an icon, making the line a bullet. The same placeholder may be
    // used more than once.
    private static string WelcomeText =>
        """
        Welcome to Xecrets Ez

        Encrypt and decrypt your files on Windows, Linux, macOS, Android and iOS.

        (c) Create an entirely local profile.
        (e) Encrypt and decrypt files in place.
        (f) Allow access to favorite folders.
        (v) Decrypt and share, view or edit files.
        (s) Encrypt and send or share files.
        (p) Suggest strong pronounceable passwords.
        (w) Overwrite and delete files (when supported).
        (o) Always work offline.

        …and more! Get the desktop app too.

        Note: Check the trash or other recovery options when deleting cloud files!
        """;

    #endregion Untranslated texts

    internal static IReadOnlyList<WelcomeLine> WelcomeLines(IReadOnlyDictionary<string, string> iconMap) =>
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

        return new WelcomeLine("", line);
    }

    #region Translated texts

    // "Password"
    public static string BreadcrumbPassword => WatermarkPassword;

    // "Suggest password"
    public static string BreadcrumbSuggestPassword => MenuSuggestPassword;

    // "Create"
    public static string ButtonCreate => AppTexts.LabelCreate;

    public static string ButtonDecrypt => AppTexts.ButtonDecrypt;

    // "Edit"
    public static string ButtonEdit => AppTexts.ButtonEdit;

    public static string ButtonEncrypt => AppTexts.ButtonEncrypt;

    public static string ButtonExit => AppTexts.ButtonExit;

    // "Help", also used as the overflow menu item text, see MenuHelp.
    public static string ButtonHelp => AppTexts.ButtonHelp;

    // "Ok"
    public static string ButtonOk => AppTexts.LabelOk;

    // "Save As…"
    public static string ButtonSaveAs => AppTexts.ButtonSaveAs.ToSentenceCase();

    // "Add a decryption password"
    public static string DialogTextAddPassword => AppTexts.DialogTextAddPassword;

    public static string DialogTextInsufficientRights => AppTexts.DialogTextInsufficientRights;

    // "Canceled"
    public static string DialogTextOperationNotCompleted => AppTexts.FilesCanceledMessage;

    public static string DialogTextWrongPasswordOpen => AppTexts.DialogTextWrongPasswordOpen;

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

    // "Help"
    public static string MenuHelp => ButtonHelp;

    // "Suggest password", also used as the page title.
    public static string MenuSuggestPassword => AppTexts.DialogTitleSuggestPassword;

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

    public static string XecretsSupportUrl => AppTexts.XecretsSupportUrl;

    #endregion Translated texts
}
