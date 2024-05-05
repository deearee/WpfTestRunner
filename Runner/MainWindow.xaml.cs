using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Runner;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataGrid.ItemsSource = CollectionViewSource.GetDefaultView(new List<TestResult>());
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        // todo this modal can be closed...
        var modal = new Window
        {
            Width = 150,
            Height = 100,
            ResizeMode = ResizeMode.NoResize,
            Content = new TextBlock()
            {
                Text = "Running tests...", VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };

        var runFilePath = PathBox.Text;

        var task = Task.Run(() =>
        {
            try
            {
                var tempTrxPath = GetTempTrxFilePath();
                RunTests(tempTrxPath, runFilePath);
                return ParseResults(tempTrxPath);
            }
            catch(Exception e)
            {
                Trace.WriteLine(e.ToString());
                return [];
            }
        });

        Task.Factory.StartNew(
            () => modal.ShowDialog(),
            default,
            TaskCreationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext()
        );

        var testResults = await task;

        var view = CollectionViewSource.GetDefaultView(testResults);
        view.Filter = w => 
            ((TestResult)w).Name.ToLowerInvariant().Contains(SearchBox.Text.ToLowerInvariant()) && 
            (ComboBox.Text == "All" || ((TestResult)w).Outcome == (string)ComboBox.SelectionBoxItem);

        DataGrid.ItemsSource = view;

        modal.Close();
    }

    private void RunTests(string trxFilePath, string projectPath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments = $"test \"{projectPath}\" --logger \"trx;LogFileName={trxFilePath}\"",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process process = Process.Start(startInfo);

        process.WaitForExit();
    }

    private string GetTempTrxFilePath()
    {
        string tempFileName = System.IO.Path.GetTempFileName();
        return System.IO.Path.ChangeExtension(tempFileName, ".trx");
    }

    private List<TestResult> ParseResults(string trxFilePath)
    {
        string fileData = File.ReadAllText(trxFilePath);

        // Hack to avoid having to deal with namespaces in XPath
        fileData = fileData.Replace(" xmlns=\"", " noxmlns=\"");

        XDocument doc = XDocument.Load(new StringReader(fileData));

        return doc.XPathSelectElements("/TestRun/Results/*")
            .Select(test => new TestResult()
            {
                Name = test.Attribute("testName")?.Value ?? "Unknown",
                Outcome = test.Attribute("outcome")?.Value ?? "Unknown"
            }).ToList();
    }

    private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
    {
        // Refresh the table
        ((ICollectionView)DataGrid.ItemsSource)?.Refresh();
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Refresh the table later
        Task.Factory.StartNew(
            () => ((ICollectionView)DataGrid.ItemsSource)?.Refresh(),
            default,
            TaskCreationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext()
        );
    }
}
