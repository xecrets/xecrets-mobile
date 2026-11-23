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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.PageModels;

/// <summary>
/// A search of the text of the text editor, ignoring case, which only finds matches while it is open. It is kept only
/// in memory, for as long as the page model it belongs to, so that it is the same when opened again.
/// </summary>
public partial class TextSearchModel : ObservableObject
{
    private string _text = "";

    // Where the current match starts, kept while nothing is found, so that correcting the search returns there.
    private int _currentStart;

    /// <summary>
    /// True while the search is shown, false otherwise.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    public partial bool IsOpen { get; set; }

    /// <summary>
    /// The text searched for, or "" for no search.
    /// </summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>
    /// The matches of the search, a new instance after each search and each move to another match.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand), nameof(FindPreviousCommand))]
    public partial TextSearchResult Result { get; private set; } = TextSearchResult.None;

    /// <summary>
    /// The position of the current match and the number of matches, that there are no matches, or "" when there is no
    /// search or the search is closed.
    /// </summary>
    public string CountText => !IsOpen || SearchText.Length == 0
        ? ""
        : Result.CurrentIndex < 0
            ? MobileTexts.TextSearchNoMatches
            : string.Format(MobileTexts.TextSearchMatchOfCountFormat, Result.CurrentIndex + 1, Result.Starts.Count);

    /// <summary>
    /// Sets the text to search, and searches it again if the search is open, keeping the current match, or the one after
    /// it, as current.
    /// </summary>
    /// <param name="text">The text to search.</param>
    public void SetText(string text)
    {
        _text = text;
        Search();
    }

    partial void OnSearchTextChanged(string value) => Search();

    partial void OnIsOpenChanged(bool value) => Search();

    [RelayCommand]
    private void Open() => IsOpen = true;

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void FindNext() => MoveTo((Result.CurrentIndex + 1) % Result.Starts.Count);

    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void FindPrevious() => MoveTo((Result.CurrentIndex - 1 + Result.Starts.Count) % Result.Starts.Count);

    private bool HasMatches() => Result.Starts.Count > 0;

    private void MoveTo(int index)
    {
        _currentStart = Result.Starts[index];
        Result = new TextSearchResult(Result.Starts, Result.Length, index);
    }

    // Ordinal, so that every match has the length of the text searched for, and matches never overlap.
    private void Search()
    {
        if (!IsOpen || SearchText.Length == 0)
        {
            Result = TextSearchResult.None;
            return;
        }

        List<int> starts = [];
        for (int start = _text.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase);
             start >= 0;
             start = _text.IndexOf(SearchText, start + SearchText.Length, StringComparison.OrdinalIgnoreCase))
        {
            starts.Add(start);
        }

        int currentIndex = starts.BinarySearch(_currentStart);
        if (currentIndex < 0)
        {
            currentIndex = ~currentIndex;
        }

        if (currentIndex == starts.Count)
        {
            currentIndex = starts.Count == 0 ? -1 : 0;
        }

        if (currentIndex >= 0)
        {
            _currentStart = starts[currentIndex];
        }

        Result = new TextSearchResult(starts, SearchText.Length, currentIndex);
    }
}
