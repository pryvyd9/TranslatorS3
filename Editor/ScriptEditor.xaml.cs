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
using Core;

namespace Editor
{
    public delegate void ScriptChangedEventHandler();

    class ErrorComparer : Comparer<IParserError>
    {
        public override int Compare(IParserError x, IParserError y)
        {
            return GetValue(x).CompareTo(GetValue(y));
        }

        private int GetValue(IParserError v)
        {
            switch (v.Tag)
            {
                case "syntax":
                    return 0;
                case "lexical":
                    return 1;
                case "semantic":
                    return 2;
                default:
                    return 3;
            }
        }
    }


    /// <summary>
    /// Interaction logic for ScriptEditor.xaml
    /// </summary>
    public partial class ScriptEditor : UserControl
    {
        public Func<IEnumerable<ITerminal>> ControlTerminalsGetter { private get; set; }

        public Func<IEnumerable<IParsedToken>> ParsedTokensGetter { private get; set; }

        public Func<IEnumerable<IParserError>> ErrorsGetter { private get; set; }

        public event ScriptChangedEventHandler ScriptChanged;

        public Func<int> TabIndentGetter { private get; set; }


        public Color ControlTerminalColor { get; set; } = Color.FromRgb(0, 0, 255);

        public Color SyntaxErrorTerminalColor { get; set; } = Color.FromRgb(255, 100, 100);

        public Color SemanticErrorTerminalColor { get; set; } = Color.FromRgb(100, 255, 100);

        public Color LexicalErrorTerminalColor { get; set; } = Color.FromRgb(255, 100, 255);




        private int defaultTabIndent = 4;

        private int TabIndent => TabIndentGetter?.Invoke() ?? defaultTabIndent;

        private TextPointer Start => Box.Document.ContentStart;
        private TextPointer End => Box.Document.ContentEnd;

        private TextRange All => new TextRange(Start, End);

        private IDocument document;

        private bool mustUpdate = true;
        private const double documentWidthSizeModifier = 12;
        



        public ScriptEditor()
        {
            InitializeComponent();
        }

        public void SetDocument(IDocument document)
        {
            this.document = document;
            All.Text = document.Content;
        }

        public void Update()
        {
            if (ParsedTokensGetter == null)
                return;

            Highlight();
        }

        private void Box_KeyDown(object sender, KeyEventArgs e)
        {
            //var token = GetTerminalAtPosition(Box.CaretPosition);

            if (e.Key == Key.Tab)
            {
                var richTextBox = sender as RichTextBox;
                if (richTextBox == null) return;

                if (richTextBox.Selection.Text != string.Empty)
                    richTextBox.Selection.Text = string.Empty;

                TextPointer caretPosition = richTextBox.CaretPosition;

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    RemoveTab(caretPosition);
                }
                else
                {
                    AddTab(caretPosition);
                }

                e.Handled = true;

            }

        }

        private void Highlight()
        {
            mustUpdate = false;

            All.ClearAllProperties();

            if (ParsedTokensGetter?.Invoke() != null && ControlTerminalsGetter?.Invoke() != null)
            {
                var tokens = ParsedTokensGetter().Where(n => ControlTerminalsGetter().Any(m => m.Id == n.Id));

                // Paint control terminals
                foreach (var token in tokens)
                {
                    var selection = Select(token);

                    selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(ControlTerminalColor));
                }

            }

            if(ErrorsGetter?.Invoke() != null)
            {
                var sortedErrors = ErrorsGetter().OrderBy(n => n, new ErrorComparer()).Reverse();

                foreach (var error in sortedErrors)
                {
                    if (error.TokensOnError == null)
                        continue;

                    SolidColorBrush brush = GetBrush(error);

                    foreach (var token in error.TokensOnError)
                    {
                        var selection = Select(token);

                        selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
                    }
                }
            }

            mustUpdate = true;
        }


        private SolidColorBrush GetBrush(IParserError error)
        {
            switch (error.Tag)
            {
                case "syntax":
                    return new SolidColorBrush(SyntaxErrorTerminalColor);
                case "semantic":
                    return new SolidColorBrush(SemanticErrorTerminalColor);
                case "lexical":
                    return new SolidColorBrush(LexicalErrorTerminalColor);
                case "system":
                    return new SolidColorBrush(LexicalErrorTerminalColor);
                default:
                    throw new Exception("error category " + error.Tag + "is not supported");
            }
        }

        private IParsedToken GetTerminalAtPosition(TextPointer pointer)
        {
            int inTextPosition = GetInTextPosition(pointer);

            var token = ParsedTokensGetter()
                .Where(n => n.InRowPosition <= inTextPosition)
                .Last();

            return token;
        }

        private void AddTab(TextPointer caretPosition)
        {
            caretPosition = Box.CaretPosition.GetPositionAtOffset(0, LogicalDirection.Forward);
            Box.CaretPosition.InsertTextInRun(new string(' ', TabIndent));
            Box.CaretPosition = caretPosition;
        }

        private void RemoveTab(TextPointer caretPosition)
        {
            int pos = GetInTextPosition(caretPosition);

            bool isAtLineStart = caretPosition.IsAtLineStartPosition;

            if (isAtLineStart)
            {
                return;
            }

            var selection = Select(pos - TabIndent, TabIndent);

            // Count spaces at the end of selection.
            int spaceCount = selection.Text.Reverse().TakeWhile(n => n == ' ').Count();

            if (spaceCount == 0)
            {
                return;
            }

            // If there are less then Configuration.TabIndent
            // spaces before the caret then remove as many as possible.
            if (spaceCount < TabIndent)
            {
                selection.Text = selection.Text
                    .Substring(0, selection.Text.Length - spaceCount);
            }
            else
            {
                selection.Text = string.Empty;
            }

            Box.CaretPosition = selection.End;

        }


        public int GetInTextPosition(TextPointer textPointer)
        {
            int inTextPosition = 0;
            var inDocumentIterator = Start;

            foreach (var para in Box.Document.Blocks.OfType<Paragraph>())
            {

                //var e = s.GetPositionAtOffset(1);
                //var r = new TextRange(s, e).Text;

                // Paragraph starts with hidden character
                if (inDocumentIterator.GetOffsetToPosition(Start) == textPointer.GetOffsetToPosition(Start))
                {
                    return inTextPosition;
                }
                Move();

                foreach (var run in para.Inlines.OfType<Run>())
                {
                    //e = s.GetPositionAtOffset(1);
                    //r = new TextRange(s, e).Text;

                    // Run starts with hidden character
                    if (inDocumentIterator.GetOffsetToPosition(Start) == textPointer.GetOffsetToPosition(Start))
                    {
                        return inTextPosition;
                    }
                    Move();
                    foreach (var c in run.Text)
                    {
                        //e = s.GetPositionAtOffset(1);
                        //r = new TextRange(s, e).Text;


                        if (inDocumentIterator.GetOffsetToPosition(Start) == textPointer.GetOffsetToPosition(Start))
                        {
                            return inTextPosition;
                        }
                        inTextPosition++;
                        Move();

                    }

                    // Run ends with hidden character
                    if (inDocumentIterator.GetOffsetToPosition(Start) == textPointer.GetOffsetToPosition(Start))
                    {
                        return inTextPosition;
                    }
                    Move();
                }

                // Paragraph ends with hidden character
                if (inDocumentIterator.GetOffsetToPosition(Start) == textPointer.GetOffsetToPosition(Start))
                {
                    return inTextPosition;
                }
                Move();

                inTextPosition += 2;
            }



            return inTextPosition;

            void Move()
            {
                inDocumentIterator = inDocumentIterator.GetPositionAtOffset(1);
            }
        }


        private TextRange Select(int start, int length)
        {
            int inTextPosition = 0;
            TextPointer startSelectionPoint = null;
            TextPointer endSelectionPoint = null;
            var inDocumentIterator = Start;

            foreach (var para in Box.Document.Blocks.OfType<Paragraph>())
            {

                //var e = s.GetPositionAtOffset(1);
                //var r = new TextRange(s, e).Text;

                // Paragraph starts with hidden character
                MoveForward();

                foreach (var run in para.Inlines.OfType<Run>())
                {
                    //e = s.GetPositionAtOffset(1);
                    //r = new TextRange(s, e).Text;

                    // Run starts with hidden character
                    MoveForward();
                    foreach (var c in run.Text)
                    {
                        //e = s.GetPositionAtOffset(1);
                        //r = new TextRange(s, e).Text;

                        if (start == inTextPosition)
                        {
                            startSelectionPoint = inDocumentIterator;
                        }

                        inTextPosition++;



                        MoveForward();


                        if (start + length == inTextPosition)
                        {
                            endSelectionPoint = inDocumentIterator;
                        }


                    }

                    // Select symbols \r\n which are at the end of line
                    // if no start point is selected.
                    // If start point is not at \r\n then
                    // start point will be assigned with proper point and will not
                    // be reassigned later.
                    if (startSelectionPoint is null)
                    {
                        startSelectionPoint = inDocumentIterator;
                    }
                    // Run ends with hidden character
                    MoveForward();
                }

                // Paragraph end with hidden character
                MoveForward();

                // Next 2 characters are \r\n
                // They are excluded from iteration
                // but when needed they can be gotten from
                // text string.
                // Such a need arises when start point of
                // the selection is located at \r \n characters
                inTextPosition += 2;
            }


            if (start < 0)
            {
                startSelectionPoint = Start;
            }

            var tt = new TextRange(startSelectionPoint, endSelectionPoint);

            return tt;

            void MoveForward()
            {
                inDocumentIterator = inDocumentIterator.GetPositionAtOffset(1);
            }
        }

        private TextRange Select(IParsedToken token)
        {
            return Select(token.InStringPosition, token.Name.Length);
        }

        private void Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!mustUpdate)
                return;

            document.Content = All.Text;

            if(Box.Document.Blocks.Count > 0)
            {
                Box.Document.PageWidth = Box.Document.Blocks
                    .Max(n => new TextRange(n.ContentStart, n.ContentEnd).Text.Length) * documentWidthSizeModifier;
            }

            var start = RowIndexer.Document.ContentStart;
            var end = RowIndexer.Document.ContentEnd;

            new TextRange(start, end).Text = string.Join("\r\n", Box.Document.Blocks.Select((n, i) => i + 1));
            
            ScriptChanged?.Invoke();
        }

        private void Box_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender == Box)
            {
                RowIndexer.ScrollToVerticalOffset(e.VerticalOffset);
            }
            else
            {
                Box.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }

        private void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if(files.Length > 1)
                {
                    MessageBox.Show("Only first file will be dropped.");
                }

                if (System.IO.Path.GetExtension(files[0]) != ".txt")
                {
                    MessageBox.Show("Only text files are allowed to drop");
                    return;
                }

                All.Text = System.IO.File.ReadAllText(files[0]);

                document.Name = files[0].Substring(files[0].LastIndexOf("\\"));
            }
        }

        private void UserControl_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}
