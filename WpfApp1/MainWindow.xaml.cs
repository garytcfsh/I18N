using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const string OUTPUT_PREFIX = "I18N";
        private const int DEFAULT_KEY_COLUMN_INDEX = 0;

        private sealed class OutputTarget
        {
            public int ColumnIndex { get; set; }
            public string FilePath { get; set; } = "";
        }

        private sealed class CellPosition
        {
            public int RowIndex { get; set; }
            public int ColumnIndex { get; set; }
        }

        private sealed class ConversionResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public string OutputPath { get; set; } = "";
        }

        private string _sourceFilePath = "";
        public string SourceFilePath
        {
            get => _sourceFilePath;
            set
            {
                if (_sourceFilePath == value) return;
                _sourceFilePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceFilePath)));
            }
        }

        private string _errMsg = "";
        public string ErrMsg
        {
            get => _errMsg;
            set
            {
                if (_errMsg == value) return;
                _errMsg = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrMsg)));
            }
        }

        private string _preferredKeyHeader = "KEY iOS GTB";
        public string PreferredKeyHeader
        {
            get => _preferredKeyHeader;
            set
            {
                if (_preferredKeyHeader == value) return;
                _preferredKeyHeader = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreferredKeyHeader)));
            }
        }

        private string _outputPath = "";
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (_outputPath == value) return;
                _outputPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputPath)));
            }
        }

        private bool _skipEmptyValues = true;
        public bool SkipEmptyValues
        {
            get => _skipEmptyValues;
            set
            {
                if (_skipEmptyValues == value) return;
                _skipEmptyValues = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SkipEmptyValues)));
            }
        }

        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set
            {
                if (_isConverting == value) return;
                _isConverting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConverting)));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ErrMsg = "Step 1: Open an xlsx file. Step 2: Confirm key header. Step 3: Convert.";
        }

        private bool ValidateSourceFile()
        {
            if (string.IsNullOrWhiteSpace(SourceFilePath))
            {
                ErrMsg = "Please open a source file first.";
                return false;
            }

            if (!File.Exists(SourceFilePath))
            {
                ErrMsg = "Source file not found.";
                return false;
            }

            return true;
        }

        private ConversionResult Convert2Txts()
        {
            bool skipEmpty = SkipEmptyValues;
            string folderName = $"{OUTPUT_PREFIX}-txts";
            System.IO.Directory.CreateDirectory(folderName);
            string outputPath = Path.GetFullPath(folderName);

            var outputTargets = new List<OutputTarget>();

            List<string[]> rows = LoadRowsFromSource();
            if (rows.Count == 0)
            {
                return new ConversionResult { Success = false, Message = "No data rows found" };
            }

            int keyRowIndex = FindKeyRowIndex(rows);
            if (keyRowIndex < 0)
            {
                return new ConversionResult { Success = false, Message = "Cannot find KEY header row" };
            }

            CellPosition abbrCell = FindCellPosition(rows, "abbr");
            int abbrRowIndex = abbrCell?.RowIndex ?? -1;
            int keyColumnIndex = ResolveKeyColumnIndex(rows, keyRowIndex, abbrCell, PreferredKeyHeader);
            int bodyStartIndex = (abbrRowIndex > keyRowIndex) ? abbrRowIndex + 1 : keyRowIndex + 1;

            string[] keyRow = rows[keyRowIndex];
            string[] abbrRow = abbrRowIndex >= 0 ? rows[abbrRowIndex] : null;

            for (int c = 0; c < keyRow.Length; c++)
            {
                if (c == keyColumnIndex)
                {
                    continue;
                }

                if (abbrRow != null)
                {
                    string code = c < abbrRow.Length ? abbrRow[c]?.Trim() : "";
                    if (!LooksLikeLanguageCode(code))
                    {
                        continue;
                    }
                }

                string columnTitle = keyRow[c]?.Trim();
                if (string.IsNullOrWhiteSpace(columnTitle))
                {
                    continue;
                }

                string filePath = $@"{folderName}/{columnTitle}.txt";
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                FileStream fs = File.Create(filePath);
                fs.Close();

                outputTargets.Add(new OutputTarget
                {
                    ColumnIndex = c,
                    FilePath = filePath,
                });
            }

            if (outputTargets.Count == 0)
            {
                return new ConversionResult { Success = false, Message = "No language columns found" };
            }

            for (int r = bodyStartIndex; r < rows.Count; r++)
            {
                string[] columns = rows[r];
                if (columns.Length <= keyColumnIndex)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(columns[keyColumnIndex]))
                {
                    continue;
                }

                WriteBody(columns, keyColumnIndex, outputTargets, skipEmpty);
            }

            return new ConversionResult
            {
                Success = true,
                Message = $"Completed\nOutput: {outputPath}",
                OutputPath = outputPath,
            };
        }

        private ConversionResult Convert2XCode()
        {
            bool skipEmpty = SkipEmptyValues;
            string folderName = $"{OUTPUT_PREFIX}-XCode";
            System.IO.Directory.CreateDirectory(folderName);
            string outputPath = Path.GetFullPath(folderName);

            var outputTargets = new List<OutputTarget>();

            List<string[]> rows = LoadRowsFromSource();
            if (rows.Count == 0)
            {
                return new ConversionResult { Success = false, Message = "No data rows found" };
            }

            int keyRowIndex = FindKeyRowIndex(rows);
            if (keyRowIndex < 0)
            {
                return new ConversionResult { Success = false, Message = "Cannot find KEY header row" };
            }

            CellPosition abbrCell = FindCellPosition(rows, "abbr");
            int abbrRowIndex = abbrCell?.RowIndex ?? -1;
            int keyColumnIndex = ResolveKeyColumnIndex(rows, keyRowIndex, abbrCell, PreferredKeyHeader);
            int languageCodeRowIndex = abbrRowIndex >= 0 ? abbrRowIndex : keyRowIndex;
            int bodyStartIndex = (abbrRowIndex > keyRowIndex) ? abbrRowIndex + 1 : keyRowIndex + 1;

            string[] languageCodeRow = rows[languageCodeRowIndex];
            for (int c = 0; c < languageCodeRow.Length; c++)
            {
                if (c == keyColumnIndex)
                {
                    continue;
                }

                string languageCode = languageCodeRow[c]?.Trim();
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    continue;
                }

                if (abbrRowIndex < 0 && !LooksLikeLanguageCode(languageCode))
                {
                    continue;
                }

                string dirPath = $@"{folderName}/{languageCode}.lproj";
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, true);
                }
                Directory.CreateDirectory(dirPath);

                string filePath = $@"{dirPath}/Localizable.strings";
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                FileStream fs = File.Create(filePath);
                fs.Close();

                outputTargets.Add(new OutputTarget
                {
                    ColumnIndex = c,
                    FilePath = filePath,
                });
            }

            if (outputTargets.Count == 0)
            {
                return new ConversionResult { Success = false, Message = "No valid language-code columns found for Xcode output" };
            }

            for (int r = bodyStartIndex; r < rows.Count; r++)
            {
                string[] columns = rows[r];
                if (columns.Length <= keyColumnIndex)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(columns[keyColumnIndex]))
                {
                    continue;
                }

                WriteBody(columns, keyColumnIndex, outputTargets, skipEmpty);
            }

            return new ConversionResult
            {
                Success = true,
                Message = $"Completed\nOutput: {outputPath}",
                OutputPath = outputPath,
            };
        }

        private void WriteBody(string[] columns, int keyColumnIndex, IReadOnlyList<OutputTarget> outputTargets, bool skipEmpty)
        {
            foreach (OutputTarget target in outputTargets)
            {
                string value = target.ColumnIndex < columns.Length ? columns[target.ColumnIndex] ?? "" : "";
                string key = keyColumnIndex < columns.Length ? columns[keyColumnIndex] ?? "" : "";

                string s;
                if (key.StartsWith("//"))
                {
                    s = key;
                }
                else
                {
                    if (skipEmpty && string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    string n = value.Replace("\"", "\\\"");
                    s = $"\"{key}\" = \"{n}\";";
                }
                Debug.WriteLine(s);
                File.AppendAllText(target.FilePath, $"{s}\n");
            }
        }

        private List<string[]> LoadRowsFromSource()
        {
            string ext = Path.GetExtension(SourceFilePath).ToLowerInvariant();
            if (ext == ".xlsx")
            {
                return ReadXlsxRows(SourceFilePath);
            }

            return File.ReadAllLines(SourceFilePath)
                .Select(row => row.Split('\t'))
                .ToList();
        }

        private static int FindKeyRowIndex(IReadOnlyList<string[]> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                for (int c = 0; c < rows[i].Length; c++)
                {
                    string value = rows[i][c]?.Trim() ?? "";
                    if (value.StartsWith("key", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private static CellPosition FindCellPosition(IReadOnlyList<string[]> rows, string marker)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                for (int c = 0; c < rows[i].Length; c++)
                {
                    if (string.Equals(rows[i][c]?.Trim(), marker, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return new CellPosition
                        {
                            RowIndex = i,
                            ColumnIndex = c,
                        };
                    }
                }
            }
            return null;
        }

        private static int ResolveKeyColumnIndex(IReadOnlyList<string[]> rows, int keyRowIndex, CellPosition abbrCell, string preferredKeyHeader)
        {
            if (abbrCell != null)
            {
                return abbrCell.ColumnIndex;
            }

            string[] row = rows[keyRowIndex];

            if (!string.IsNullOrWhiteSpace(preferredKeyHeader))
            {
                for (int c = 0; c < row.Length; c++)
                {
                    if (string.Equals(row[c]?.Trim(), preferredKeyHeader.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        return c;
                    }
                }
            }

            for (int c = 0; c < row.Length; c++)
            {
                if (string.Equals(row[c]?.Trim(), "key", System.StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }

            for (int c = 0; c < row.Length; c++)
            {
                string value = row[c]?.Trim() ?? "";
                if (value.StartsWith("key", System.StringComparison.OrdinalIgnoreCase) &&
                    value.IndexOf("ios", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return c;
                }
            }

            for (int c = 0; c < row.Length; c++)
            {
                string value = row[c]?.Trim() ?? "";
                if (value.StartsWith("key", System.StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }

            return DEFAULT_KEY_COLUMN_INDEX;
        }

        private static bool LooksLikeLanguageCode(string value)
        {
            return Regex.IsMatch(value, "^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$");
        }

        private static List<string[]> ReadXlsxRows(string filePath)
        {
            using ZipArchive archive = ZipFile.OpenRead(filePath);

            List<string> sharedStrings = ReadSharedStrings(archive);
            string worksheetPath = ResolveFirstWorksheetPath(archive);

            ZipArchiveEntry worksheetEntry = archive.GetEntry(worksheetPath);
            if (worksheetEntry == null)
            {
                throw new IOException($"Worksheet not found: {worksheetPath}");
            }

            using Stream worksheetStream = worksheetEntry.Open();
            XDocument worksheetDoc = XDocument.Load(worksheetStream);
            XNamespace ns = worksheetDoc.Root?.Name.Namespace ?? XNamespace.None;

            IEnumerable<XElement> rowNodes = worksheetDoc
                .Descendants(ns + "sheetData")
                .Descendants(ns + "row");

            var rows = new List<string[]>();

            foreach (XElement rowNode in rowNodes)
            {
                var cellsByColumn = new Dictionary<int, string>();
                int maxColumnIndex = -1;

                foreach (XElement cell in rowNode.Elements(ns + "c"))
                {
                    string reference = cell.Attribute("r")?.Value ?? "";
                    int columnIndex = GetColumnIndex(reference);
                    if (columnIndex < 0)
                    {
                        continue;
                    }

                    string cellValue = GetCellValue(cell, sharedStrings, ns);
                    cellsByColumn[columnIndex] = cellValue;
                    if (columnIndex > maxColumnIndex)
                    {
                        maxColumnIndex = columnIndex;
                    }
                }

                if (maxColumnIndex < 0)
                {
                    continue;
                }

                string[] rowValues = new string[maxColumnIndex + 1];
                foreach ((int columnIndex, string value) in cellsByColumn)
                {
                    rowValues[columnIndex] = value;
                }

                rows.Add(rowValues);
            }

            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var sharedStrings = new List<string>();
            ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedStringsEntry == null)
            {
                return sharedStrings;
            }

            using Stream stream = sharedStringsEntry.Open();
            XDocument doc = XDocument.Load(stream);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            foreach (XElement si in doc.Descendants(ns + "si"))
            {
                string value = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                sharedStrings.Add(value);
            }

            return sharedStrings;
        }

        private static string ResolveFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relsEntry == null)
            {
                return "xl/worksheets/sheet1.xml";
            }

            using Stream workbookStream = workbookEntry.Open();
            XDocument workbookDoc = XDocument.Load(workbookStream);
            XNamespace workbookNs = workbookDoc.Root?.Name.Namespace ?? XNamespace.None;
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

            XElement firstSheet = workbookDoc.Descendants(workbookNs + "sheet").FirstOrDefault();
            string relationId = firstSheet?.Attribute(relNs + "id")?.Value;
            if (string.IsNullOrWhiteSpace(relationId))
            {
                return "xl/worksheets/sheet1.xml";
            }

            using Stream relsStream = relsEntry.Open();
            XDocument relsDoc = XDocument.Load(relsStream);
            XNamespace relsNs = relsDoc.Root?.Name.Namespace ?? XNamespace.None;

            XElement relation = relsDoc
                .Descendants(relsNs + "Relationship")
                .FirstOrDefault(r => string.Equals(r.Attribute("Id")?.Value, relationId, System.StringComparison.Ordinal));

            string target = relation?.Attribute("Target")?.Value;
            if (string.IsNullOrWhiteSpace(target))
            {
                return "xl/worksheets/sheet1.xml";
            }

            target = target.Replace('\\', '/');
            if (target.StartsWith("/"))
            {
                target = target.Substring(1);
            }

            if (!target.StartsWith("xl/"))
            {
                target = "xl/" + target.TrimStart('/');
            }

            return target;
        }

        private static int GetColumnIndex(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return -1;
            }

            int result = 0;
            bool hasLetter = false;
            foreach (char ch in reference)
            {
                if (char.IsLetter(ch))
                {
                    hasLetter = true;
                    result = (result * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
                }
                else
                {
                    break;
                }
            }

            return hasLetter ? result - 1 : -1;
        }

        private static string GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace ns)
        {
            string type = cell.Attribute("t")?.Value;
            if (type == "inlineStr")
            {
                return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
            }

            string raw = cell.Element(ns + "v")?.Value ?? "";
            if (type == "s" && int.TryParse(raw, out int sharedStringIndex))
            {
                if (sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
                {
                    return sharedStrings[sharedStringIndex];
                }
            }

            return raw;
        }

        private void OnBrowseClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "I18N Files (*.xlsx;*.tsv)|*.xlsx;*.tsv|Excel Files (*.xlsx)|*.xlsx|Tab Seperate Files (*.tsv)|*.tsv";

                if (openFileDialog.ShowDialog() == true)
                {
                    SourceFilePath = openFileDialog.FileName;
                    OutputPath = "";
                    ErrMsg = "File selected. Continue to Step 2 and Step 3.";
                }
            }
            catch (Exception ex)
            {
                ShowException("Browse", ex);
            }
        }

        private async void OnToTxtClicked(object sender, RoutedEventArgs e)
        {
            if (!ValidateSourceFile() || IsConverting)
            {
                return;
            }

            try
            {
                IsConverting = true;
                ErrMsg = "Converting...";
                await Task.Yield();

                ConversionResult result = await Task.Run(Convert2Txts);
                OutputPath = result.OutputPath;
                ErrMsg = result.Message;
            }
            catch (Exception ex)
            {
                ShowException("Convert to txt(s)", ex);
            }
            finally
            {
                IsConverting = false;
            }
        }

        private async void On2XCodeClicked(object sender, RoutedEventArgs e)
        {
            if (!ValidateSourceFile() || IsConverting)
            {
                return;
            }

            try
            {
                IsConverting = true;
                ErrMsg = "Converting...";
                await Task.Yield();

                ConversionResult result = await Task.Run(Convert2XCode);
                OutputPath = result.OutputPath;
                ErrMsg = result.Message;
            }
            catch (Exception ex)
            {
                ShowException("Convert to Xcode", ex);
            }
            finally
            {
                IsConverting = false;
            }
        }

        public void ShowException(string source, Exception ex)
        {
            ErrMsg = $"[{source}] {ex}";
        }
    }
}
