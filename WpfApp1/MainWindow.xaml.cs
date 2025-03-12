using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const string OUTPUT_PREFIX = "I18N";
        private const int HEADER_ROW_LINE = 2;
        private const int KEY_COLUMN_INDEX = 0;

        private List<string> files = new List<string>();

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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Tab Seperate Files (*.tsv)|*.tsv";
			if(openFileDialog.ShowDialog() == true)
            {
                SourceFilePath = openFileDialog.FileName;
            }
            else
            {
                ErrMsg = "Open file failed";
            }
        }

        private bool Convert2Txts()
        {
            ErrMsg = "Converting...";
            files.Clear();
            string folderName = $"{OUTPUT_PREFIX}-txts";
            System.IO.Directory.CreateDirectory(folderName);

            string[] rows = File.ReadAllLines(SourceFilePath);
            if (rows.Length <= HEADER_ROW_LINE)
            {
                ErrMsg = $"No Body\nrows.Length <= {HEADER_ROW_LINE}";
                return false;
            }

            for (int r = 0; r < rows.Length; r++)
            {
                string[] columns = rows[r].Split('\t');
                if (columns.Length <= 2)
                {
                    ErrMsg = "columns.Length <= 2";
                    return false;
                }

                if (r == 0)
                {
                    if (columns[KEY_COLUMN_INDEX].ToLower() != "key")
                    {
                        ErrMsg = $"row[{r}] column[{KEY_COLUMN_INDEX}] ToLower() != \"key\"";
                        return false;
                    }

                    for (int c = KEY_COLUMN_INDEX + 1; c < columns.Length; c++)
                    {
                        string filePath = $@"{folderName}/{columns[c]}.txt";
                        if(File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        FileStream fs = File.Create(filePath);
                        fs.Close();
                        files.Add(filePath);
                    }
                }
                else if (r >= HEADER_ROW_LINE)
                {
                    WriteBody(columns, KEY_COLUMN_INDEX + 1);
                }
            }

            ErrMsg = "Completed";
            return true;
        }

        private bool Convert2XCode()
        {
            ErrMsg = "Converting...";
            files.Clear();
            string folderName = $"{OUTPUT_PREFIX}-XCode";
            System.IO.Directory.CreateDirectory(folderName);

            string[] rows = File.ReadAllLines(SourceFilePath);
            if (rows.Length <= HEADER_ROW_LINE)
            {
                ErrMsg = $"No Body\nrows.Length <= {HEADER_ROW_LINE}";
                return false;
            }

            for (int r = 0; r < rows.Length; r++)
            {
                string[] columns = rows[r].Split('\t');
                if (columns.Length <= 2)
                {
                    ErrMsg = "columns.Length <= 2";
                    return false;
                }

                if (r == 1)
                {
                    if (columns[KEY_COLUMN_INDEX].ToLower() != "abbr")
                    {
                        ErrMsg = $"row[{r}] column[{KEY_COLUMN_INDEX}] ToLower() != \"abbr\"";
                        return false;
                    }

                    for (int c = KEY_COLUMN_INDEX + 1; c < columns.Length; c++)
                    {
                        if (columns[c] == string.Empty)
                        {
                            continue;
                        }
                        string dirPath = $@"{folderName}/{columns[c]}.lproj";
                        if (Directory.Exists(dirPath))
                        {
                            Directory.Delete(dirPath, true);
                        }
                        Directory.CreateDirectory(dirPath);

                        string filePath = $@"{dirPath}/Localizable.strings";
                        if(File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        FileStream fs = File.Create(filePath);
                        fs.Close();

                        files.Add(filePath);
                    }
                }
                else if (r >= HEADER_ROW_LINE)
                {
                    WriteBody(columns, KEY_COLUMN_INDEX + 1);
                }
            }

            ErrMsg = "Completed";
            return true;
        }

        private void WriteBody(string[] columns, int startIndex)
        {
            int fileIndex = 0;
            for (int c = startIndex; c < startIndex + files.Count; c++)
            {
                string s = "";
                if (columns[KEY_COLUMN_INDEX].StartsWith("//"))
                {
                    s = $"{columns[KEY_COLUMN_INDEX]}";
                }
                else
                {
                    string n = columns[c].Replace("\"", "\\\"");
                    s = $"\"{columns[KEY_COLUMN_INDEX]}\" = \"{n}\";";
                }
                Debug.WriteLine(s);
                File.AppendAllText(files[fileIndex], $"{s}\n");

                fileIndex++;
            }
        }

        private void OnToTxtClicked(object sender, RoutedEventArgs e)
        {
            Convert2Txts();
        }

        private void On2XCodeClicked(object sender, RoutedEventArgs e)
        {
            Convert2XCode();
        }
    }
}
