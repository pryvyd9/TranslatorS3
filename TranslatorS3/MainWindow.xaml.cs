using System;
using System.Collections.Generic;
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
using TranslatorS3.Entities;
using Core;
using static System.IO.File;
using LogView;
using System.Windows.Threading;
using System.Timers;
using ScriptEditor;

using static ScriptEditor.Tag;
using CWindow = ConsoleWindow.ConsoleWindow;
using Executor;

namespace TranslatorS3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public CoolEditor ScriptEditor => scriptEditor;
        public ErrorPanel.ErrorPanel ErrorPanel => errorPanel;

        public event RoutedEventHandler ShowConfigClick;
        public event RoutedEventHandler ShowLoggerClick;
        public event RoutedEventHandler StepOverClick;
        public event RoutedEventHandler RunClick;

        public event RoutedEventHandler NewFileClick;
        public event RoutedEventHandler OpenFileClick;




        public MainWindow()
        {
            InitializeComponent();
        }

        private void ShowConfiguration_Click(object sender, RoutedEventArgs e)
        {
            ShowConfigClick?.Invoke(sender, e);
        }

        private void ShowLogger_Click(object sender, RoutedEventArgs e)
        {
            ShowLoggerClick?.Invoke(sender, e);
        }

        private void StepOver_Click(object sender, RoutedEventArgs e)
        {
            StepOverClick?.Invoke(sender, e);
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            RunClick?.Invoke(sender, e);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileClick?.Invoke(sender, e);
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            NewFileClick?.Invoke(sender, e);
        }
    }
}
