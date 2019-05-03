using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Input;

namespace ConsoleWindow
{

    public class ConsoleWindow : Window
    {
        private readonly ListBox past;
        private readonly TextBox present;

        private readonly Queue<Action<object>> inputs = new Queue<Action<object>>();

        private readonly ObservableCollection<object> items;




        public ConsoleWindow()
        {
            items = new ObservableCollection<object>();

            past = new ListBox
            {
                ItemsSource = items,
            };
            present = new TextBox
            {
                Height = 24,
                IsEnabled = false,
            };

            past.SetValue(Grid.RowProperty, 0);
            present.SetValue(Grid.RowProperty, 1);

            var grid = new Grid();

            var row1 = new RowDefinition();
            var row2 = new RowDefinition { Height = new GridLength(present.Height) };

            grid.RowDefinitions.Add(row1);
            grid.RowDefinitions.Add(row2);

            grid.Children.Add(past);
            grid.Children.Add(present);

            present.KeyDown += Present_KeyDown;

            Content = grid;
        }

        // Enforce input in present.
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            present.Focus();
        }

        private void Present_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (inputs.Count > 0)
                {
                    inputs.Dequeue()?.Invoke(present.Text);
                    if (inputs.Count == 0)
                    {
                        present.IsEnabled = false;
                    }
                }

                Output(present.Text);

                present.Text = string.Empty;
            }
        }



        public void Input(Action<object> input)
        {
            present.IsEnabled = true;

            inputs.Enqueue(input);
        }

        public void Output(object value)
        {
            items.Add(value);

            GetChildOfType<ScrollViewer>(past).PageDown();
        }


        private static T GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child);

                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
