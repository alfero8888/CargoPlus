using System.IO.Compression;
using System.Text;

namespace CargoPlus;

internal static class ExcelSheet
{
    public static void Save(string path, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        Entry(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        Entry(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        Entry(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Cargo Plus" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
        Entry(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);
        Entry(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows));
    }

    private static string Sheet(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        var line = 1;
        xml.Append(Row(line, headers));
        foreach (var row in rows)
        {
            line++;
            xml.Append(Row(line, row));
        }

        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    private static string Row(int line, IReadOnlyList<string> cells)
    {
        var xml = new StringBuilder();
        xml.Append("<row r=\"").Append(line).Append("\">");
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = Column(i) + line.ToString(System.Globalization.CultureInfo.InvariantCulture);
            xml.Append("<c r=\"").Append(cell).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
            xml.Append(System.Security.SecurityElement.Escape(cells[i] ?? ""));
            xml.Append("</t></is></c>");
        }

        xml.Append("</row>");
        return xml.ToString();
    }

    private static string Column(int index)
    {
        var name = "";
        var number = index + 1;
        while (number > 0)
        {
            number--;
            name = (char)('A' + number % 26) + name;
            number /= 26;
        }

        return name;
    }

    private static void Entry(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }
}
