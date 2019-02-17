using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Core;

namespace Editor
{
    /// <summary>
    /// Interaction logic for ErrorBox.xaml
    /// </summary>
    public partial class ErrorBox : UserControl, ICollection<IParserError>
    {
        private readonly List<IParserError> errors = new List<IParserError>();
        private readonly ObservableCollection<string> errorMessages = new ObservableCollection<string>();

        public int Count => errors.Count;

        public bool IsReadOnly => false;



        public ErrorBox()
        {
            InitializeComponent();
            Box.ItemsSource = errorMessages;
        }

        public void Add(IParserError item)
        {
            errors.Add(item);
            errorMessages.Add(item.Message);
        }

        public void Replace(IEnumerable<IParserError> items)
        {

            Clear();

            if (items == null)
                return;

            foreach (IParserError item in items)
            {
                if (item == null)
                    continue;

                Add(item);
            }
        }

        public void AddRange(IEnumerable<IParserError> items)
        {
            foreach (IParserError item in items)
            {
                Add(item);
            }
        }

        public void Clear()
        {
            errors.Clear();
            errorMessages.Clear();
        }

        public bool Contains(IParserError item)
        {
            return errors.Contains(item);
        }

        public void CopyTo(IParserError[] array, int arrayIndex)
        {
            //throw new Exception("messages cannot be copied");
            errors.CopyTo(array, arrayIndex);
        }

        public bool Remove(IParserError item)
        {
            errorMessages.Remove(item.Message);
            return errors.Remove(item);
        }

        public IEnumerator<IParserError> GetEnumerator()
        {
            return errors.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return errors.GetEnumerator();
        }
    }
}
