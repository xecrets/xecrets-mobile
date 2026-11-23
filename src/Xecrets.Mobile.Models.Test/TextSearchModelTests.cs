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

using Xecrets.Core.Models;
using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.PageModels;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class TextSearchModelTests
{
    [Test]
    public void OpenShowsTheSearchWithNothingFound()
    {
        TextSearchModel search = new();
        search.SetText("abc");

        search.OpenCommand.Execute(null);

        Assert.That(search.IsOpen, Is.True);
        Assert.That(search.Result.Starts, Is.Empty);
        Assert.That(search.CountText, Is.Empty);
        Assert.That(search.FindNextCommand.CanExecute(null), Is.False);
        Assert.That(search.FindPreviousCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void AllMatchesAreFoundIgnoringCaseAndTheFirstIsCurrent()
    {
        TextSearchModel search = Create("Apple, apple and APPLE pie");

        search.SearchText = "aPPle";

        Assert.That(search.Result.Starts, Is.EqualTo([0, 7, 17]));
        Assert.That(search.Result.Length, Is.EqualTo(5));
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(0));
        Assert.That(search.CountText, Is.EqualTo(string.Format(MobileTexts.TextSearchMatchOfCountFormat, 1, 3)));
        Assert.That(search.FindNextCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void MatchesDoNotOverlap()
    {
        TextSearchModel search = Create("aaaaa");

        search.SearchText = "aa";

        Assert.That(search.Result.Starts, Is.EqualTo([0, 2]));
    }

    [Test]
    public void NoMatchesIsTold()
    {
        TextSearchModel search = Create("abc");

        search.SearchText = "x";

        Assert.That(search.Result.Starts, Is.Empty);
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(-1));
        Assert.That(search.CountText, Is.EqualTo(MobileTexts.TextSearchNoMatches));
        Assert.That(search.FindNextCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void NextAndPreviousWrapAround()
    {
        TextSearchModel search = Create("x x x");
        search.SearchText = "x";

        search.FindNextCommand.Execute(null);
        search.FindNextCommand.Execute(null);
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(2));

        search.FindNextCommand.Execute(null);
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(0));

        search.FindPreviousCommand.Execute(null);
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(2));
        Assert.That(search.CountText, Is.EqualTo(string.Format(MobileTexts.TextSearchMatchOfCountFormat, 3, 3)));
    }

    [Test]
    public void MovingToTheOnlyMatchIsStillTold()
    {
        TextSearchModel search = Create("abc");
        search.SearchText = "b";
        TextSearchResult before = search.Result;

        search.FindNextCommand.Execute(null);

        Assert.That(search.Result, Is.Not.SameAs(before));
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(0));
    }

    [Test]
    public void ChangingTheSearchKeepsTheCurrentMatchOrTheNextOne()
    {
        TextSearchModel search = Create("ab ac ab ac");
        search.SearchText = "a";
        search.FindNextCommand.Execute(null);

        search.SearchText = "ab";
        Assert.That(search.Result.Starts, Is.EqualTo([0, 6]));
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(1));

        search.SearchText = "ac";
        Assert.That(search.Result.Starts, Is.EqualTo([3, 9]));
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(1));
    }

    [Test]
    public void CorrectingASearchThatFindsNothingReturnsToTheCurrentMatch()
    {
        TextSearchModel search = Create("ab ab ab");
        search.SearchText = "a";
        search.FindNextCommand.Execute(null);

        search.SearchText = "ax";
        search.SearchText = "ab";

        Assert.That(search.Result.CurrentIndex, Is.EqualTo(1));
    }

    [Test]
    public void ChangingTheSearchPastTheLastMatchWrapsToTheFirst()
    {
        TextSearchModel search = Create("ab ac ab");
        search.SearchText = "a";
        search.FindPreviousCommand.Execute(null);

        search.SearchText = "ac";

        Assert.That(search.Result.CurrentIndex, Is.EqualTo(0));
    }

    [Test]
    public void ChangingTheTextSearchesAgain()
    {
        TextSearchModel search = Create("one two one");
        search.SearchText = "one";
        search.FindNextCommand.Execute(null);

        search.SetText("one two one one");

        Assert.That(search.Result.Starts, Is.EqualTo([0, 8, 12]));
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(1));
    }

    [Test]
    public void ChangingTheTextWithoutASearchFindsNothing()
    {
        TextSearchModel search = Create("abc");

        search.SetText("abcabc");

        Assert.That(search.Result.Starts, Is.Empty);
        Assert.That(search.CountText, Is.Empty);
    }

    [Test]
    public void ClosingHidesTheMatchesAndKeepsTheSearch()
    {
        TextSearchModel search = Create("abc");
        search.SearchText = "b";

        search.CloseCommand.Execute(null);

        Assert.That(search.IsOpen, Is.False);
        Assert.That(search.SearchText, Is.EqualTo("b"));
        Assert.That(search.Result.Starts, Is.Empty);
        Assert.That(search.CountText, Is.Empty);
    }

    [Test]
    public void OpeningAgainSearchesTheCurrentTextFromTheCurrentMatch()
    {
        TextSearchModel search = Create("ab ab ab");
        search.SearchText = "b";
        search.FindNextCommand.Execute(null);
        search.CloseCommand.Execute(null);

        search.SetText("ab ab ab ab");
        search.OpenCommand.Execute(null);

        Assert.That(search.Result.Starts, Is.EqualTo([1, 4, 7, 10]));
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(1));
        Assert.That(search.CountText, Is.EqualTo(string.Format(MobileTexts.TextSearchMatchOfCountFormat, 2, 4)));
    }

    [Test]
    public void NothingIsSearchedWhileClosed()
    {
        TextSearchModel search = Create("abc");
        search.SearchText = "b";
        search.CloseCommand.Execute(null);

        search.SetText("abcabc");

        Assert.That(search.Result.Starts, Is.Empty);
    }

    [Test]
    public void EmptySearchFindsNothing()
    {
        TextSearchModel search = Create("abc");
        search.SearchText = "b";

        search.SearchText = "";

        Assert.That(search.Result.Starts, Is.Empty);
        Assert.That(search.Result.CurrentIndex, Is.EqualTo(-1));
        Assert.That(search.CountText, Is.Empty);
    }

    [Test]
    public async Task EditingTheTextClosesTheSearchAndOpeningItSearchesTheEditedText()
    {
        using TestApp app = new();
        app.Preview.Current.SetText(
            new DecryptedFileInfo("", "a.txt", "text/plain", 4, PreviewKind.Text, FileTimes.UtcNow),
            "text"u8.ToArray(),
            EncryptedWithParameters.Empty);
        EditPageModel page = app.CreateEditPage();
        await page.InitializeCommand.ExecuteAsync(null);
        page.Search.OpenCommand.Execute(null);
        page.Search.SearchText = "t";

        page.Text = "text text";

        Assert.That(page.Search.IsOpen, Is.False);
        Assert.That(page.Search.Result.Starts, Is.Empty);

        page.Search.OpenCommand.Execute(null);

        Assert.That(page.Search.Result.Starts, Is.EqualTo([0, 3, 5, 8]));
    }

    private static TextSearchModel Create(string text)
    {
        TextSearchModel search = new();
        search.SetText(text);
        search.OpenCommand.Execute(null);
        return search;
    }
}
