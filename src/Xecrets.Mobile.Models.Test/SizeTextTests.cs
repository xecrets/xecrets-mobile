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

using Xecrets.Mobile.Models.Utilities;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class SizeTextTests
{
    [Test]
    [SetCulture("en-US")]
    public void SizeIsInTheLargestUnitItReaches()
    {
        Assert.That(1023L.ToSizeText(), Is.EqualTo(string.Format(MobileTexts.FileSizeFormat, "1,023")));
        Assert.That(1536L.ToSizeText(), Is.EqualTo(string.Format(MobileTexts.FileSizeKilobytesFormat, "1.5")));
        Assert.That((5L << 20).ToSizeText(), Is.EqualTo(string.Format(MobileTexts.FileSizeMegabytesFormat, "5")));
        Assert.That((1500L << 30).ToSizeText(), Is.EqualTo(string.Format(MobileTexts.FileSizeGigabytesFormat, "1,500")));
    }

    /// <summary>
    /// The digit groups are separated by a non-breaking space in Swedish, keeping the number on one line.
    /// </summary>
    [Test]
    [SetCulture("sv-SE")]
    public void NumberIsFormattedAsTheCurrentCultureDoes()
    {
        Assert.That(
            (1500L << 30 | 1L << 29).ToSizeText(),
            Is.EqualTo(string.Format(MobileTexts.FileSizeGigabytesFormat, "1 500,5")));
    }
}
