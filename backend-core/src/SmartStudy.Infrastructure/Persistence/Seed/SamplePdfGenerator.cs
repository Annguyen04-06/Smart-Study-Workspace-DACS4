using System.Globalization;
using System.Text;

namespace SmartStudy.Infrastructure.Persistence.Seed;

/// <summary>
/// Tiện ích sinh tự động các tệp PDF đa trang chuẩn ISO 1.4 vật lý vào wwwroot/uploads/materials/.
/// Đảm bảo 100% tài liệu mẫu khi nhấn "Đọc trực tiếp PDF" trên giao diện đều mở ra tài liệu học thuật nhiều trang sắc nét, không bao giờ lỗi 404.
/// </summary>
public static class SamplePdfGenerator
{
    public sealed record PdfSeedSpec(
        string FileName,
        string CourseCode,
        string DocumentTitle,
        string[] Sections);

    public static long EnsureMultiPagePdfExists(string materialsDirectory, PdfSeedSpec spec)
    {
        Directory.CreateDirectory(materialsDirectory);
        var fullPath = Path.Combine(materialsDirectory, spec.FileName);

        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 1200)
        {
            return new FileInfo(fullPath).Length;
        }

        // Chia nội dung học thuật thành các trang PDF chuẩn A4 (595 x 842 pt)
        var asciiTitle = ToSafePdfAscii(spec.DocumentTitle);
        var asciiCourse = ToSafePdfAscii($"VKU Smart Study Workspace - Module {spec.CourseCode}");
        var wrappedLines = new List<string>();

        foreach (var section in spec.Sections)
        {
            var cleanSection = ToSafePdfAscii(section);
            foreach (var paragraph in cleanSection.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                wrappedLines.AddRange(WrapLine(paragraph.Trim(), maxChars: 82));
                wrappedLines.Add(string.Empty);
            }
        }

        const int linesPerPage = 34;
        var pageChunks = new List<List<string>>();
        for (var i = 0; i < wrappedLines.Count; i += linesPerPage)
        {
            pageChunks.Add(wrappedLines.Skip(i).Take(linesPerPage).ToList());
        }

        if (pageChunks.Count == 0)
        {
            pageChunks.Add(new List<string> { asciiTitle });
        }

        var bytes = BuildMultiPagePdfBytes(asciiCourse, asciiTitle, pageChunks);
        File.WriteAllBytes(fullPath, bytes);
        return bytes.Length;
    }

    private static byte[] BuildMultiPagePdfBytes(string headerLabel, string docTitle, List<List<string>> pages)
    {
        // Object 1: Catalog, Object 2: Pages, Object 3: Font Helvetica, Object 4: Font Helvetica-Bold
        // Với mỗi trang i (0..N-1): PageObj = 5 + i*2, ContentObj = 6 + i*2
        var pageCount = pages.Count;
        var totalObjects = 4 + pageCount * 2;
        var objects = new List<string>(totalObjects);

        var kidsRefs = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{5 + i * 2} 0 R"));
        objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        objects.Add($"2 0 obj\n<< /Type /Pages /Kids [{kidsRefs}] /Count {pageCount} >>\nendobj\n");
        objects.Add("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        objects.Add("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");

        for (var p = 0; p < pageCount; p++)
        {
            var pageObjNum = 5 + p * 2;
            var contentObjNum = 6 + p * 2;

            var sb = new StringBuilder();
            // Thanh màu tiêu đề trên đầu trang
            sb.AppendLine("0.31 0.27 0.90 rg");
            sb.AppendLine("42 786 511 28 re f");
            sb.AppendLine("1 1 1 rg");
            sb.AppendLine("BT /F2 10 Tf 52 796 Td");
            sb.AppendLine($"({EscapePdfLiteral($"{headerLabel}   |   Page {p + 1} of {pageCount}")}) Tj ET");

            sb.AppendLine("0.08 0.11 0.18 rg");
            sb.AppendLine("BT");
            if (p == 0)
            {
                sb.AppendLine("/F2 14 Tf 46 756 Td");
                sb.AppendLine($"({EscapePdfLiteral(docTitle)}) Tj");
                sb.AppendLine("0 -24 Td /F1 10 Tf");
            }
            else
            {
                sb.AppendLine("/F1 10 Tf 46 758 Td");
            }

            foreach (var line in pages[p])
            {
                if (string.IsNullOrEmpty(line))
                {
                    sb.AppendLine("0 -10 Td");
                    continue;
                }

                var isHeading = line.StartsWith("## ") || line.StartsWith("SECTION ");
                if (isHeading)
                {
                    sb.AppendLine("/F2 11 Tf");
                    sb.AppendLine($"({EscapePdfLiteral(line.TrimStart('#', ' '))}) Tj");
                    sb.AppendLine("/F1 10 Tf");
                    sb.AppendLine("0 -16 Td");
                }
                else
                {
                    sb.AppendLine($"({EscapePdfLiteral(line)}) Tj");
                    sb.AppendLine("0 -15 Td");
                }
            }

            sb.AppendLine("ET");

            var streamContent = sb.ToString();
            var byteLen = Encoding.ASCII.GetByteCount(streamContent);

            objects.Add(
                $"{pageObjNum} 0 obj\n" +
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> " +
                $"/Contents {contentObjNum} 0 R >>\nendobj\n");

            objects.Add(
                $"{contentObjNum} 0 obj\n<< /Length {byteLen} >>\nstream\n{streamContent}endstream\nendobj\n");
        }

        using var ms = new MemoryStream();
        void WriteAscii(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        WriteAscii("%PDF-1.4\n");
        var offsets = new List<long>(objects.Count);
        foreach (var obj in objects)
        {
            offsets.Add(ms.Position);
            WriteAscii(obj);
        }

        var xrefOffset = ms.Position;
        WriteAscii($"xref\n0 {objects.Count + 1}\n");
        WriteAscii("0000000000 65535 f \n");
        foreach (var off in offsets)
        {
            WriteAscii($"{off:D10} 00000 n \n");
        }

        WriteAscii($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return ms.ToArray();
    }

    private static IEnumerable<string> WrapLine(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            yield return text;
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxChars && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static string ToSafePdfAscii(string vietnameseText)
    {
        if (string.IsNullOrEmpty(vietnameseText)) return string.Empty;

        var decomposed = vietnameseText
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Replace("—", "-")
            .Replace("–", "-")
            .Replace("•", "*")
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            sb.Append(ch <= 126 && (ch >= 32 || ch == '\n') ? ch : ' ');
        }
        return sb.ToString();
    }

    private static string EscapePdfLiteral(string input)
    {
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
