using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WPFOthello.GameEngine
{
    public enum SquareContentType
    {
        EmptySquare = -1,
        White = 0,
        Black = 1
    }

    public interface IGameWindow
    {
        void PlacePiece(SquareContentType piece, int row, int column);
        void DisplayMessage(string message);
    }

    public class BestMoveData
    {
        public readonly int score;
        public readonly int row;
        public readonly int column;

        public BestMoveData(int score, int row, int column)
        {
            this.score = score;
            this.row = row;
            this.column = column;
        }
    }

    public class GameEngine
    {
        public const int boardDimension = 8;
        public const int boardArea = boardDimension * boardDimension;      // board.Length may make this unnecessary.
        public const int numDirections = 8;
        public readonly int[] adx = { -1, 0, 1, -1, 1, -1, 0, 1 };          // adx.Length == numDirections
        public readonly int[] ady = { -1, -1, -1, 0, 0, 1, 1, 1 };          // ady.Length == numDirections
        public readonly SquareContentType[] board;
        public SquareContentType currentPlayer;
        public readonly Dictionary<SquareContentType, string> playerNameDictionary = new Dictionary<SquareContentType, string>();
        public readonly Dictionary<SquareContentType, bool> isAutomated = new Dictionary<SquareContentType, bool>();
        public const int defaultPly = 5;
        public const int minimumPly = 1;
        public const int maximumPly = 9;
        public readonly Dictionary<SquareContentType, int> playerPlyDictionary = new Dictionary<SquareContentType, int>();
        public readonly Dictionary<SquareContentType, int> playerPopulationDictionary = new Dictionary<SquareContentType, int>();
        public IGameWindow gameWindow;
        public int boardPopulation;
        //public bool isGameOver;
        public int noAutomatedMovePossible = 0;

        public GameEngine(IGameWindow gameWindow, string boardAsString = null)
        {

            if (gameWindow == null)
            {
                throw new ArgumentNullException("gameWindow", "GameEngine constructor: The gameWindow parameter is null.");
            }

            this.gameWindow = gameWindow;

            this.board = new SquareContentType[boardArea];         // The board is always square.
            this.playerNameDictionary[SquareContentType.White] = "White";
            this.playerNameDictionary[SquareContentType.Black] = "Black";
            this.isAutomated[SquareContentType.White] = false;
            this.isAutomated[SquareContentType.Black] = true;
            this.playerPlyDictionary[SquareContentType.White] = defaultPly;
            this.playerPlyDictionary[SquareContentType.Black] = defaultPly;
            BeginNewGame(boardAsString);
        }

        public void BeginNewGame(string boardAsString = null)
        {

            for (int i = 0; i < this.board.Length; ++i)
            {
                this.board[i] = SquareContentType.EmptySquare;
            }

            this.boardPopulation = 0;

            if (boardAsString != null)
            {

                if (boardAsString.Length != boardArea)
                {
                    throw new ArgumentException(
                        string.Format("boardAsString.Length is {0} instead of the expected {1}.", boardAsString.Length, boardArea),
                        "boardAsString");
                }

                this.playerPopulationDictionary[SquareContentType.White] = 0;
                this.playerPopulationDictionary[SquareContentType.Black] = 0;

                // Deserialize boardAsString
                int stringIndex = 0;

                for (int row = 0; row < boardDimension; ++row)
                {

                    for (int column = 0; column < boardDimension; ++column)
                    {
                        SquareContentType squareContent = SquareContentType.EmptySquare;

                        switch (boardAsString[stringIndex])
                        {
                            case 'W':
                                squareContent = SquareContentType.White;
                                ++this.playerPopulationDictionary[SquareContentType.White];
                                break;

                            case 'B':
                                squareContent = SquareContentType.Black;
                                ++this.playerPopulationDictionary[SquareContentType.Black];
                                break;

                            default:
                                break;
                        }

                        if (squareContent != SquareContentType.EmptySquare)
                        {
                            PlacePiece(squareContent, row, column, null, false);
                        }

                        ++stringIndex;
                    }
                }
            }
            else
            {
                var halfBoardDimension = boardDimension / 2;

                SetSquareState(SquareContentType.White, halfBoardDimension - 1, halfBoardDimension - 1, true);
                SetSquareState(SquareContentType.Black, halfBoardDimension - 1, halfBoardDimension, true);
                SetSquareState(SquareContentType.Black, halfBoardDimension, halfBoardDimension - 1, true);
                SetSquareState(SquareContentType.White, halfBoardDimension, halfBoardDimension, true);
                this.playerPopulationDictionary[SquareContentType.White] = 2;
                this.playerPopulationDictionary[SquareContentType.Black] = 2;
            }

            this.currentPlayer = SquareContentType.White;
            //this.isGameOver = false;
            this.noAutomatedMovePossible = 0;
        }

        public string GetBoardAsString()
        {
            var sb = new StringBuilder();
            int boardIndex = 0;

            for (int row = 0; row < boardDimension; ++row)
            {

                for (int column = 0; column < boardDimension; ++column)
                {
                    char c;

                    switch (this.board[boardIndex])
                    {
                        case SquareContentType.White:
                            c = 'W';
                            break;

                        case SquareContentType.Black:
                            c = 'B';
                            break;

                        default:
                            c = ' ';
                            break;
                    }

                    sb.Append(c);
                    ++boardIndex;
                }
            }

            return sb.ToString();
        }

        public bool IsGameNotOver()
        {
            return this.playerPopulationDictionary[SquareContentType.White] > 0 &&
                this.playerPopulationDictionary[SquareContentType.Black] > 0 &&
                this.playerPopulationDictionary[SquareContentType.White] + this.playerPopulationDictionary[SquareContentType.Black] < boardArea &&
                noAutomatedMovePossible < 2;
        }

        public int SquareScore(int row, int column)
        {
            const int cornerSquareScore = 8;
            const int edgeSquareScore = 2;
            var nScore = 1;
            var isInEdgeColumn = column == 0 || column == boardDimension - 1;

            if (row == 0 || row == boardDimension - 1) 
            {

                if (isInEdgeColumn) 
                {
                    nScore = cornerSquareScore;
                }
                else
                {
                    nScore = edgeSquareScore;
                }
            }
            else if (isInEdgeColumn) 
            {
                nScore = edgeSquareScore;
            }

            return nScore;
        }

        public void SetSquareState(SquareContentType player, int row, int column, bool displayMove)
        {
            this.board[row * boardDimension + column] = player;

            if (displayMove)
            {
                this.gameWindow.PlacePiece(player, row, column);
            }
        }

        public int PlacePiece(SquareContentType player, int row, int column, List<int> undoBuffer, bool displayMove)
        {
            // If player is White or Black, the square being written to must be empty just before the move is made.
            // If player is Empty, the square being written to must be non-empty just before the move is made, and displayMove must be false.

            if (row < 0 || row >= boardDimension)
            {
                throw new ArgumentOutOfRangeException("row", string.Format("PlacePiece() : row {0} is out of range.", row));
            }

            if (column < 0 || column >= boardDimension)
            {
                throw new ArgumentOutOfRangeException("column", string.Format("PlacePiece() : column {0} is out of range.", column));
            }

            var oldSquareContent = this.board[row * boardDimension + column];

            if (player != SquareContentType.EmptySquare)
            {

                if (oldSquareContent != SquareContentType.EmptySquare)
                {
                    throw new ArgumentException("PlacePiece() : Attempted to write a White or a Black into a non-empty square.", "player");
                }
            }
            else
            {

                if (oldSquareContent == SquareContentType.EmptySquare)
                {
                    throw new ArgumentException("PlacePiece() : Attempted to erase an already-empty square.", "player");
                }

                if (displayMove)
                {
                    throw new ArgumentException("PlacePiece() : Attempted to display an erasing move to the game window.");
                }
            }

            /*
            this.board[row * boardDimension + column] = player;

            if (player == SquareContentType.EmptySquare)
            {
                --this.boardPopulation;
            }
            else
            {
                ++this.boardPopulation;
            }

            //var victory = player != SquareContentType.EmptySquare && IsVictory(player, row, column);

            if (displayMove)
            {
                this.gameWindow.PlacePiece(player, row, column);
                //this.isGameOver = victory;   // This can only be set to true during real moves.
            }

            //return victory; // This can return true for real or speculative moves.
             */

            var nScore = 0;
            var numPiecesFlipped = 0;

            if (undoBuffer != null)
            {
                undoBuffer.Clear();
            }

            for (var i = 0; i < numDirections; ++i) 
            {
                var bOwnPieceFound = false;
                var row2 = row;
                var column2 = column;
                var nSquaresToFlip = 0;

                // Pass 1: Scan and count.

                for (; ; ) 
                {
                    row2 += ady[i];
                    column2 += adx[i];

                    if (row2 < 0 || row2 >= boardDimension ||
				        column2 < 0 || column2 >= boardDimension)
                    {
                        break;
                    }

                    var squareState = this.board[row2 * boardDimension + column2];

                    if (squareState == SquareContentType.EmptySquare)
                    {
                        break;
                    }

                    if (squareState == player) 
                    {
                        bOwnPieceFound = true;
                        break;
                    }

                    nSquaresToFlip++;
                }

                if (!bOwnPieceFound) 
                {
                    continue;
                }

                // Pass 2: Flip.
                row2 = row;
                column2 = column;

                for (var j = 0; j < nSquaresToFlip; ++j) 
                {
                    row2 += ady[i];
                    column2 += adx[i];

                    SetSquareState(player, row2, column2, displayMove);
                    nScore += 2 * SquareScore(row2, column2);
                    ++numPiecesFlipped;

                    if (undoBuffer != null) 
                    {
                        // Add (row2, column2) to the undo queue.
                        undoBuffer.Add(row2 * boardDimension + column2);
                    }

                    //nUndoSize++;
                }
            }

            //if (nUndoSize > 0) 
            if (numPiecesFlipped > 0)
            {
                SetSquareState(player, row, column, displayMove);
                //returnObject.numPiecesFlipped = nUndoSize;
                //returnObject.score = nScore + squareScore(nRow, nCol);
                nScore += SquareScore(row, column);

                var opponent = (player == SquareContentType.White) ? SquareContentType.Black : SquareContentType.White;

                playerPopulationDictionary[player] += numPiecesFlipped + 1;
                playerPopulationDictionary[opponent] -= numPiecesFlipped;
            }
            // Else no opposing pieces were flipped, and the move fails.

            //return nUndoSize + 1;
            //return returnObject;
            return nScore;
        }

        public int FindBestMove(
        	SquareContentType player, int ply,
	        int parentScore, int bestUncleRecursiveScore,	// For alpha-beta pruning.
            bool returnBestMoveCoords, out int bestRow, out int bestColumn)
        {
            var bestScore = -2 * boardArea;
            var bestMoveList = returnBestMoveCoords ? new List<int>() : null;
            var undoBuffer = new List<int>();
            var opponent = (player == SquareContentType.White) ? SquareContentType.Black : SquareContentType.White;

            for (var nSquare = 0; nSquare < boardArea; ++nSquare)
            {

                if (this.board[nSquare] != SquareContentType.EmptySquare)
                {
                    continue;
                }

                //var nRow = parseInt(nSquare / nBoardWidth, 10);
                var row = nSquare / boardDimension;
                //var nCol = nSquare % nBoardWidth;
                var column = nSquare % boardDimension;
                //var placePieceResult = placePiece(nPlayer, nRow, nCol, undoBuffer, false);
                var score = PlacePiece(player, row, column, undoBuffer, false);
                //var nUndoSize = placePieceResult.numPiecesFlipped;

                //alert("(" + nRow + "," + nCol + "): undo size == " + nUndoSize + "; score == " + placePieceResult.score);

                //if (nUndoSize <= 0) 
                if (score <= 0)
                {
                    continue;
                }

                //m_nMovesTried++;

                //var nScore = placePieceResult.score;

                //PiecePopulations[nPlayer] += nUndoSize + 1;
                //PiecePopulations[1 - nPlayer] -= nUndoSize;

                //playerPopulationDictionary[player] += undoBuffer.Count + 1;
                //playerPopulationDictionary[opponent] -= undoBuffer.Count;

                //if (PiecePopulations[1 - nPlayer] <= 0) 
                if (playerPopulationDictionary[opponent] <= 0)
                {
                    // The opposing player has been annihilated.
                    score = boardArea;  // TODO: Should this be multiplied by something, such as 8?
                }
                else if (ply > 1 &&
			        playerPopulationDictionary[player] + playerPopulationDictionary[opponent] < boardArea) 
                {
                    int dummyBestRow;
                    int dummyBestColumn;

                    score -= FindBestMove(opponent, ply - 1, score, bestScore, false, out dummyBestRow, out dummyBestColumn);
                }

                SetSquareState(SquareContentType.EmptySquare, row, column, false);
                playerPopulationDictionary[player] -= undoBuffer.Count + 1;
                playerPopulationDictionary[opponent] += undoBuffer.Count;

                foreach (var i in undoBuffer) 
                {
                    this.board[i] = opponent;
                }

                if (score > bestScore) 
                {
                    bestScore = score;

                    if (bestMoveList != null)
                    {
                        bestMoveList.Clear();
                        bestMoveList.Add(nSquare);
                    }
                    else if (parentScore - bestScore < bestUncleRecursiveScore) 
                    {
                        // Alpha-beta pruning.  Because of the initial parameters for the top-level move, this break is never executed for the top-level move.
                        break; // ie. return.
                    }
                }
                else if (score == bestScore && bestMoveList != null) 
                {
                    bestMoveList.Add(nSquare);
                }
            }

            /*
    var returnObject = new BestMoveData();

    if (bestMoveIndices.length > 0) {
        var i = parseInt(Math.random() * bestMoveIndices.length, 10);
        var nBestIndex = bestMoveIndices[i];

        returnObject.bestRow = parseInt(nBestIndex / nBoardWidth, 10);
        returnObject.bestCol = nBestIndex % nBoardWidth;
    }

    returnObject.bestScore = nBestScore;
    return returnObject;
             */

            if (bestMoveList == null || bestMoveList.Count == 0)
            {
                bestRow = -1;
                bestColumn = -1;
                //numBestMoves = -1;
            }
            /*
            else if (bestMoveList.Count == 0)
            {
                throw new Exception("FindBestMove() : The bestMoveList is empty at the end of the method.");
            }
             */
            else
            {
                var randomNumberGenerator = new Random();
                var index = randomNumberGenerator.Next(bestMoveList.Count);
                var packedCoordinates = bestMoveList[index];

                bestRow = packedCoordinates / boardDimension;
                bestColumn = packedCoordinates % boardDimension;
                //numBestMoves = bestMoveList.Count;
            }

            return bestScore;
        }

        public BestMoveData FindBestMoveWrapper(SquareContentType player, int ply)
        {
            int bestRow;
            int bestColumn;
            int bestScore = FindBestMove(player, ply, 0, 0, true, out bestRow, out bestColumn);

            return new BestMoveData(bestScore, bestRow, bestColumn);
        }
    }
}
