using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Dataset : Component
    {
        private sealed class DatasetSettings
        {
            public string Format { get; set; } = "Auto";
            public string SourcePath { get; set; } = string.Empty;
            public string SheetName { get; set; } = string.Empty;
            public string CsvDelimiter { get; set; } = ",";
            public bool CsvHasHeader { get; set; } = true;
            public string JsonArrayPath { get; set; } = string.Empty;
            public string XmlRowPath { get; set; } = string.Empty;
            public int MaxRows { get; set; }
        }

        public Dataset()
        {
            Name = "Dataset";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var rows = LoadRows(Settings);
            var normalized = NormalizeSettings(Settings);

            var data = new DatasetData
            {
                Id = this.Id,
                ComponentName = this.Name,
                DataSource = normalized.SourcePath,
                Rows = rows,
                CurrentRow = rows.Count > 0 ? 1 : 0
            };

            data.Properties["format"] = normalized.Format;
            data.Properties["rowCount"] = rows.Count;
            return Task.FromResult<ComponentData>(data);
        }

        public static List<Dictionary<string, object>> LoadRows(Dictionary<string, string> settings)
        {
            var normalized = NormalizeSettings(settings);
            if (string.IsNullOrWhiteSpace(normalized.SourcePath))
            {
                return new List<Dictionary<string, object>>();
            }

            if (!File.Exists(normalized.SourcePath))
            {
                throw new FileNotFoundException($"Dataset file not found: {normalized.SourcePath}", normalized.SourcePath);
            }

            var format = NormalizeFormat(normalized.Format, normalized.SourcePath);
            var rows = format switch
            {
                "Excel" => LoadExcelRows(normalized),
                "Csv" => LoadCsvRows(normalized),
                "Json" => LoadJsonRows(normalized),
                "Xml" => LoadXmlRows(normalized),
                _ => throw new InvalidOperationException($"Unsupported dataset format: {normalized.Format}")
            };

            if (normalized.MaxRows > 0)
            {
                rows = rows.Take(normalized.MaxRows).ToList();
            }

            return rows;
        }

        private static DatasetSettings NormalizeSettings(Dictionary<string, string> settings)
        {
            var normalized = new DatasetSettings
            {
                Format = TryGet(settings, "Format", "Auto"),
                SourcePath = TryGet(settings, "SourcePath", TryGet(settings, "DataSource", string.Empty)),
                SheetName = TryGet(settings, "SheetName", string.Empty),
                CsvDelimiter = TryGet(settings, "CsvDelimiter", ","),
                CsvHasHeader = bool.TryParse(TryGet(settings, "CsvHasHeader", "true"), out var hasHeader) ? hasHeader : true,
                JsonArrayPath = TryGet(settings, "JsonArrayPath", string.Empty),
                XmlRowPath = TryGet(settings, "XmlRowPath", string.Empty),
                MaxRows = int.TryParse(TryGet(settings, "MaxRows", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxRows)
                    ? Math.Max(0, maxRows)
                    : 0
            };

            if (string.IsNullOrWhiteSpace(normalized.CsvDelimiter))
            {
                normalized.CsvDelimiter = ",";
            }

            return normalized;
        }

        private static string TryGet(Dictionary<string, string> settings, string key, string fallback)
        {
            return settings.TryGetValue(key, out var value) ? value : fallback;
        }

        private static string NormalizeFormat(string raw, string sourcePath)
        {
            if (!string.IsNullOrWhiteSpace(raw)
                && !string.Equals(raw, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(raw, "Excel", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "Csv", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "Json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "Xml", StringComparison.OrdinalIgnoreCase))
                {
                    return char.ToUpperInvariant(raw[0]) + raw[1..].ToLowerInvariant();
                }
            }

            var extension = Path.GetExtension(sourcePath)?.Trim().ToLowerInvariant() ?? string.Empty;
            return extension switch
            {
                ".xlsx" or ".xlsm" or ".xls" => "Excel",
                ".csv" => "Csv",
                ".json" => "Json",
                ".xml" => "Xml",
                _ => "Csv"
            };
        }

        private static List<Dictionary<string, object>> LoadExcelRows(DatasetSettings settings)
        {
            using var workbook = new XLWorkbook(settings.SourcePath);
            var worksheet = string.IsNullOrWhiteSpace(settings.SheetName)
                ? workbook.Worksheets.FirstOrDefault()
                : workbook.Worksheets.FirstOrDefault(sheet => string.Equals(sheet.Name, settings.SheetName, StringComparison.OrdinalIgnoreCase));

            // If the configured sheet does not exist, fall back to the first sheet instead of returning empty.
            worksheet ??= workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                return new List<Dictionary<string, object>>();
            }

            var range = worksheet.RangeUsed();
            if (range == null)
            {
                return new List<Dictionary<string, object>>();
            }

            var firstRow = range.RangeAddress.FirstAddress.RowNumber;
            var lastRow = range.RangeAddress.LastAddress.RowNumber;
            var firstColumn = range.RangeAddress.FirstAddress.ColumnNumber;
            var lastColumn = range.RangeAddress.LastAddress.ColumnNumber;

            var headers = new List<string>();
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var headerValue = worksheet.Cell(firstRow, column).GetValue<string>()?.Trim();
                headers.Add(string.IsNullOrWhiteSpace(headerValue) ? $"Column{column - firstColumn + 1}" : headerValue);
            }

            var rows = new List<Dictionary<string, object>>();
            for (var row = firstRow + 1; row <= lastRow; row++)
            {
                var dataRow = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var hasAny = false;

                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    var value = worksheet.Cell(row, column).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        hasAny = true;
                    }

                    dataRow[headers[column - firstColumn]] = value ?? string.Empty;
                }

                if (hasAny)
                {
                    rows.Add(dataRow);
                }
            }

            // If no rows were produced after header interpretation, treat the first row as data.
            if (rows.Count == 0)
            {
                var singleRow = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var hasAnyValue = false;

                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    var value = worksheet.Cell(firstRow, column).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        hasAnyValue = true;
                    }

                    singleRow[$"Column{column - firstColumn + 1}"] = value ?? string.Empty;
                }

                if (hasAnyValue)
                {
                    rows.Add(singleRow);
                }
            }

            return rows;
        }

        private static List<Dictionary<string, object>> LoadCsvRows(DatasetSettings settings)
        {
            var rows = new List<Dictionary<string, object>>();

            using var parser = new TextFieldParser(settings.SourcePath)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };

            parser.SetDelimiters(settings.CsvDelimiter);

            string[]? headers = null;
            if (settings.CsvHasHeader && !parser.EndOfData)
            {
                headers = parser.ReadFields();
            }

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null)
                {
                    continue;
                }

                if (headers == null)
                {
                    headers = Enumerable.Range(1, fields.Length)
                        .Select(index => $"Column{index}")
                        .ToArray();
                }

                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Length; i++)
                {
                    var key = string.IsNullOrWhiteSpace(headers[i]) ? $"Column{i + 1}" : headers[i];
                    var value = i < fields.Length ? fields[i] : string.Empty;
                    row[key] = value ?? string.Empty;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static List<Dictionary<string, object>> LoadJsonRows(DatasetSettings settings)
        {
            using var stream = File.OpenRead(settings.SourcePath);
            using var document = JsonDocument.Parse(stream);
            var root = ResolveJsonPath(document.RootElement, settings.JsonArrayPath);

            var rows = new List<Dictionary<string, object>>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    rows.Add(JsonElementToRow(element));
                }

                return rows;
            }

            rows.Add(JsonElementToRow(root));
            return rows;
        }

        private static JsonElement ResolveJsonPath(JsonElement root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || string.Equals(path.Trim(), "$", StringComparison.Ordinal))
            {
                return root;
            }

            var normalized = path.Trim();
            if (normalized.StartsWith("$", StringComparison.Ordinal))
            {
                normalized = normalized.TrimStart('$');
                if (normalized.StartsWith(".", StringComparison.Ordinal))
                {
                    normalized = normalized[1..];
                }
            }

            var current = root;
            foreach (var segment in normalized.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
                {
                    current = next;
                    continue;
                }

                return root;
            }

            return current;
        }

        private static Dictionary<string, object> JsonElementToRow(JsonElement element)
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    FlattenJsonValue(property.Value, property.Name, row);
                }

                return row;
            }

            row["Value"] = JsonElementToObject(element);
            return row;
        }

        private static void FlattenJsonValue(JsonElement element, string key, Dictionary<string, object> row)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var hadProperty = false;
                foreach (var property in element.EnumerateObject())
                {
                    hadProperty = true;
                    var childKey = string.IsNullOrWhiteSpace(key)
                        ? property.Name
                        : $"{key}.{property.Name}";
                    FlattenJsonValue(property.Value, childKey, row);
                }

                if (!hadProperty && !string.IsNullOrWhiteSpace(key))
                {
                    row[key] = "{}";
                }

                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                var items = element.EnumerateArray().ToList();
                if (items.Count == 0)
                {
                    row[key] = "[]";
                    return;
                }

                if (items.All(item => item.ValueKind != JsonValueKind.Object && item.ValueKind != JsonValueKind.Array))
                {
                    row[key] = string.Join(", ", items.Select(item => JsonElementToObject(item)?.ToString() ?? string.Empty));
                    return;
                }

                for (var i = 0; i < items.Count; i++)
                {
                    FlattenJsonValue(items[i], $"{key}[{i}]", row);
                }

                return;
            }

            row[key] = JsonElementToObject(element);
        }

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt64(out var number) ? number : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
                _ => element.ToString()
            };
        }

        private static List<Dictionary<string, object>> LoadXmlRows(DatasetSettings settings)
        {
            var document = XDocument.Load(settings.SourcePath);
            var rows = ResolveXmlRows(document, settings.XmlRowPath);

            return rows
                .Select(XmlElementToRow)
                .Where(row => row.Count > 0)
                .ToList();
        }

        private static IEnumerable<XElement> ResolveXmlRows(XDocument document, string rowPath)
        {
            if (document.Root == null)
            {
                return Enumerable.Empty<XElement>();
            }

            if (string.IsNullOrWhiteSpace(rowPath))
            {
                return document.Root.Elements();
            }

            var segments = rowPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<XElement> current = new[] { document.Root };

            if (segments.Length > 0 && string.Equals(segments[0], document.Root.Name.LocalName, StringComparison.OrdinalIgnoreCase))
            {
                segments = segments.Skip(1).ToArray();
            }

            foreach (var segment in segments)
            {
                current = current.SelectMany(element => element.Elements()
                    .Where(child => string.Equals(child.Name.LocalName, segment, StringComparison.OrdinalIgnoreCase)));
            }

            return current;
        }

        private static Dictionary<string, object> XmlElementToRow(XElement element)
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            FlattenXmlElement(element, element.Name.LocalName, row);

            return row;
        }

        private static void FlattenXmlElement(XElement element, string keyPrefix, Dictionary<string, object> row)
        {
            foreach (var attribute in element.Attributes())
            {
                var attributeKey = string.IsNullOrWhiteSpace(keyPrefix)
                    ? $"@{attribute.Name.LocalName}"
                    : $"{keyPrefix}.@{attribute.Name.LocalName}";
                row[attributeKey] = attribute.Value;
            }

            var children = element.Elements().ToList();
            if (children.Count == 0)
            {
                var valueKey = string.IsNullOrWhiteSpace(keyPrefix) ? "Value" : keyPrefix;
                row[valueKey] = element.Value;
                return;
            }

            foreach (var group in children.GroupBy(child => child.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            {
                var grouped = group.ToList();
                for (var index = 0; index < grouped.Count; index++)
                {
                    var child = grouped[index];
                    var baseKey = string.IsNullOrWhiteSpace(keyPrefix)
                        ? child.Name.LocalName
                        : $"{keyPrefix}.{child.Name.LocalName}";
                    var childKey = grouped.Count == 1 ? baseKey : $"{baseKey}[{index}]";
                    FlattenXmlElement(child, childKey, row);
                }
            }
        }
    }
}
