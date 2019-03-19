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

namespace TranslatorS3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Grammar Grammar { get; set; }

        private ITokenParserResult TokenParserResult { get; set; }
        private IParserResult SyntaxParserResult { get; set; }
        private ISemanticParserResult SemanticParserResult { get; set; }

        private IEnumerable<IParsedToken> ParsedTokens => TokenParserResult.ParsedTokens;


        private IDocument document;

        private readonly Window logWindow;
        private bool shouldCloseLogWindow = false;

        private readonly Timer timer;

        // One second to idle before starting analyzing
        private const int TimeToAnalyze = 1000;

        private bool isTimerAssigned;
        private IDocument timerAssignedDocument;

        public MainWindow()
        {
            InitializeComponent();



            //EditorBox.ScriptChanged += EditorBox_ScriptChanged;
            //EditorBox.ParsedTokensGetter = () => ParsedTokens;
            //EditorBox.ErrorsGetter = () => GetErrors();
            //EditorBox.ControlTerminalsGetter = () => Grammar.Nodes.Terminals.Where(n => n.IsControl);
            //EditorBox.TabIndentGetter = () => Configuration.General.TabIndent;

            logWindow = new Window() { Content = new LogView.LogView(), Title = "Logger" };
            logWindow.Closing += LogWindow_Closing;

            timer = new Timer() { AutoReset = false };
            //timer.Elapsed += Timer_Elapsed;
        }


        private IEnumerable<IParserError> GetErrors()
        {
            if (SyntaxParserResult.Errors == null)
                return SemanticParserResult.Errors;

            if (SemanticParserResult.Errors == null)
                return SyntaxParserResult.Errors;

            var errors = SyntaxParserResult.Errors
               .Concat(SemanticParserResult.Errors);

            return errors;
        }

        private void InitializeParsers()
        {
            #region Grammar
            Grammar = new Grammar();
            //Grammar.Load();
            var finiteAutomaton = new FiniteAutomaton();

            dynamic parser = ParserManager.InitializeParser(
                "GrammarParser.dll",
                "GrammarParser.GrammarParser",
                Configuration.Path.GrammarInputXml,
                Configuration.Parser.ShouldIncludeTerminalsFromInsideOfDefinedTokens,
                Configuration.Parser.ShouldConvertLeftRecursionToRight);


            Grammar.Parse(parser);

            var controlTerminals = Grammar.Nodes.Terminals.Where(n => n.IsControl).Select(n => n.Name).ToArray();

            ScriptEditor.ApplyTextColor(document, controlTerminals, Color.FromRgb(0, 0, 255));

            // Parse finite automaton

            parser = ParserManager.InitializeParser(
                "FiniteAutomatonParser.dll",
                "FiniteAutomatonParser.FiniteAutomatonParser",
                Grammar.ClassTable,
                Grammar.Nodes,
                Grammar.UnclassifiedTerminals);

            finiteAutomaton.Parse(parser);
            //finiteAutomaton.Save();
            //finiteAutomaton.Load();


            SaveGrammarTxt(Grammar);
            SaveGrammarFactorizedTxt(Grammar);
            //Grammar.Save();

            #endregion


            ParserManager.InitializeParser(
                "SyntaxRecursiveParser.dll",
                "SyntaxRecursiveParser.SyntaxRecursiveParser",
                true,
                Grammar.ClassTable.TokenClasses.Forward(Grammar.ClassTable.UndefinedTokenClassName),
                Grammar.Nodes.Axiom);

            //PushdownAutomaton pushdownAutomaton = new PushdownAutomaton();
            //pushdownAutomaton.Load();


            //ParserManager.InitializeParser(
            //    "SyntaxPushdownParser.dll",
            //    "SyntaxPushdownParser.SyntaxPushdownParser",
            //    pushdownAutomaton,
            //    Grammar.Nodes,
            //    true,
            //    Grammar.ClassTable.TokenClasses.Forward(Grammar.ClassTable.UndefinedTokenClassName));

            ParserManager.InitializeParser(
               "TokenParser.dll",
               "TokenParser.TokenParser",
               Grammar.Nodes,
               Grammar.ClassTable,
               finiteAutomaton);

            ParserManager.InitializeParser(
               "SemanticParser.dll",
               "SemanticParser.SemanticParser",
               Grammar.ClassTable,
               Grammar.Nodes);


            #region Predescence
            //Create predescence table

            //parser = ParserManager.InitializeParser(
            //   "PredescenceTableParser.dll",
            //   "PredescenceTableParser.PredescenceTableParser",
            //   Grammar.Nodes);

            //var predescenceTable = new PredescenceTable();
            //predescenceTable.Parse(parser);

            //var f = (Func<IEnumerable<IParsedToken>>)(() => ParsedTokens);

            //parser = ParserManager.InitializeParser(
            //   "SyntaxPredescenceTableParserWithPOLIZ.dll",
            //   "SyntaxPredescenceTableParser.SyntaxPredescenceTableParser",
            //   predescenceTable.Nodes,
            //   f,
            //   Grammar.Nodes,
            //   Grammar.Nodes.Axiom);

            //parser = ParserManager.InitializeParser(
            //    "SyntaxPredescenceTableParser.dll",
            //    "SyntaxPredescenceTableParser.SyntaxPredescenceTableParser",
            //    predescenceTable.Nodes,
            //    f,
            //    Grammar.Nodes,
            //    Grammar.Nodes.Axiom);

            //SavePredescenceTableTxt(predescenceTable, Grammar);

            #endregion
        }




        private void LogWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!shouldCloseLogWindow)
            {
                e.Cancel = true;
                (sender as Window).Hide();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Configuration.Load();

            if(Exists(Configuration.Path.ScriptTxt))
            {
                document = new Document(ReadAllText(Configuration.Path.ScriptTxt))
                {
                    Name = Configuration.Path.ScriptTxt.Substring(Configuration.Path.ScriptTxt.LastIndexOf("\\") + 1),
                    Path = Configuration.Path.ScriptTxt,
                };
            }
            else
            {
                document = new Document(ReadAllText(string.Empty))
                {
                    Name = "noname",
                    Path = Configuration.Path.ScriptTxt,
                };
            }

            InitializeParsers();

            document.Updated += Document_Updated;

            ScriptEditor.OpenDocument(document);

            ScriptEditor.Focus(document);

            Document_Updated(document);
        }


        private void Update(IDocument document)
        {
            ParserManager.TokenParser.Script = document.Text;
            TokenParserResult = ParserManager.TokenParser.Parse();

            if (ParsedTokens == null)
            {
                ErrorPanel.ReplaceErrors(document, new IParserError[] { });

                return;
            }
            
            ParserManager.SyntaxParser.ParsedTokens = ParsedTokens;
            SyntaxParserResult = ParserManager.SyntaxParser.Parse();

            ParserManager.SemanticParser.ParsedTokens = ParsedTokens;
            SemanticParserResult = ParserManager.SemanticParser.Parse();



            var errors = GetErrors().ToArray();
            

            var semanticErrors = errors
               .Where(n => n.Tag == "semantic")
               .SelectMany(n => n.TokensOnError.Select(m => (m.InStringPosition, m.InStringPosition + m.Name.Length - 1)))
               .ToArray();

            var syntaxErrors = errors
                .Where(n => n.Tag == "syntax")
                .SelectMany(n => n.TokensOnError?.Select(m => (m.InStringPosition, m.InStringPosition + m.Name.Length - 1)))
                .ToArray();

            var lexicalErrors = errors
               .Where(n => n.Tag == "lexical")
               .SelectMany(n => n.TokensOnError.Select(m => (m.InStringPosition, m.InStringPosition + m.Name.Length - 1)))
               .ToArray();

            document.ResetHighlight();
            //document.ResetFormat();

            document.ApplyHighlight(semanticErrors, new[] { Semantic }, Brushes.GreenYellow);

            document.ApplyHighlight(syntaxErrors, new[] { Syntax }, Brushes.OrangeRed);

            document.ApplyHighlight(lexicalErrors, new[] { Lexical }, Brushes.Violet);

            ErrorPanel.ReplaceErrors(document, errors);

            //ErrorBox.Replace(errors);
        }

        private void Document_Updated(IDocument document)
        {
            if (!timer.Enabled)
                timer.Start();

            timer.Interval = TimeToAnalyze;

            if (isTimerAssigned || timerAssignedDocument == document) return;

            timer.Elapsed += (sender, e) =>
            {
                Dispatcher.Invoke(() => Update(document), DispatcherPriority.Background);
            };
            isTimerAssigned = true;
            timerAssignedDocument = document;

        }

        

        private void Window_Closed(object sender, EventArgs e)
        {
            Save(Configuration.Path.ScriptTxt, document.Text);

            if (SyntaxParserResult != null)
            {
                var errors = GetErrors().GroupBy(n => n.Tag).Select(n => $"{n.Key}\r\n{string.Join("\r\n", n.Select(m => m.Message))}");

                Save(Configuration.Path.ErrorsTxt, string.Join("\r\n\r\n", errors));
            }

            SaveParsedTokensTxt(TokenParserResult);

            shouldCloseLogWindow = true;

            logWindow.Close();

            Configuration.Save();
        }

        private void ShowConfiguration_Click(object sender, RoutedEventArgs e)
        {
            ConfigView.ConfigView configView = new ConfigView.ConfigView();

            configView.ConfigType = typeof(Configuration);

            configView.Initialize();

            Window window = new Window
            {
                Content = configView,
                Height = 350,
                Width = 600,
            };
            window.ShowDialog();
        }

        private void ShowLogger_Click(object sender, RoutedEventArgs e)
        {
            if(logWindow.WindowState == WindowState.Minimized)
            {
                logWindow.WindowState = WindowState.Normal;
            }

            if (logWindow.IsVisible == false)
            {
                logWindow.Visibility = Visibility.Visible;
            }
            else
            {
                logWindow.Show();
            }
        }




        private static void Save(string path, string contents)
        {
            Configuration.CreateDirectoryFromPath(path);

            WriteAllText(path, contents);
        }

        private static void SaveGrammarFactorizedTxt(Grammar grammar)
        {
            Configuration.CreateDirectoryFromPath(Configuration.Path.GrammarFactorizedTxt);

            var copies = grammar.Nodes.Unsorted;

            var list = copies.Select(n =>
            {
                if (n is IMedium m)
                {
                    return $"{n}::={(n as IFactor).ToString()}";
                }

                if (n is IClass c)
                {
                    return $"{n}::={string.Join("|", c.Symbols.OrderBy(k => k))}";
                }

                return null;
            }).Where(n => !string.IsNullOrWhiteSpace(n));

            WriteAllLines(Configuration.Path.GrammarFactorizedTxt, list);

        }

        private static void SaveGrammarTxt(Grammar grammar)
        {
            Configuration.CreateDirectoryFromPath(Configuration.Path.GrammarTxt);

            var copies = grammar.Nodes.Unsorted;

            var list = copies.Select(n =>
            {
                if (n is IMedium m)
                {
                    return n.ToString() + "::=" + string.Join("|", m.Cases.Select(k => string.Join("", k.Count() == 0 ? new List<string> { "^" } : k.Select(j => j.ToString()))));
                }

                if (n is IClass c)
                {
                    return $"{n}::={string.Join("|", c.Symbols.OrderBy(k => k))}";
                }

                return null;
            }).Where(n => !string.IsNullOrWhiteSpace(n));

            WriteAllLines(Configuration.Path.GrammarTxt, list);

        }

        private static void SavePredescenceTableTxt(PredescenceTable table, Grammar grammar)
        {
            Configuration.CreateDirectoryFromPath(Configuration.Path.PredescenceTableTxt);

            List<string> ignoredNodes = new List<string> { "tail", "head", "digit" };

            var unsorted = grammar.Nodes.Unsorted;

            var sortedNodes = unsorted.OfType<IMedium>().Where(n => !(n is IDefinedToken)).Cast<INode>()
                .Concat(unsorted.OfType<IClass>())
                .Concat(unsorted.OfType<IDefinedToken>().Where(n => !(n is ITerminal)))
                .Concat(unsorted.OfType<ITerminal>())
                .Where(n => !ignoredNodes.Contains(n.Name));



            List<string> content = new List<string>();

            const int firstColumnWidth = 12;

            content.Add(string.Join("",  sortedNodes.Select(n => GetColumn(n,n.ToString()))
                .Prepend($"{string.Empty,firstColumnWidth}|")
                .Append("#|")));

            foreach (var verticalNode in sortedNodes)
            {
                if (table.Nodes.ContainsKey(verticalNode.Id))
                {
                    string str = $"{verticalNode.ToString(),firstColumnWidth}|";

                    foreach (var horizontalNode in sortedNodes)
                    {
                        if (table.Nodes[verticalNode.Id].Relashionships.ContainsKey(horizontalNode.Id))
                        {
                            str += GetColumn(horizontalNode, GetSign(verticalNode.Id, horizontalNode.Id));
                        }
                        else
                        {
                            str += GetColumn(horizontalNode, string.Empty);
                        }
                    }

                    // Add each > #
                    str += ">|";


                    content.Add(str);
                }
            }

            // Add # < each
            content.Add(string.Join("",sortedNodes.Select(node=>GetColumn(node, "<")).Prepend($"{"#",firstColumnWidth}|")));


            Logger.AddRange("predescenceTable", content);

            WriteAllLines(Configuration.Path.PredescenceTableTxt, content);

            return;

            string GetSign(int verticalId, int horizontalId)
            {
                switch (table.Nodes[verticalId].Relashionships[horizontalId])
                {
                    case Relationship.Undefined:
                        return string.Empty;
                    case Relationship.Greater:
                        return ">";
                    case Relationship.Lower:
                        return "<";
                    case Relationship.Equal:
                        return "=";
                    default:
                        return table.Nodes[verticalId].Relashionships[horizontalId].ToString();
                }
            }

            string GetColumn(INode horizontalNode, string str)
            {
                return string.Format("{0,-" + horizontalNode.ToString().Length + "}|", str);
            }
        }

        private static void SaveParsedTokensTxt(ITokenParserResult tokenParserResult)
        {
            Configuration.CreateDirectoryFromPath(Configuration.Path.ParsedNodesDirectory);

            if (tokenParserResult.ParsedTokens is null || !tokenParserResult.ParsedTokens.Any())
            {
                return;
            }

            var tables = tokenParserResult.ParsedTokens.Distinct(n=>n.Name).GroupBy(n => n.TokenClassId);

            IDictionary<string,int> GetTable(int classId)
            {
                return tables.First(m => m.Key == classId).Select((m, j) => (j, m.Name)).ToDictionary(m=>m.Name, m=>m.j);
            }

            try
            {
                var parsedNodes = tokenParserResult.ParsedTokens.Select((n, i) => $"{i,4} {n.Name,-10} {n.TokenClassId,2} " +
                    $"{GetTable(n.TokenClassId)[n.Name]}");

                var identifiers = GetTable(1).Select(n => $"{n.Value,4} {n.Key}");
                var constants = GetTable(3).Select(n => $"{n.Value,4} {n.Key}");
                var labels = GetTable(2).Select(n => $"{n.Value,4} {n.Key}");


                WriteAllLines(Configuration.Path.ParsedNodesDirectory + "parsed-nodes.txt", parsedNodes);
                WriteAllLines(Configuration.Path.ParsedNodesDirectory + "identifiers.txt", identifiers);
                WriteAllLines(Configuration.Path.ParsedNodesDirectory + "constants.txt", constants);
                WriteAllLines(Configuration.Path.ParsedNodesDirectory + "labels.txt", labels);
            }
            catch { }
        }

    }
}
