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
using System.Reflection;

namespace ConfigView
{
    /// <summary>
    /// Interaction logic for ConfigView.xaml
    /// </summary>
    public partial class ConfigView : UserControl
    {
        public Type ConfigType { private get; set; }

        private readonly Dictionary<UIElement, (PropertyInfo propertyInfo, bool needsRestart, object newValue)> bindingDict =
            new Dictionary<UIElement, (PropertyInfo propertyInfo, bool needsRestart, object newValue)>();

        public ConfigView()
        {
            InitializeComponent();
        }

        public void Initialize()
        {
            tabControl.Items.Clear();


            var members = ConfigType.GetNestedTypes();

            var categories = members
                .Where(n =>  n.GetCustomAttributes(false).Any(m => m is ICategoryAttribute))
                .Select(n =>(n, n.GetCustomAttributes(false).Single(m => m is ICategoryAttribute) as ICategoryAttribute))
                .Where(n =>n.Item2!=null);

            foreach (var (category, categoryAttr) in categories)
            {
                var options = category.GetProperties()
                    .Where(n => n.GetCustomAttributes(false).Any(m => m is IOptionAttribute))
                    .Select(n => (n, n.GetCustomAttributes(false).Single(m => m is IOptionAttribute) as IOptionAttribute))
                    .Where(n => n.Item2 != null);

                var tab = new TabItem() { Header = categoryAttr.Name };

                tabControl.Items.Add(tab);
                var scrollView = new ScrollViewer();
                var stack = new StackPanel();

                tab.Content = scrollView;
                scrollView.Content = stack;

                foreach (var (option, optionAttr) in options)
                {
                    var value = option.GetValue(null);


                    if (option.PropertyType == typeof(bool))
                    {
                        var checkBox = new CheckBox
                        {
                            IsChecked = (bool)value,
                            Content = optionAttr.Name.Replace('-',' '),
                        };

                        bindingDict[checkBox] = (option, optionAttr.RequiresRestart, value);

                        checkBox.Checked += CheckBox_Checked;
                        checkBox.Unchecked += CheckBox_Checked;

                        stack.Children.Add(checkBox);
                    }
                    else if(option.PropertyType == typeof(string) 
                        || option.PropertyType == typeof(int)
                        || option.PropertyType == typeof(double))
                    {
                        var stackPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                        };


                        var textbox = new TextBox
                        {
                            Text = (string)Convert.ChangeType(value, typeof(string)),
                        };

                        bindingDict[textbox] = (option, optionAttr.RequiresRestart, value);

                        textbox.KeyDown += Textbox_KeyDown;
                        textbox.TextChanged += Textbox_TextChanged;

                        stackPanel.Children.Add(new Label() { Content = optionAttr.Name.Replace('-', ' ') });
                        stackPanel.Children.Add(textbox);

                        stack.Children.Add(stackPanel);
                    }


                }

            }

        }

        private void Textbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var element = bindingDict[sender as UIElement];
            
            var val = Convert.ChangeType((sender as TextBox).Text, bindingDict[sender as UIElement].propertyInfo.PropertyType);

            bindingDict[sender as UIElement] = (element.propertyInfo, element.needsRestart, val);
        }

        private void Textbox_KeyDown(object sender, KeyEventArgs e)
        {
            var element = bindingDict[sender as UIElement];

            if(e.Key == Key.Escape)
            {
                (sender as TextBox).Text = (string)Convert.ChangeType(element.propertyInfo.GetValue(null), typeof(string));
            }
        }


        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var element = bindingDict[sender as UIElement];
            var val = Convert.ChangeType((sender as TextBox).Text, bindingDict[sender as UIElement].propertyInfo.PropertyType);
            bindingDict[sender as UIElement] = (element.propertyInfo, element.needsRestart, val);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            foreach (var option in bindingDict)
            {
                option.Value.propertyInfo.SetValue(null, option.Value.newValue);
            }
        }

    }
}
