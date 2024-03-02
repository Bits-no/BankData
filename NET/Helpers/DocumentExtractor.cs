using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace BitsNo.Helpers;

public static class DocumentExtractor
{
    private static Dictionary<int, string> GetSharedStringDictionary(SharedStringTablePart sharedStringTablePart)
    {
        var dictionary = new Dictionary<int, string>();
        using var reader = OpenXmlReader.Create(sharedStringTablePart);
        int i = 0;
        while (reader.Read())
        {
            if (reader.ElementType == typeof(SharedStringItem))
            {
                var ssi = (SharedStringItem?)reader.LoadCurrentElement();
                dictionary.Add(i++, ssi?.Text?.Text?.Trim() ?? string.Empty);
            }
        }
        return dictionary;
    }

    public class ParsedXls
    {
        public List<string[]> Rows { get; internal set; } = [];

        public DateTime? Modified { get; internal set; }

        public IEnumerable<string> GeneratePsv() => GeneratePsvFromRows(Rows);
    }

    public static ParsedXls ParseXlsx(Stream data)
    {
        using var spd = SpreadsheetDocument.Open(data, false);
        var wbp = spd?.WorkbookPart;
        var parsed = new ParsedXls();
        if (wbp is null)
            return parsed;

        parsed.Modified = spd!.PackageProperties.Modified;
        var sharedStringdict = GetSharedStringDictionary(wbp.GetPartsOfType<SharedStringTablePart>().First());
        var worksheetPart = wbp.WorksheetParts.First();
        var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
        string text;
        foreach (var r in sheetData.Elements<Row>())
        {
            var row = new List<string>();
            foreach (var c in r.Elements<Cell>())
            {
                text = c.InnerText;
                if (c.DataType?.Value == CellValues.SharedString)
                    text = sharedStringdict[int.Parse(text)];
                row.Add(text);
            }
            parsed.Rows.Add([.. row]);
        }
        return parsed;
    }

    public static ParsedXls ParseXls(Stream data)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(data, new() {
            LeaveOpen = true,
        });
        var parsed = new ParsedXls();
        // parsed.Modified = https://github.com/ExcelDataReader/ExcelDataReader/issues/308
        do
        {
            while (reader.Read())
            {
                var row = new List<string>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(Convert.ToString(reader.GetValue(i) ?? "")!.Trim());
                }
                parsed.Rows.Add([.. row]);
            }
        } while (reader.NextResult());
        return parsed;
    }

    private class BankDetailRaw
    {
        // Bank identifier|BIC|Bank
        public readonly string Identifier;
        public readonly string BIC;
        public readonly string Bank;

        public int Id => int.TryParse(Identifier, out int id) ? id : -1;

        protected BankDetailRaw(string identifier, string bic, string bank)
        {
            Identifier = identifier;
            BIC = bic;
            Bank = bank;
        }

        private static string ValidateRowAndGetIdentifier(string[] row)
        {
            if (row.Length != 3)
                throw new ArgumentOutOfRangeException(nameof(row), row, $"param count invalid {row.Length} {string.Join("|", row)}");
            return row[0];
        }

        public BankDetailRaw(string[] row)
            : this(ValidateRowAndGetIdentifier(row), row[1], row[2])
        {
        }

        public override string ToString() => string.Join("|", Identifier, BIC, Bank);

        public bool IsSame(BankDetailRaw other) => 
            BIC == other.BIC && 
            Bank == other.Bank;
    }

    private class BankDetailSet : BankDetailRaw
    {
        public readonly int Start;
        public int End;

        public BankDetailSet(BankDetailRaw raw)
            : base(raw.Identifier, raw.BIC, raw.Bank)
        {
            Start = raw.Id;
            End = Start;
        }

        public BankDetailSet Add(BankDetailRaw raw)
        {
            if (!IsSame(raw))
                throw new ArgumentOutOfRangeException(nameof(raw), raw, $"Not Same BIC/Bank {this}");
            if (raw.Id != End + 1 && raw.Id != End)
                throw new ArgumentOutOfRangeException(nameof(raw), raw, $"Id must be sequential next to End or same {this}");
            End = raw.Id;
            return this;
        }

        public string Interval => $"{Start:0000}" + (Start == End ? "" : $"-{End:0000}");

        public override string ToString() => string.Join("|", Interval, BIC, Bank);
    }

    private static IEnumerable<string> GeneratePsvFromRows(IEnumerable<string[]> rows)
    {
        var list = new List<BankDetailRaw>();
        foreach (var row in rows)
        {
            var rawRow = new BankDetailRaw(row);
            if (list.Count != 0 && !int.TryParse(rawRow.Identifier, out int id))
                throw new ArgumentOutOfRangeException(nameof(row), row, "Only first line is expected header");
            list.Add(rawRow);
        }
        BankDetailSet? prev = null;
        foreach (var row in list.OrderBy(x => x.Id))
        {
            if (row.Id == -1)
            {
                // Comment/header
                yield return $"#{row}";
                prev = null;
                continue;
            }

            if (prev is not null &&
                prev.IsSame(row) &&
                row.Id == prev.End + 1)
            {
                prev.Add(row);
            }
            else
            {
                if (prev is not null)
                    yield return prev.ToString();
                prev = new BankDetailSet(row);
            }
        }
        if (prev is not null)
            yield return prev.ToString();
        yield break;
    }
}
