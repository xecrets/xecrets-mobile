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

using NUnit.Framework;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class TextEditorFontModelTests
{
    [Test]
    public async Task MissingSettingsMeanMonospaceAtTheDefaultSize()
    {
        TextEditorFontModel font = await LoadAsync(new FakeProfileService());

        Assert.That(font.IsProportionalFont, Is.False);
        Assert.That(font.FontSize, Is.EqualTo(16));
    }

    [Test]
    public async Task SettingsAreLoaded()
    {
        FakeProfileService profile = new();
        profile.TextEditor.FontSize = 20;
        profile.TextEditor.ProportionalFont = true;

        TextEditorFontModel font = await LoadAsync(profile);

        Assert.That(font.IsProportionalFont, Is.True);
        Assert.That(font.FontSize, Is.EqualTo(20));
    }

    [Test]
    public async Task SizeOutsideTheRangeIsLimited()
    {
        FakeProfileService profile = new();
        profile.TextEditor.FontSize = 48;

        TextEditorFontModel font = await LoadAsync(profile);

        Assert.That(font.FontSize, Is.EqualTo(32));
        Assert.That(font.IncreaseFontSizeCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task IncreaseAndDecreaseAreSaved()
    {
        FakeProfileService profile = new();
        TextEditorFontModel font = await LoadAsync(profile);

        await font.IncreaseFontSizeCommand.ExecuteAsync(null);
        Assert.That(profile.TextEditor.FontSize, Is.EqualTo(18));

        await font.DecreaseFontSizeCommand.ExecuteAsync(null);
        await font.DecreaseFontSizeCommand.ExecuteAsync(null);
        Assert.That(font.FontSize, Is.EqualTo(14));
        Assert.That(profile.TextEditor.FontSize, Is.EqualTo(14));
    }

    [Test]
    public async Task SizeStopsAtTheLimits()
    {
        FakeProfileService profile = new();
        profile.TextEditor.FontSize = 12;
        TextEditorFontModel font = await LoadAsync(profile);

        await font.DecreaseFontSizeCommand.ExecuteAsync(null);

        Assert.That(font.FontSize, Is.EqualTo(10));
        Assert.That(font.DecreaseFontSizeCommand.CanExecute(null), Is.False);
        Assert.That(font.IncreaseFontSizeCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task ToggleIsSaved()
    {
        FakeProfileService profile = new();
        TextEditorFontModel font = await LoadAsync(profile);

        await font.ToggleProportionalFontCommand.ExecuteAsync(null);
        Assert.That(font.IsProportionalFont, Is.True);
        Assert.That(profile.TextEditor.ProportionalFont, Is.True);

        await font.ToggleProportionalFontCommand.ExecuteAsync(null);
        Assert.That(font.IsProportionalFont, Is.False);
        Assert.That(profile.TextEditor.ProportionalFont, Is.False);
    }

    [Test]
    public async Task EditPageLoadsTheFont()
    {
        string path = Path.GetTempFileName();
        try
        {
            TestApp app = new();
            app.Profile.TextEditor.ProportionalFont = true;
            app.Preview.Current.SetText(new DecryptedFileInfo(path, "a.txt", "text/plain", 4, PreviewKind.Text), "text");
            EditPageModel page = app.CreateEditPage();

            await page.InitializeCommand.ExecuteAsync(null);

            Assert.That(page.Font.IsProportionalFont, Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<TextEditorFontModel> LoadAsync(FakeProfileService profile)
    {
        TextEditorFontModel font = new(profile);
        await font.LoadAsync();
        return font;
    }
}
