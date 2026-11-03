using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace RPGAuto.EditorTools.Data
{
    /// <summary>
    /// 외부 라이브러리 없이 .xlsx(Open XML)를 읽는 최소 리더.
    /// 시트 이름 → 행 목록(각 행은 열 순서대로 문자열 배열)을 반환한다.
    /// Excel에서 파일을 열어 둔 상태에서도 읽을 수 있도록 공유 모드로 연다.
    /// </summary>
    public static class XlsxReader
    {
        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static Dictionary<string, List<string[]>> ReadAllSheets(string path)
        {
            var result = new Dictionary<string, List<string[]>>();

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

            var sharedStrings = ReadSharedStrings(zip);

            // 시트 이름 → 파일 경로
            var rels = new Dictionary<string, string>();
            var relDoc = LoadXml(zip, "xl/_rels/workbook.xml.rels");
            var relNs = new XmlNamespaceManager(relDoc.NameTable);
            relNs.AddNamespace("r", PkgRelNs);
            foreach (XmlElement rel in relDoc.SelectNodes("//r:Relationship", relNs))
            {
                string target = rel.GetAttribute("Target").TrimStart('/');
                if (!target.StartsWith("xl/")) target = "xl/" + target;
                rels[rel.GetAttribute("Id")] = target;
            }

            var wbDoc = LoadXml(zip, "xl/workbook.xml");
            var ns = new XmlNamespaceManager(wbDoc.NameTable);
            ns.AddNamespace("m", MainNs);
            ns.AddNamespace("r", RelNs);
            foreach (XmlElement sheet in wbDoc.SelectNodes("//m:sheets/m:sheet", ns))
            {
                string name = sheet.GetAttribute("name");
                string rid = sheet.GetAttribute("id", RelNs);
                if (!rels.TryGetValue(rid, out var sheetPath)) continue;
                result[name] = ReadSheet(zip, sheetPath, sharedStrings);
            }
            return result;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            if (zip.GetEntry("xl/sharedStrings.xml") == null) return list;

            var doc = LoadXml(zip, "xl/sharedStrings.xml");
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", MainNs);
            foreach (XmlElement si in doc.SelectNodes("//m:si", ns))
            {
                var sb = new StringBuilder();
                foreach (XmlNode t in si.SelectNodes(".//m:t", ns)) sb.Append(t.InnerText);
                list.Add(sb.ToString());
            }
            return list;
        }

        private static List<string[]> ReadSheet(ZipArchive zip, string entryPath, List<string> shared)
        {
            var rows = new List<string[]>();
            var doc = LoadXml(zip, entryPath);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", MainNs);

            foreach (XmlElement row in doc.SelectNodes("//m:sheetData/m:row", ns))
            {
                int rowIndex = int.Parse(row.GetAttribute("r")) - 1;
                while (rows.Count < rowIndex) rows.Add(new string[0]);

                var cells = new List<string>();
                foreach (XmlElement c in row.SelectNodes("m:c", ns))
                {
                    int col = ColumnIndex(c.GetAttribute("r"));
                    while (cells.Count < col) cells.Add(string.Empty);

                    string type = c.GetAttribute("t");
                    string value;
                    if (type == "inlineStr")
                    {
                        value = c.SelectSingleNode("m:is", ns)?.InnerText ?? string.Empty;
                    }
                    else
                    {
                        string v = c.SelectSingleNode("m:v", ns)?.InnerText ?? string.Empty;
                        value = type == "s" && int.TryParse(v, out int idx) && idx < shared.Count ? shared[idx] : v;
                    }
                    cells.Add(value);
                }
                rows.Add(cells.ToArray());
            }
            return rows;
        }

        /// <summary>"AB12" → 27 (0-based 열 번호)</summary>
        private static int ColumnIndex(string cellRef)
        {
            int col = 0;
            foreach (char ch in cellRef)
            {
                if (ch < 'A' || ch > 'Z') break;
                col = col * 26 + (ch - 'A' + 1);
            }
            return col - 1;
        }

        private static XmlDocument LoadXml(ZipArchive zip, string entryPath)
        {
            var entry = zip.GetEntry(entryPath) ?? throw new FileNotFoundException("xlsx 내부 파일 없음: " + entryPath);
            var doc = new XmlDocument();
            using var s = entry.Open();
            doc.Load(s);
            return doc;
        }
    }
}
