//#define USE_GAME_ENGINE_SERVICE

using System;
using System.Collections.Generic;
using System.ComponentModel;        // For BackgroundWorker
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using WPFOthello.GameEngine;
using GameEngine = WPFOthello.GameEngine.GameEngine;

namespace WPFOthello
{
    // See http://social.msdn.microsoft.com/Forums/en/wpf/thread/97656b9b-5d26-4e86-bbd5-d0a53e9c5ec0

    public static class WPFObjectCopier
    {
        public static T Clone<T>(T source)
        {
            string objXaml = XamlWriter.Save(source);
            StringReader stringReader = new StringReader(objXaml);
            XmlReader xmlReader = XmlReader.Create(stringReader);
            T t = (T)XamlReader.Load(xmlReader);
            return t;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IGameWindow
    {
        private GameEngine.GameEngine gameEngine;
        private readonly Dictionary<string, Canvas> canvasDictionary = new Dictionary<string, Canvas>();
        //private readonly Canvas canvasEmpty;
        private readonly Canvas canvasWhiteDisc;
        private readonly Canvas canvasBlackDisc;
        private readonly BackgroundWorker worker;

        public MainWindow()
        {
            InitializeComponent();

            worker = FindResource("backgroundWorker") as BackgroundWorker;

            SetUpImageGrid();

            //canvasEmpty = (Canvas)FindResource("EmptyCanvas");
            canvasWhiteDisc = (Canvas)FindResource("WhiteDisc");
            canvasBlackDisc = (Canvas)FindResource("BlackDisc");

            gameEngine = new GameEngine.GameEngine(this);

            automateWhite.IsChecked = gameEngine.isAutomated[SquareContentType.White];
            automateBlack.IsChecked = gameEngine.isAutomated[SquareContentType.Black];

            tbWhitePly.Text = this.gameEngine.playerPlyDictionary[SquareContentType.White].ToString();
            tbBlackPly.Text = this.gameEngine.playerPlyDictionary[SquareContentType.Black].ToString();

            BeginNewGame();
        }

        private void CopyCanvasContents(Canvas source, Canvas destination)
        {
            destination.Children.Clear();

            foreach (UIElement child in source.Children)
            {
                // TODO: Note: We may have to clone the child.
                //destination.Children.Add(child);
                destination.Children.Add(WPFObjectCopier.Clone<UIElement>(child));
            }
        }

        public void SetUpImageGrid()
        {
            boardGrid.Children.Clear();
            boardGrid.RowDefinitions.Clear();
            boardGrid.ColumnDefinitions.Clear();

            for (int i = 0; i < GameEngine.GameEngine.boardDimension; ++i)
            {
                var rowDefinition = new RowDefinition();
                var columnDefinition = new ColumnDefinition();

                rowDefinition.Height = new GridLength(1.0, GridUnitType.Star);
                columnDefinition.Width = new GridLength(1.0, GridUnitType.Star);
                boardGrid.RowDefinitions.Add(rowDefinition);
                boardGrid.ColumnDefinitions.Add(columnDefinition);
            }

            canvasDictionary.Clear();

            int index = 0;

            for (int row = 0; row < GameEngine.GameEngine.boardDimension; ++row)
            {

                for (int column = 0; column < GameEngine.GameEngine.boardDimension; ++column)
                {
                    var indexAsString = index.ToString();
                    var canvas = new Canvas();

                    Grid.SetRow(canvas, row);
                    Grid.SetColumn(canvas, column);
                    canvas.Name = "square" + indexAsString;
                    canvas.Tag = indexAsString;
                    boardGrid.Children.Add(canvas);
                    canvasDictionary[indexAsString] = canvas;
                    ++index;
                }
            }
        }

        public void PlacePiece(SquareContentType piece, int row, int column)
        {
            var squareIndex = row * GameEngine.GameEngine.boardDimension + column;
            var squareIndexAsString = squareIndex.ToString();

            if (!canvasDictionary.ContainsKey(squareIndexAsString))
            {
                DisplayMessage(string.Format("PlacePiece() : No square with tag '{0}'", squareIndexAsString));
                return;
            }

            var sourceCanvas = (piece == SquareContentType.White) ? canvasWhiteDisc : canvasBlackDisc;

            CopyCanvasContents(sourceCanvas, canvasDictionary[squareIndexAsString]);
        }

        public void DisplayMessage(string message)
        {
            messageLabel.Text = message;
        }

        public void DisplayTurnMessage() 
        {
            string turnMessage;

            if (this.gameEngine.IsGameNotOver())
            {
                turnMessage = this.gameEngine.playerNameDictionary[this.gameEngine.currentPlayer];

                if (this.gameEngine.isAutomated[this.gameEngine.currentPlayer]) 
                {
                    turnMessage = turnMessage + " is thinking...";
                }
                else
                {
                    turnMessage = turnMessage + "'s turn.";
                }
            }
            else
            {
                var whiteLead = this.gameEngine.playerPopulationDictionary[SquareContentType.White]
                    - this.gameEngine.playerPopulationDictionary[SquareContentType.Black];

                if (whiteLead > 0) 
                {
                    turnMessage = this.gameEngine.playerNameDictionary[SquareContentType.White] + " wins.";
                }
                else if (whiteLead < 0) 
                {
                    turnMessage = this.gameEngine.playerNameDictionary[SquareContentType.Black] + " wins.";
                }
                else 
                {
                    turnMessage = "Tie game.";
                }

                turnMessage = "Game over; " + turnMessage;
            }

            DisplayMessage(turnMessage);

            //$("#numberOfWhitePiecesID").html(PiecePopulations[WhiteNumber]);
            //$("#numberOfBlackPiecesID").html(PiecePopulations[BlackNumber]);
        }

        private void BeginNewGame()
        {

            foreach (var canvas in canvasDictionary.Values)
            {
                canvas.Children.Clear();
            }

            gameEngine.BeginNewGame();
            DisplayTurnMessage();

            if (this.gameEngine.isAutomated[this.gameEngine.currentPlayer])
            {
                AutomatedMove();
            }
        }

        private void btnNewGame_Click(object sender, RoutedEventArgs e)
        {
            BeginNewGame();
        }

        private void FindBestMoveWrapper(out int bestRow, out int bestColumn)   // Unused
        {
            this.Cursor = Cursors.Wait;

#if USE_GAME_ENGINE_SERVICE
            var gameEngineServiceClient = new GameEngineServiceClient();
            int bestSquareIndex = gameEngineServiceClient.FindBestMove(
                this.gameEngine.boardDimension,
                this.gameEngine.GetBoardAsString(),
                this.gameEngine.currentPlayer == SquareContentType.X,
                this.gameEngine.playerPlyDictionary[this.gameEngine.currentPlayer]);

            bestRow = bestSquareIndex / this.gameEngine.boardDimension;
            bestColumn = bestSquareIndex % this.gameEngine.boardDimension;
#else
            //int numBestMoves;

            this.gameEngine.FindBestMove(this.gameEngine.currentPlayer,
                this.gameEngine.playerPlyDictionary[this.gameEngine.currentPlayer],
                0, 0,
                true, out bestRow, out bestColumn /*, out numBestMoves */);
#endif
        }

        private void MoveHelper(int row, int column)
        {

            //for (; ; )
            //{

                if (row >= 0 && column >= 0)
                {
                    this.gameEngine.PlacePiece(this.gameEngine.currentPlayer, row, column, null, true);
                    this.gameEngine.noAutomatedMovePossible = 0;
                }
                else
                {
                    ++this.gameEngine.noAutomatedMovePossible;
                }

                DisplayTurnMessage();

                if (!this.gameEngine.IsGameNotOver())
                {
                    this.Cursor = Cursors.Arrow;
                    return;
                }

                this.gameEngine.currentPlayer = (this.gameEngine.currentPlayer == SquareContentType.White) ? SquareContentType.Black : SquareContentType.White;
                DisplayTurnMessage();

                if (!this.gameEngine.isAutomated[this.gameEngine.currentPlayer])
                {
                    this.Cursor = Cursors.Arrow;
                    return;
                }

                //FindBestMoveWrapper(out row, out column);
                AutomatedMove();
            //}
        }

        private void AutomatedMove()
        {
            this.Cursor = Cursors.Wait;
            worker.RunWorkerAsync();
            /*
            int bestRow;
            int bestColumn;

            FindBestMoveWrapper(out bestRow, out bestColumn);
            MoveHelper(bestRow, bestColumn);
             */
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (!this.gameEngine.IsGameNotOver() || this.gameEngine.isAutomated[this.gameEngine.currentPlayer])
            {
                return;
            }

            // Bust that funky disco move, white boy.
            Canvas canvasSender = sender as Canvas;

            if (canvasSender == null)
            {
                DisplayMessage("Image_MouseUp() : sender is not a Canvas");
                return;
            }

            int squareIndex;

            if (!int.TryParse(canvasSender.Tag.ToString(), out squareIndex))
            {
                DisplayMessage(string.Format("Image_MouseUp() : canvasSender.Tag '{0}' is not an int", canvasSender.Tag));
                return;
            }

            if (this.gameEngine.board[squareIndex] != SquareContentType.EmptySquare)
            {
                DisplayMessage(string.Format("Error: Square not empty.  {0}'s turn.", this.gameEngine.playerNameDictionary[this.gameEngine.currentPlayer]));
                return;
            }

            int row = squareIndex / GameEngine.GameEngine.boardDimension;
            int column = squareIndex % GameEngine.GameEngine.boardDimension;

            MoveHelper(row, column);
        }

        private void automateWhite_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkbox = sender as CheckBox;

            if (checkbox == null)
            {
                DisplayMessage("sender is not a CheckBox");
                return;
            }

            this.gameEngine.isAutomated[SquareContentType.White] = checkbox.IsChecked.HasValue && checkbox.IsChecked.Value;
        }

        private void automateBlack_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkbox = sender as CheckBox;

            if (checkbox == null)
            {
                DisplayMessage("sender is not a CheckBox");
                return;
            }

            this.gameEngine.isAutomated[SquareContentType.Black] = checkbox.IsChecked.HasValue && checkbox.IsChecked.Value;
        }

        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            BeginNewGame();
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void tbWhitePly_LostFocus(object sender, RoutedEventArgs e)
        {
            int ply;

            if (int.TryParse(tbWhitePly.Text, out ply) && ply >= GameEngine.GameEngine.minimumPly && ply <= GameEngine.GameEngine.maximumPly)
            {
                this.gameEngine.playerPlyDictionary[SquareContentType.White] = ply;
            }
            else
            {
                tbWhitePly.Text = this.gameEngine.playerPlyDictionary[SquareContentType.White].ToString();
            }
        }

        private void tbBlackPly_LostFocus(object sender, RoutedEventArgs e)
        {
            int ply;

            if (int.TryParse(tbBlackPly.Text, out ply) && ply >= GameEngine.GameEngine.minimumPly && ply <= GameEngine.GameEngine.maximumPly)
            {
                this.gameEngine.playerPlyDictionary[SquareContentType.Black] = ply;
            }
            else
            {
                tbBlackPly.Text = this.gameEngine.playerPlyDictionary[SquareContentType.Black].ToString();
            }
        }

        /*
        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (!worker.IsBusy)
            {
                this.Cursor = Cursors.Wait;
                worker.RunWorkerAsync();
                button.Content = "Cancel";
            }
            else
            {
                worker.CancelAsync();
            }
        }
         */

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
#if USE_GAME_ENGINE_SERVICE
            var gameEngineServiceClient = new GameEngineServiceClient();
            int bestSquareIndex = gameEngineServiceClient.FindBestMove(
                GameEngine.GameEngine.boardDimension,
                this.gameEngine.GetBoardAsString(),
                this.gameEngine.currentPlayer == SquareContentType.White,
                this.gameEngine.playerPlyDictionary[this.gameEngine.currentPlayer]);
            int bestRow = bestSquareIndex / GameEngine.GameEngine.boardDimension;
            int bestColumn = bestSquareIndex % GameEngine.GameEngine.boardDimension;

            e.Result = new BestMoveData(0, bestRow, bestColumn);
#else
            e.Result = this.gameEngine.FindBestMoveWrapper(this.gameEngine.currentPlayer,
                this.gameEngine.playerPlyDictionary[this.gameEngine.currentPlayer]);
#endif
            /*
            for (int i = 1; i <= 100; i++)
            {
                if (worker.CancellationPending)
                    break;

                Thread.Sleep(100);
                worker.ReportProgress(i);
            }
             */
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var bestMoveData = e.Result as BestMoveData;

            MoveHelper(bestMoveData.row, bestMoveData.column);
            /*
            this.Cursor = Cursors.Arrow;
            Console.WriteLine(e.Error.Message);
            button.Content = "Start";
             */
        }

        /*
        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }
         */
    }
}
