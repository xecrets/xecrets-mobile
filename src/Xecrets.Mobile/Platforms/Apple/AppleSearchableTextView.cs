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
using System.ComponentModel;
using System.Threading.Tasks;

using CoreGraphics;

using Foundation;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;

using UIKit;

using Xecrets.Mobile.Abstractions;
using Xecrets.Mobile.Models.Models;

namespace Xecrets.Mobile.Platforms.Apple;

/// <summary>
/// The searchable text of an editor that scrolls its text itself, as on iOS and Mac Catalyst. The positions in the text
/// view are those of its content, which is the text, since the origin of its bounds is the offset its content is
/// scrolled by.
/// </summary>
/// <remarks>
/// The text view uses TextKit 2, since nothing has it fall back to TextKit 1. The text is said to be laid out again
/// only when the size or the font of the editor changes. The text view lays out
/// only the text near what is visible and estimates where the rest is, so the height of its content changes while it
/// is scrolled, by a few lines, which is not taken as laying out the text again.
/// </remarks>
internal sealed class AppleSearchableTextView : ISearchableTextView
{
    private readonly Editor _editor;

    private readonly UITextView _textView;

    private readonly IDisposable _contentOffsetObserver;

    private bool _isScrolled;

    private bool _isLaidOut;

    private bool _areEventsPosted;

    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleSearchableTextView"/> class.
    /// </summary>
    /// <param name="editor">The laid out editor.</param>
    public AppleSearchableTextView(Editor editor)
    {
        _editor = editor;
        _textView = (UITextView)editor.Handler!.PlatformView!;
        _contentOffsetObserver = _textView.AddObserver(
            "contentOffset",
            NSKeyValueObservingOptions.New,
            OnContentOffsetChanged);
        _editor.SizeChanged += OnEditorSizeChanged;
        _editor.PropertyChanged += OnEditorPropertyChanged;
    }

    public event EventHandler? Scrolled;

    public event EventHandler? LayoutChanged;

    public double Top => _textView.ContentOffset.Y;

    public double Height => _textView.Bounds.Height;

    public double TextHeight => _textView.ContentSize.Height;

    /// <remarks>
    /// The text view only lays out the text near what is visible, and only estimates where the rest is. After a long
    /// jump to an estimated position, it may draw other text there than the estimate has, so for a character that is not
    /// drawn, the text is laid out from the character first, and scrolled to where that puts it, which is then drawn.
    /// </remarks>
    public Task ScrollToLineAsync(int index, double room)
    {
        if (!IsDrawn(index))
        {
            NSTextLayoutManager layoutManager = _textView.TextLayoutManager!;
            NSTextContentManager contentManager = layoutManager.TextContentManager!;
            ScrollTo(layoutManager.TextViewportLayoutController.RelocateViewport(
                contentManager.GetLocation(contentManager.DocumentRange.Location, index)!));
        }

        ScrollTo(GetLineBounds(index).Top - room);
        return Task.CompletedTask;
    }

    /// <remarks>
    /// The text drawn is that of the viewport of the layout, which there is none of before the text is laid out.
    /// </remarks>
    public bool IsDrawn(int index)
    {
        NSTextLayoutManager layoutManager = _textView.TextLayoutManager!;
        if (layoutManager.TextViewportLayoutController.ViewportRange is not { } viewportRange)
        {
            return false;
        }

        NSTextContentManager contentManager = layoutManager.TextContentManager!;
        INSTextLocation documentStart = contentManager.DocumentRange.Location;
        return index >= contentManager.GetOffset(documentStart, viewportRange.Location) &&
            index < contentManager.GetOffset(documentStart, viewportRange.EndLocation);
    }

    public (double Top, double Bottom) GetLineBounds(int index)
    {
        CGRect caret = _textView.GetCaretRectForPosition(_textView.GetPosition(_textView.BeginningOfDocument, index)!);
        return (caret.Top, caret.Bottom);
    }

    /// <remarks>
    /// The positions of text that is not drawn are only estimates, which need not agree with the text that is drawn, so
    /// the paragraphs are gone through from the text drawn toward the position, and laid out on the way, which puts each
    /// next to the one before. The paragraphs are positioned in the text container, which is inset in the text view.
    /// </remarks>
    public int GetLineStart(double y)
    {
        NSTextLayoutManager layoutManager = _textView.TextLayoutManager!;
        NSTextContentManager contentManager = layoutManager.TextContentManager!;
        INSTextLocation documentStart = contentManager.DocumentRange.Location;
        INSTextLocation from = layoutManager.TextViewportLayoutController.ViewportRange!.Location;
        double containerY = y - _textView.TextContainerInset.Top;
        bool isAbove = containerY < layoutManager.GetTextLayoutFragment(from)!.LayoutFragmentFrame.Top;
        int lineStart = isAbove ? 0 : (int)contentManager.GetOffset(documentStart, contentManager.DocumentRange.EndLocation);
        layoutManager.EnumerateTextLayoutFragments(
            from,
            isAbove
                ? NSTextLayoutFragmentEnumerationOptions.EnsuresLayout | NSTextLayoutFragmentEnumerationOptions.Reverse
                : NSTextLayoutFragmentEnumerationOptions.EnsuresLayout,
            fragment =>
            {
                CGRect frame = fragment.LayoutFragmentFrame;
                if (isAbove ? containerY < frame.Top : containerY >= frame.Bottom)
                {
                    return true;
                }

                int fragmentStart = (int)contentManager.GetOffset(documentStart, fragment.RangeInElement.Location);
                foreach (NSTextLineFragment line in fragment.TextLineFragments)
                {
                    lineStart = fragmentStart + (int)line.CharacterRange.Location;
                    if (containerY < frame.Top + line.TypographicBounds.Bottom)
                    {
                        break;
                    }
                }

                return false;
            });
        return lineStart;
    }

    /// <remarks>
    /// The highlights are rendering attributes of the layout, which are only drawn, so that the text storage is not
    /// edited. Editing it would have the text view lay out the text again, which moves the text whose layout it only
    /// estimates, and scroll to its caret if it is being edited. The text view does not draw the text in view again
    /// when only its rendering attributes change, so its layout is invalidated, which is only the text near what is
    /// visible. It is laid out again at once, rather than before the screen is next drawn, so that positions got after
    /// this are where it is drawn.
    /// </remarks>
    public void HighlightMatches(
        TextSearchResult result,
        int first,
        int end,
        Color matchColor,
        Color currentMatchColor)
    {
        NSTextLayoutManager layoutManager = _textView.TextLayoutManager!;
        NSTextContentManager contentManager = layoutManager.TextContentManager!;
        NSTextRange documentRange = contentManager.DocumentRange;
        layoutManager.RemoveRenderingAttribute(UIStringAttributeKey.BackgroundColor, documentRange);

        UIColor platformMatchColor = matchColor.ToPlatform();
        UIColor platformCurrentMatchColor = currentMatchColor.ToPlatform();
        for (int index = first; index < end; index++)
        {
            INSTextLocation start = contentManager.GetLocation(documentRange.Location, result.Starts[index])!;
            layoutManager.AddRenderingAttribute(
                UIStringAttributeKey.BackgroundColor,
                index == result.CurrentIndex ? platformCurrentMatchColor : platformMatchColor,
                new NSTextRange(start, contentManager.GetLocation(start, result.Length)!));
        }

        layoutManager.InvalidateLayout(layoutManager.TextViewportLayoutController.ViewportRange!);
        _textView.SetNeedsLayout();
        _textView.LayoutIfNeeded();
    }

    public void Dispose()
    {
        _contentOffsetObserver.Dispose();
        _editor.SizeChanged -= OnEditorSizeChanged;
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _isDisposed = true;
    }

    private void ScrollTo(double top)
    {
        UIEdgeInsets inset = _textView.AdjustedContentInset;
        double maximum = Math.Max(
            -inset.Top,
            _textView.ContentSize.Height + inset.Bottom - _textView.Bounds.Height);
        _textView.SetContentOffset(new CGPoint(0, Math.Clamp(top, -inset.Top, maximum)), false);

        // The text view only lays out the text in view, and estimates where the rest is, so the text now in view is
        // laid out, which also moves it to where it is laid out, before positions in it are got.
        _textView.LayoutIfNeeded();
    }

    private void OnContentOffsetChanged(NSObservedChange change)
    {
        _isScrolled = true;
        PostEvents();
    }

    private void OnEditorSizeChanged(object? sender, EventArgs e) => OnLaidOut();

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == Editor.FontSizeProperty.PropertyName ||
            e.PropertyName == Editor.FontFamilyProperty.PropertyName ||
            e.PropertyName == Editor.FontAttributesProperty.PropertyName)
        {
            OnLaidOut();
        }
    }

    private void OnLaidOut()
    {
        _isLaidOut = true;
        PostEvents();
    }

    // The text view sets its content offset while it scrolls and lays out its text, when its text storage must not be
    // edited, as the handlers of the events do, and the font of the editor may change before that of the text view.
    // The events are raised once that is done, and once for all the changes until then.
    private void PostEvents()
    {
        if (!_areEventsPosted)
        {
            _areEventsPosted = true;
            _textView.BeginInvokeOnMainThread(RaiseEvents);
        }
    }

    // A layout change is raised alone, since the text is then also where it is scrolled to.
    private void RaiseEvents()
    {
        _areEventsPosted = false;

        // The events may have been posted before the viewport was disposed.
        if (_isDisposed)
        {
            return;
        }

        bool isScrolled = _isScrolled;
        bool isLaidOut = _isLaidOut;
        _isScrolled = false;
        _isLaidOut = false;
        if (isLaidOut)
        {
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (isScrolled)
        {
            Scrolled?.Invoke(this, EventArgs.Empty);
        }
    }
}
