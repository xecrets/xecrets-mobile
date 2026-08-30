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

using System.Text;

using NUnit.Framework;

using Xecrets.Mobile.Models.Models;
using Xecrets.Mobile.Models.Services;

namespace Xecrets.Mobile.Models.Test;

[TestFixture]
public sealed class ContentTypeDetectorTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"xecrets-mobile-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    [Test]
    public void RecognizedExtensionWithAllAsciiContentIsNotReclassifiedAsText()
    {
        // A recognized extension (.pdf) must keep its positively-identified content type and External kind even
        // when its bytes happen to pass the "looks like text" sniff - that sniff exists only for unrecognized
        // extensions. This pins the fix for the bug where such a file was silently reclassified as text/plain.
        string filePath = Path.Combine(_directory, "document.pdf");
        File.WriteAllText(filePath, "%PDF-1.4\n1 0 obj << /Type /Catalog >>\nendobj\n", Encoding.ASCII);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(filePath, "document.pdf");

        Assert.That(file.ContentType, Is.EqualTo("application/pdf"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.External));
    }

    [Test]
    public void UnrecognizedExtensionWithTextContentIsClassifiedAsText()
    {
        // Files with an extension the detector does not know at all still fall back to sniffing their content -
        // this is the case the heuristic is meant for.
        string filePath = Path.Combine(_directory, "notes.xyz");
        File.WriteAllText(filePath, "Just some plain text notes.", Encoding.ASCII);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(filePath, "notes.xyz");

        Assert.That(file.ContentType, Is.EqualTo("text/plain"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.Text));
    }

    [Test]
    public void UnrecognizedExtensionWithBinaryContentStaysExternal()
    {
        string filePath = Path.Combine(_directory, "blob.xyz");
        File.WriteAllBytes(filePath, [0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD]);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(filePath, "blob.xyz");

        Assert.That(file.ContentType, Is.EqualTo("application/octet-stream"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.External));
    }

    [Test]
    public void TextFileOverTheSizeCapIsClassifiedAsExternal()
    {
        // A text file past MaxInlineTextSizeBytes is still exactly what it is - ContentType stays text/plain - it
        // is just reported as External instead of Text, so the app's internal viewer/editor (which key off Kind,
        // not size) leave it alone. Nothing is truncated: the whole file is decrypted to disk as usual, Open In and
        // Save As are unaffected.
        string filePath = Path.Combine(_directory, "large.txt");
        File.WriteAllText(filePath, new string('a', (int)ContentTypeDetector.MaxInlineTextSizeBytes + 1), Encoding.ASCII);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(filePath, "large.txt");

        Assert.That(file.ContentType, Is.EqualTo("text/plain"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.External));
    }

    [Test]
    public void TextFileAtOrUnderTheSizeCapStaysText()
    {
        string filePath = Path.Combine(_directory, "small.txt");
        File.WriteAllText(filePath, new string('a', (int)ContentTypeDetector.MaxInlineTextSizeBytes), Encoding.ASCII);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(filePath, "small.txt");

        Assert.That(file.ContentType, Is.EqualTo("text/plain"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.Text));
    }

    [TestCase("report.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [TestCase("report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [TestCase("report.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [TestCase("report.doc", "application/msword")]
    [TestCase("report.xls", "application/vnd.ms-excel")]
    [TestCase("report.ppt", "application/vnd.ms-powerpoint")]
    [TestCase("report.odt", "application/vnd.oasis.opendocument.text")]
    public void OfficeAndOpenDocumentExtensionsMapToTheirOwnContentType(string fileName, string expectedContentType)
    {
        Assert.That(ContentTypeDetector.DetectContentType(fileName), Is.EqualTo(expectedContentType));
    }

    [Test]
    public void RecognizedBinaryOfficeExtensionIsNotReclassifiedAsText()
    {
        // A .docx is a zip container, not text, but its bytes could in principle happen to pass the "looks like
        // text" sniff for a short enough sample. As with .pdf above, a recognized extension must keep its
        // positively-identified content type and External kind rather than being routed to the inline text viewer.
        string filePath = Path.Combine(_directory, "report.docx");
        File.WriteAllBytes(filePath, [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00]);

        DecryptedFileInfo file = ContentTypeDetector.CreateInfo(filePath, "report.docx");

        Assert.That(file.ContentType, Is.EqualTo("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        Assert.That(file.Kind, Is.EqualTo(PreviewKind.External));
    }
}
