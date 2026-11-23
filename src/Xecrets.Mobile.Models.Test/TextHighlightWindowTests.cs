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

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class TextHighlightWindowTests
{
    [Test]
    public void TheWindowExtendsAVisibleHeightAboveAndBelow()
    {
        TextHighlightWindow window = TextHighlightWindow.Around(1000, 1500, 10000);

        Assert.That(window, Is.EqualTo(new TextHighlightWindow(500, 2000)));
    }

    [Test]
    public void TheWindowStaysWithinTheText()
    {
        Assert.That(TextHighlightWindow.Around(100, 600, 800), Is.EqualTo(new TextHighlightWindow(0, 800)));
    }

    [Test]
    public void ScrollingWithinTheMarginStaysCovered()
    {
        TextHighlightWindow window = TextHighlightWindow.Around(1000, 1500, 10000);

        Assert.That(window.Covers(600, 1100, 10000), Is.True);
        Assert.That(window.Covers(1400, 1900, 10000), Is.True);
    }

    [Test]
    public void ScrollingPastTheMarginIsNotCovered()
    {
        TextHighlightWindow window = TextHighlightWindow.Around(1000, 1500, 10000);

        Assert.That(window.Covers(400, 900, 10000), Is.False);
        Assert.That(window.Covers(1600, 2100, 10000), Is.False);
    }

    [Test]
    public void ShowingMoreThanTheTextAtItsEdgesIsCovered()
    {
        TextHighlightWindow window = TextHighlightWindow.Around(0, 500, 800);

        Assert.That(window.Covers(-50, 450, 800), Is.True);
        Assert.That(window.Covers(400, 900, 800), Is.True);
    }

    [Test]
    public void NoWindowCoversNothing()
    {
        Assert.That(TextHighlightWindow.None.Covers(0, 0, 0), Is.False);
    }
}
