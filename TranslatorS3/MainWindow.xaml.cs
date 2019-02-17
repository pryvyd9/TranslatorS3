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
        private IParserResult SemanticParserResult { get; set; }

        private IEnumerable<IParsedToken> ParsedTokens => TokenParserResult.ParsedTokens;

        private Script script;

        private readonly Window logWindow;
        private bool shoulCloseLogWindow = false;

        private readonly Timer timer;

        // One second to idle before starting analyzing
        private const int timeToAnalize = 1000;



        public MainWindow()
        {
            InitializeComponent();

            EditorBox.ScriptChanged += EditorBox_ScriptChanged;
            EditorBox.ParsedTokensGetter = () => ParsedTokens;
            EditorBox.ErrorsGetter = () => GetErrors();
            EditorBox.ControlTerminalsGetter = () => Grammar.Nodes.Terminals.Where(n => n.IsControl);
            EditorBox.TabIndentGetter = () => Configuration.General.TabIndent;

            logWindow = new Window() { Content = new LogView.LogView(), Title = "Logger" };
            logWindow.Closing += LogWindow_Closing;

            timer = new Timer() { AutoReset = false };
            timer.Elapsed += Timer_Elapsed;
        }

        private void Update()
        {
            ParserManager.TokenParser.Script = script.Content;
            TokenParserResult = ParserManager.TokenParser.Parse();

            ParserManager.SyntaxParser.ParsedTokens = ParsedTokens;
            SyntaxParserResult = ParserManager.SyntaxParser.Parse();

            ParserManager.SemanticParser.ParsedTokens = ParsedTokens;
            SemanticParserResult = ParserManager.SemanticParser.Parse();

            EditorBox.Update();

            ErrorBox.Replace(GetErrors());
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
            Grammar = new Grammar();
            //Grammar.Load();
            FiniteAutomaton finiteAutomaton = new FiniteAutomaton();

            dynamic parser = ParserManager.InitializeParser(
                "GrammarParser.dll",
                "GrammarParser.GrammarParser",
                Configuration.Path.GrammarInputXml,
                Configuration.Parser.ShouldIncludeTerminalsFromInsideOfDefinedTokens,
                Configuration.Parser.ShouldConvertLeftRecursionToRight);


            Grammar.Parse(parser);


            // Parse finite automaton

            parser = ParserManager.InitializeParser(
                "FiniteAutomatonParser.dll",
                "FiniteAutomatonParser.FiniteAutomatonParser",
                Grammar.ClassTable,
                Grammar.Nodes,
                Grammar.UnclassifiedTerminals);

            finiteAutomaton.Parse(parser);
            //finiteAutomaton.Save();
            finiteAutomaton.Load();


            SaveGrammarTxt(Grammar);
            SaveGrammarFactorizedTxt(Grammar);
            //Grammar.Save();




            //ParserManager.InitializeParser(
            //    "SyntaxRecursiveParser.dll",
            //    "SyntaxRecursiveParser.SyntaxRecursiveParser",
            //    true,
            //    Grammar.ClassTable.TokenClasses.Forward(Grammar.ClassTable.UndefinedTokenClassName),
            //    Grammar.Nodes.Axiom);

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
               Grammar.ClassTable);


            // Create predescence table

            parser = ParserManager.InitializeParser(
                "PredescenceTableParser.dll",
                "PredescenceTableParser.PredescenceTableParser",
                Grammar.Nodes);

            var predescenceTable = new PredescenceTable();
            predescenceTable.Parse(parser);

            //var f = Microsoft.FSharp.Core.FuncConvert.ToFSharpFunc<Microsoft.FSharp.Core.Unit,IEnumerable<IParsedToken>>((x) => ParsedTokens);
            var f = (Func<IEnumerable<IParsedToken>>)(() => ParsedTokens);

            //new SyntaxPredescenceTableParser.SyntaxPredescenceTableParser(
            //    predescenceTable.Nodes,
            //    //() => ParsedTokens,
            //    f,
            //    //Microsoft.FSharp.Core.FuncConvert.ToFSharpFunc<Tuple<Microsoft.FSharp.Core.Unit,IEnumerable<IParsedToken>>>(() => ParsedTokens),
            //    //(Microsoft.FSharp.Core.FSharpFunc<Microsoft.FSharp.Core.Unit, IEnumerable<IParsedToken>>) ((Func<IEnumerable<IParsedToken>>)(() => ParsedTokens)),
            //    Grammar.Nodes
            //    );
            parser = ParserManager.InitializeParser(
                "SyntaxPredescenceTableParser.dll",
                "SyntaxPredescenceTableParser.SyntaxPredescenceTableParser",
                predescenceTable.Nodes,
                f,
                //Microsoft.FSharp.Core.FuncConvert.ToFSharpFunc<Microsoft.FSharp.Core.Unit, IEnumerable<IParsedToken>>((x) => ParsedTokens),
                Grammar.Nodes);

            SavePredescenceTableTxt(predescenceTable, Grammar);

        }




        private void LogWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!shoulCloseLogWindow)
            {
                e.Cancel = true;
                (sender as Window).Hide();
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(Update, DispatcherPriority.Background);
        }

        private void EditorBox_ScriptChanged()
        {
            if (!timer.Enabled)
                timer.Start();

            timer.Interval =  timeToAnalize;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Configuration.Load();

            if(Exists(Configuration.Path.ScriptTxt))
            {
                script = new Script
                {
                    Content = ReadAllText(Configuration.Path.ScriptTxt),
                    Name = Configuration.Path.ScriptTxt.Substring(Configuration.Path.ScriptTxt.LastIndexOf("\\")),
                };
            }
            else
            {
                script = new Script
                {
                    Content = string.Empty,
                    Name = "noname",
                };
            }

            InitializeParsers();

            EditorBox.SetDocument(script);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Save(Configuration.Path.ScriptTxt, script.Content);

            var errors = GetErrors().GroupBy(n => n.Tag).Select(n => $"{n.Key}\r\n{string.Join("\r\n", n.Select(m => m.Message))}");

            Save(Configuration.Path.ErrorsTxt, string.Join("\r\n\r\n", errors));

            SaveParsedTokensTxt(TokenParserResult);

            shoulCloseLogWindow = true;

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

        private static void SaveParsedTokensTxt(ITokenParserResult TokenParserResult)
        {
            Configuration.CreateDirectoryFromPath(Configuration.Path.ParsedNodesDirectory);

            var tables = TokenParserResult.ParsedTokens.Distinct(n=>n.Name).GroupBy(n => n.TokenClassId);

            IDictionary<string,int> GetTable(int classId)
            {
                return tables.First(m => m.Key == classId).Select((m, j) => (j, m.Name)).ToDictionary(m=>m.Name, m=>m.j);
            }

            try
            {
                var parsedNodes = TokenParserResult.ParsedTokens.Select((n, i) => $"{i,4} {n.Name,-10} {n.TokenClassId,2} " +
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
