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
using System.Collections.ObjectModel;
using Core;

namespace ErrorPanel
{
    /// <summary>
    /// Interaction logic for ErrorPanel.xaml
    /// </summary>
    public partial class ErrorPanel : UserControl
    {
        private Dictionary<object, List<object>> errors = new Dictionary<object, List<object>>();

        public ErrorPanel()
        {
            InitializeComponent();
        }

        public void ReplaceErrors(dynamic document, IEnumerable<IParserError> errors)
        {
            ShowErrors(document.Name, errors);
            //ShowErrors(ErrorTable, errors);
        }

        private void ShowErrors(string documentName, IEnumerable<IParserError> errors)
        {
            Box.Items.Clear();

            foreach (var error in errors)
            {
                Box.Items.Add(error.Message + " in " + documentName);
            }
        }

        private void ShowErrors(Table table, IEnumerable<IParserError> errors)
        {
            table.RowGroups.Clear();

            TableRowGroup group = new TableRowGroup();

            foreach (var error in errors)
            {
                var message = new TableCell(new Paragraph(new Run(error.Message)));
                var tag = new TableCell(new Paragraph(new Run(error.Tag)));

                var row = new TableRow();
                row.Cells.Add(message);
                row.Cells.Add(tag);

                group.Rows.Add(row);
            }

            table.RowGroups.Add(group);
        }



    }
}
