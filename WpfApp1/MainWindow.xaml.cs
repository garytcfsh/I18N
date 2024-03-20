using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string folderName = "I18N";
        private List<string> files = new List<string>();

        public string sourceFilePath = null;
        public string errMsg = "";

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
                sourceFilePath = openFileDialog.FileName;
				srcFilePath.Content = openFileDialog.FileName;

                Convert2Xcode();
            }
            else
            {
                errMsg = "Open file failed";
            }

            statusLabel.Content = errMsg;
        }

        private bool Convert2Xcode()
        {
            System.IO.Directory.CreateDirectory(folderName);

            string[] rows = File.ReadAllLines(sourceFilePath);
            if (rows.Length <= 1)
            {
                errMsg = "rows.Length <= 1";
                return false;
            }

            for (int r = 0; r < rows.Length; r++)
            {
                string[] columns = rows[r].Split('\t');
                if (columns.Length <= 1)
                {
                    errMsg = "columns.Length <= 1";
                    return false;
                }


                if (r == 0)
                {
                    if (columns[0].ToLower() != "key")
                    {
                        errMsg = "columns[0].ToLower() != \"key\"";
                        return false;
                    }

                    for (int c = 1; c < columns.Length; c++)
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
                else
                {
                    for (int c = 1; c < columns.Length; c++)
                    {
                        string s = "";
                        if (columns[0].StartsWith("//"))
                        {
                            s = $"{columns[0]}";
                        }
                        else
                        {
                            string n = columns[c].Replace("\"", "\\\"");
                            s = $"\"{columns[0]}\" = \"{n}\";";
                        }
                        Debug.WriteLine(s);
                        File.AppendAllText(files[c - 1], $"{s}\n");
                    }
                }
            }

            errMsg = "Completed";
            return true;
        }
    }
}
