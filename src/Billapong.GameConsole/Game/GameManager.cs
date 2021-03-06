﻿namespace Billapong.GameConsole.Game
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using Animation;
    using Billapong.GameConsole.Properties;
    using Billapong.GameConsole.Service;
    using Configuration;
    using Core.Client.Tracing;
    using Models;
    using Models.Events;
    using ViewModels;
    using Views;
    using Game = Models.Game;
    using Window = System.Windows.Window;

    /// <summary>
    /// Handles the running game of Billapong
    /// </summary>
    public class GameManager
    {
        /// <summary>
        /// The instance
        /// </summary>
        private static readonly GameManager SingletonInstance = new GameManager();

        /// <summary>
        /// The list with all game window view models
        /// </summary>
        private List<GameWindowViewModel> gameWindowViewModels;

        /// <summary>
        /// The game windows
        /// </summary>
        private List<Window> gameWindows;

        /// <summary>
        /// The game controller
        /// </summary>
        private IGameController gameController;

        /// <summary>
        /// The ball animation task queue
        /// </summary>
        private Queue<BallAnimationTask> ballAnimationTaskQueue;

        /// <summary>
        /// The game state view model
        /// </summary>
        private GameStateViewModel gameStateViewModel;

        /// <summary>
        /// Prevents a default instance of the <see cref="GameManager"/> class from being created.
        /// </summary>
        private GameManager()
        {
            GameConsoleContext.Current.GameConsoleCallback.GameErrorOccurred += this.ErrorOccurred;
        }

        /// <summary>
        /// Defines the possible intersections between a ball and the game field
        /// </summary>
        private enum Intersection
        {
            /// <summary>
            /// No intersection
            /// </summary>
            None,

            /// <summary>
            /// The ball hits a hole
            /// </summary>
            Hole,

            /// <summary>
            /// The ball hits the top wall
            /// </summary>
            TopWall,

            /// <summary>
            /// The ball hits the left wall
            /// </summary>
            LeftWall,

            /// <summary>
            /// The ball hits the right wall
            /// </summary>
            RightWall,

            /// <summary>
            /// The ball hits the bottom wall
            /// </summary>
            BottomWall
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static GameManager Current
        {
            get
            {
                return SingletonInstance;
            }
        }

        /// <summary>
        /// Gets the current game.
        /// </summary>
        /// <value>
        /// The current game.
        /// </value>
        public Game CurrentGame { get; private set; }

        /// <summary>
        /// Starts the game.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="stateViewModel">The state view model.</param>
        public void StartGame(Game game, GameStateViewModel stateViewModel)
        {
            Application.Current.MainWindow.Closing += this.OnWindowClosing;

            this.PrepareNewGame();
            this.CurrentGame = game;
            this.CurrentGame.CurrentGameState = Game.GameState.Running;
            this.gameStateViewModel = stateViewModel;
            this.gameStateViewModel.StartGame();

            switch (this.CurrentGame.GameType)
            {
                case GameConfiguration.GameType.SinglePlayerTraining:
                    this.gameController = new SinglePlayerTrainingGameController();
                    break;
                case GameConfiguration.GameType.MultiPlayerGame:
                    this.gameController = new MultiplayerGameController();
                    break;
                default:
                    this.gameController = new SinglePlayerGameController();
                    break;
            }

            this.gameController.BallPlacedOnGameField += this.PlaceBallOnGameField;
            this.gameController.RoundStarted += this.StartRound;
            this.gameController.RoundEnded += this.RoundEnded;
            this.gameController.GameCanceled += this.CancelGame;
            this.gameController.ErrorOccurred += this.ErrorOccurred;

            this.gameController.StartGame();
            this.OpenGameField();

            if (this.CurrentGame.LocalPlayer.HasFirstTurn)
            {
                this.PlaceBallOnGameField();
            }
        }

        /// <summary>
        /// Cancels the game if it is running.
        /// </summary>
        public void CancelGame()
        {
            if (this.CurrentGame.CurrentGameState == Game.GameState.Running)
            {
                this.gameController.CancelGame();
            }
        }

        /// <summary>
        /// Logs the message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="logMethod">The log method.</param>
        public void LogMessage(string message, Func<string, Task> logMethod)
        {
            if (logMethod == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var logMessage = string.Format(
                "Game Id: {0}{5}Player: {1}{5}Map: {2}{5}Round: {3}{5}Message: {4}",
                this.CurrentGame.GameId,
                this.CurrentGame.CurrentPlayer.Name,
                this.CurrentGame.Map.Name,
                this.CurrentGame.CurrentRound,
                message,
                Environment.NewLine);
            logMethod(logMessage);
        }

        /// <summary>
        /// Calculates the round score.
        /// </summary>
        /// <param name="ballAnimationQueue">The ball animation queue.</param>
        /// <returns>The calculated score of the round</returns>
        private static int CalculateRoundScore(Queue<BallAnimationTask> ballAnimationQueue)
        {
            if (ballAnimationQueue == null || ballAnimationQueue.Count == 0)
            {
                return 0;
            }

            var wallHits = 0;
            double ballDistance = 0;
            var holeHit = false;

            foreach (var task in ballAnimationQueue)
            {
                foreach (var step in task.Steps)
                {
                    if (step.By != null && step.To != null)
                    {
                        ballDistance += step.By.Value.DistanceTo(step.To.Value);
                    }

                    // Except for the last step within this task, every step is a wall hit
                    if (!step.Equals(task.Steps.Last()))
                    {
                        wallHits++;
                    }
                }

                if (task.IntersectsWithHole)
                {
                    holeHit = true;
                }
            }

            return CalculateRoundScore(wallHits, ballDistance, GameConfiguration.GameWindowWidth, holeHit);
        }

        /// <summary>
        /// Calculates the round score. If the ball does not hit a hole, the round score is zero.
        /// </summary>
        /// <param name="wallHits">The wall hits.</param>
        /// <param name="ballDistance">The ball distance.</param>
        /// <param name="gameWindowLength">Length of the game window.</param>
        /// <param name="hitsHole">if set to <c>true</c> the ball hits a hole.</param>
        /// <returns>
        /// The calculated score
        /// </returns>
        private static int CalculateRoundScore(int wallHits, double ballDistance, int gameWindowLength, bool hitsHole)
        {
            if (!hitsHole)
            {
                return 0;
            }

            // Normalize the score of the distance to ensure the same score over different window sizes
            var distanceScore = ballDistance / gameWindowLength;

            // We need bigger high scores than the normalized value :)
            distanceScore *= 10;

            return Convert.ToInt32((wallHits + 1) * distanceScore);
        }

        /// <summary>
        /// Called when the window is closing
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="CancelEventArgs"/> instance containing the event data.</param>
        private async void OnWindowClosing(object sender, CancelEventArgs args)
        {
            if (this.CurrentGame.CurrentGameState == Game.GameState.Running)
            {
                await GameConsoleContext.Current.GameConsoleServiceClient.CancelGameAsync(this.CurrentGame.GameId, this.CurrentGame.LocalPlayer.IsFirstPlayer, false);
            }
        }

        /// <summary>
        /// Cancels the game based on an event callback.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void CancelGame(object sender, EventArgs args)
        {
            if (this.CurrentGame != null && this.gameStateViewModel != null)
            {
                this.CurrentGame.CurrentGameState = Game.GameState.Canceled;
                this.gameStateViewModel.CancelGame();
                this.CloseGameField();
            }
        }

        /// <summary>
        /// Prepares the new game.
        /// </summary>
        private void PrepareNewGame()
        {
            this.gameWindowViewModels = new List<GameWindowViewModel>();
            this.gameWindows = new List<Window>();
            this.gameController = null;
            this.ballAnimationTaskQueue = null;
            this.gameStateViewModel = null;
        }

        /// <summary>
        /// Opens the game field.
        /// </summary>
        private void OpenGameField()
        {
            const int WindowBorderOffset = 2;

            double maxWindowRow = this.CurrentGame.Map.Windows.Max(window => window.Y);
            double maxWindowCol = this.CurrentGame.Map.Windows.Max(window => window.X);

            var gameFieldHeight = (maxWindowRow + 1) * GameConfiguration.GameWindowHeight;
            var gameFieldWidth = (maxWindowCol + 1) * GameConfiguration.GameWindowWidth;

            var initialVerticalOffset = (SystemParameters.WorkArea.Height / 2) - (gameFieldHeight / 2)
                                        - ((maxWindowRow * WindowBorderOffset) / 2);
            var initialHorizontalOffset = (SystemParameters.WorkArea.Width / 2) - (gameFieldWidth / 2)
                                          - ((maxWindowCol * WindowBorderOffset) / 2);

            var verticalOffset = initialVerticalOffset;

            for (var currentRow = 0; currentRow <= maxWindowRow; currentRow++)
            {
                var horizontalOffset = initialHorizontalOffset;
                var windowsInRow =
                    this.CurrentGame.Map.Windows.Where(window => window.Y == currentRow)
                        .OrderBy(window => window.X)
                        .ToList();

                for (var currentCol = 0; currentCol <= maxWindowCol; currentCol++)
                {
                    var currentWindow = windowsInRow.FirstOrDefault(window => window.X == currentCol);
                    if (currentWindow != null)
                    {
                        var gameWindow = new GameWindow();
                        gameWindow.Top = verticalOffset;
                        gameWindow.Left = horizontalOffset;
                        gameWindow.MinHeight = GameConfiguration.GameWindowHeight + WindowBorderOffset;
                        gameWindow.MinWidth = GameConfiguration.GameWindowWidth + WindowBorderOffset;
                        gameWindow.MaxHeight = gameWindow.MinHeight;
                        gameWindow.MaxWidth = gameWindow.MinWidth;
                        gameWindow.ResizeMode = ResizeMode.NoResize;
                        gameWindow.WindowStyle = WindowStyle.None;
                        gameWindow.BorderBrush = new SolidColorBrush(Colors.Black);

                        // Calculate the game field borders
                        var borderThickness = new Thickness();
                        if (
                            this.CurrentGame.Map.Windows.FirstOrDefault(
                                w => w.X == currentWindow.X - 1 && w.Y == currentWindow.Y) == null)
                        {
                            borderThickness.Left = 1;
                        }

                        if (
                            this.CurrentGame.Map.Windows.FirstOrDefault(
                                w => w.X == currentWindow.X + 1 && w.Y == currentWindow.Y) == null)
                        {
                            borderThickness.Right = 1;
                        }

                        if (
                            this.CurrentGame.Map.Windows.FirstOrDefault(
                                w => w.X == currentWindow.X && w.Y == currentWindow.Y - 1) == null)
                        {
                            borderThickness.Top = 1;
                        }

                        if (
                            this.CurrentGame.Map.Windows.FirstOrDefault(
                                w => w.X == currentWindow.X && w.Y == currentWindow.Y + 1) == null)
                        {
                            borderThickness.Bottom = 1;
                        }

                        gameWindow.BorderThickness = borderThickness;

                        var gameWindowViewModel = new GameWindowViewModel(currentWindow);
                        gameWindow.DataContext = gameWindowViewModel;
                        gameWindowViewModel.GameFieldClicked += this.GameFieldClicked;
                        gameWindowViewModel.AnimationFinished += this.AnimationFinished;

                        this.gameWindowViewModels.Add(gameWindowViewModel);
                        this.gameWindows.Add(gameWindow);

                        if (currentWindow.IsOwnWindow)
                        {
                            gameWindow.Show();
                        }
                    }

                    horizontalOffset += GameConfiguration.GameWindowWidth + WindowBorderOffset;
                }

                verticalOffset += GameConfiguration.GameWindowHeight + WindowBorderOffset;
            }
        }

        /// <summary>
        /// Closes the game field.
        /// </summary>
        private void CloseGameField()
        {
            foreach (var window in this.gameWindows)
            {
                window.Close();
            }
        }

        /// <summary>
        /// Gets called when a unexpected error occurs
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="GameErrorEventArgs"/> instance containing the event data.</param>
        private void ErrorOccurred(object sender, EventArgs args)
        {
            MessageBox.Show(
                Resources.UnexpectedGameError,
                Resources.Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);

                this.CancelGame(sender, args);
        }

        /// <summary>
        /// Places the ball on game field.
        /// </summary>
        private void PlaceBallOnGameField()
        {
            var possibleStartWindows = this.gameWindowViewModels.Where(x => x.Window.IsOwnWindow).Select(x => x.Window).ToArray();
            var randomWindow = GameHelpers.GetRandomWindow(possibleStartWindows);
            if (randomWindow != null)
            {
                Point? ballPosition = GameHelpers.GetRandomBallPosition(randomWindow);
                if (ballPosition != null)
                {
                    this.gameController.PlaceBallOnGameField(randomWindow.Id, ballPosition.Value);
                }
            }
        }

        /// <summary>
        /// Places the ball on game field based on an event callback.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="BallPlacedOnGameFieldEventArgs"/> instance containing the event data.</param>
        private void PlaceBallOnGameField(object sender, BallPlacedOnGameFieldEventArgs args)
        {
            var viewModel = this.gameWindowViewModels.FirstOrDefault(x => x.Window.Id == args.WindowId);
            if (viewModel != null)
            {
                var position = GameHelpers.GetBallPositionFromGridCoordinates(args.Position);
                this.CurrentGame.CurrentBallPosition = position;
                this.CurrentGame.CurrentWindow = viewModel.Window;
                this.CurrentGame.CurrentPlayer.CurrentPlayerState = Player.PlayerState.BallPlaced;
                viewModel.PlaceBall(position, this.CurrentGame.CurrentPlayer.PlayerColor);
            }
        }

        /// <summary>
        /// Starts the round. Gets called from the RoundStarted event of the game controller
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="RoundStartedEventArgs"/> instance containing the event data.</param>
        private void StartRound(object sender, RoundStartedEventArgs args)
        {
            this.ballAnimationTaskQueue = this.CalculateBallAnimationTasks(
                this.CurrentGame.CurrentWindow,
                this.CurrentGame.CurrentBallPosition,
                args.Direction);

            this.CurrentGame.CurrentRoundScore = CalculateRoundScore(this.ballAnimationTaskQueue);

            if (this.ballAnimationTaskQueue.Count > 0)
            {
                var firstTask = this.ballAnimationTaskQueue.Dequeue();
                var viewModel = this.gameWindowViewModels.FirstOrDefault(x => x.Window.Id == firstTask.Window.Id);
                if (viewModel != null)
                {
                    viewModel.BallAnimationTask = firstTask;
                    this.CurrentGame.CurrentPlayer.CurrentPlayerState = Player.PlayerState.BallMoving;
                }
            }
        }

        /// <summary>
        /// Gets called from the RoundEnded event of the game controller when the round has ended
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="RoundEndedEventArgs"/> instance containing the event data.</param>
        private void RoundEnded(object sender, RoundEndedEventArgs args)
        {
            if (args.GameEnded)
            {
                this.gameController.EndGame();
                this.EndLocalGame();
            }
            else
            {
                // Start the next round if the local player is next
                if (this.CurrentGame.CurrentPlayer.IsLocalPlayer)
                {
                    this.PlaceBallOnGameField();
                }
            }
        }

        /// <summary>
        /// Ends the game.
        /// </summary>
        private void EndLocalGame()
        {
            Application.Current.MainWindow.Closing -= this.OnWindowClosing;
            GameConsoleContext.Current.StopKeepGameAlive();
            this.CurrentGame.CurrentGameState = Game.GameState.Ended;
            this.gameStateViewModel.EndGame();
            this.CloseGameField();
        }

        /// <summary>
        /// Gets called when the game field is clicked. Occurs only if a ball is in the senders window
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="GameFieldClickedEventArgs"/> instance containing the event data.</param>
        private void GameFieldClicked(object sender, GameFieldClickedEventArgs args)
        {
            var ballPosition = this.CurrentGame.CurrentBallPosition;
            var direction = new Vector(args.MousePosition.X, args.MousePosition.Y) -
                            new Vector(ballPosition.X, ballPosition.Y);
            
            // The ball has to move into the opposite direction
            direction.Negate();

            this.gameController.StartRound(direction);
        }

        /// <summary>
        /// Gets called when an animation of a window finishes. Ends the round if the queue is empty
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void AnimationFinished(object sender, EventArgs args)
        {
            if (this.ballAnimationTaskQueue == null || this.ballAnimationTaskQueue.Count == 0)
            {
                this.gameController.EndRound(this.CurrentGame.CurrentPlayer.IsFirstPlayer, this.CurrentGame.CurrentRoundScore);
                return;
            }

            var nextTask = this.ballAnimationTaskQueue.Dequeue();
            var viewModel = this.gameWindowViewModels.FirstOrDefault(w => w.Window.Id == nextTask.Window.Id);
            if (viewModel == null) return;

            var nextBallStartPosition = nextTask.Steps.First().By;
            if (nextBallStartPosition != null)
            {
                viewModel.PlaceBall(nextBallStartPosition.Value, this.CurrentGame.CurrentPlayer.PlayerColor);
                viewModel.BallAnimationTask = nextTask;
                this.LogMessage(string.Format("Ball changed to window with id {0}", nextTask.Window.Id), Tracer.Debug);
            }
        }

        /// <summary>
        /// Calculates the ball animation tasks.
        /// </summary>
        /// <param name="initialWindow">The initial window.</param>
        /// <param name="startPosition">The start position.</param>
        /// <param name="initialDirection">The initial direction.</param>
        /// <returns>The calculated animation queue</returns>
        private Queue<BallAnimationTask> CalculateBallAnimationTasks(Models.Window initialWindow, Point startPosition, Vector initialDirection)
        {
            var animationQueue = new Queue<BallAnimationTask>();
            var isLastAnimation = false;
            var currentWindow = initialWindow;
            var currentBallPosition = startPosition;
            var currentDirection = initialDirection;
            var stepCounter = 0;
            var currentTask = new BallAnimationTask { Window = currentWindow };
            var previousIntersection = Intersection.None;
            var windowPreviouslyChanged = false;
            const double BallRadius = GameConfiguration.BallDiameter / 2;
            
            while (!isLastAnimation)
            {
                // Ensure an end of the animation
                stepCounter++;
                if (stepCounter == 30)
                {
                    isLastAnimation = true;
                    currentTask.IsLastAnimation = true;
                    currentTask.IntersectsWithHole = false;
                }

                var borderPositionCorrection = new Point();

                // Calculate the ignorable intersection
                var ignoredIntersection = Intersection.None;
                if (previousIntersection != Intersection.None)
                {
                    if (windowPreviouslyChanged)
                    {
                        switch (previousIntersection)
                        {
                            case Intersection.TopWall:
                                ignoredIntersection = Intersection.BottomWall;
                                break;
                            case Intersection.LeftWall:
                                ignoredIntersection = Intersection.RightWall;
                                break;
                            case Intersection.RightWall:
                                ignoredIntersection = Intersection.LeftWall;
                                break;
                            case Intersection.BottomWall:
                                ignoredIntersection = Intersection.TopWall;
                                break;
                        }
                    }
                    else
                    {
                        ignoredIntersection = previousIntersection;
                    }
                }

                var currentIntersection = Intersection.None;
                Point? intersectionPosition = null;
                Models.Window neighbourWindow = null;

                // Generate a point outside of the window
                var intersectionTestPoint = currentBallPosition + (currentDirection * 1000);

                // Check for hole intersection
                if (currentWindow.Holes != null) 
                { 
                    foreach (var hole in currentWindow.Holes)
                    {
                        Point? firstIntersection;
                        Point? secondIntersection;

                        var intersection = CalculationHelpers.CalculateLineSphereIntersection(
                            hole.CenterPosition,
                            hole.Radius,
                            currentBallPosition,
                            intersectionTestPoint,
                            out firstIntersection,
                            out secondIntersection);

                        if (intersection > 0 && firstIntersection != null)
                        {
                            currentIntersection = Intersection.Hole;

                            // This hole intersection is only relevant, if the distance between this intersection and the ball is the shortest
                            if (intersectionPosition == null
                                || currentBallPosition.DistanceTo((Point)firstIntersection)
                                < currentBallPosition.DistanceTo((Point)intersectionPosition))
                            {
                                intersectionPosition = firstIntersection;    
                            }
                        }    
                    }
                }

                // Check for top wall hit
                if (intersectionPosition == null && ignoredIntersection != Intersection.TopWall)
                {
                    intersectionPosition = CalculationHelpers.GetLineIntersection(
                        currentBallPosition,
                        intersectionTestPoint,
                        new Point(0, 0),
                        new Point(GameConfiguration.GameWindowWidth, 0));

                    if (intersectionPosition != null)
                    {
                        previousIntersection = currentIntersection = Intersection.TopWall;

                        var upperWindow = this.CurrentGame.Map.Windows.FirstOrDefault(w => w.X == currentWindow.X && w.Y == currentWindow.Y - 1);
                        if (upperWindow != null)
                        {
                            neighbourWindow = upperWindow;
                        }
                        else
                        {
                            borderPositionCorrection.Y += BallRadius;
                        }
                    }
                }

                if (intersectionPosition == null && ignoredIntersection != Intersection.LeftWall)
                {
                    // Check for left wall hit
                    intersectionPosition = CalculationHelpers.GetLineIntersection(
                        currentBallPosition,
                        intersectionTestPoint,
                        new Point(0, 0),
                        new Point(0, GameConfiguration.GameWindowWidth));

                    if (intersectionPosition != null)
                    {
                        previousIntersection = currentIntersection = Intersection.LeftWall;

                        var leftWindow = this.CurrentGame.Map.Windows.FirstOrDefault(w => w.X == currentWindow.X - 1 && w.Y == currentWindow.Y);
                        if (leftWindow != null)
                        {
                            neighbourWindow = leftWindow;
                        }
                        else
                        {
                            borderPositionCorrection.X += BallRadius;
                        }
                    }
                }

                if (intersectionPosition == null && ignoredIntersection != Intersection.RightWall)
                {
                    // Check for right wall hit
                    intersectionPosition = CalculationHelpers.GetLineIntersection(
                        currentBallPosition,
                        intersectionTestPoint,
                        new Point(GameConfiguration.GameWindowWidth, 0),
                        new Point(GameConfiguration.GameWindowWidth, GameConfiguration.GameWindowWidth));

                    if (intersectionPosition != null)
                    {
                        previousIntersection = currentIntersection = Intersection.RightWall;

                        var rightWindow = this.CurrentGame.Map.Windows.FirstOrDefault(w => w.X == currentWindow.X + 1 && w.Y == currentWindow.Y);
                        if (rightWindow != null)
                        {
                            neighbourWindow = rightWindow;
                        }
                        else
                        {
                            borderPositionCorrection.X -= BallRadius;
                        }
                    }
                }

                if (intersectionPosition == null && ignoredIntersection != Intersection.BottomWall)
                {
                    // Check for bottom wall hit
                    intersectionPosition = CalculationHelpers.GetLineIntersection(
                        currentBallPosition,
                        intersectionTestPoint,
                        new Point(0, GameConfiguration.GameWindowWidth),
                        new Point(GameConfiguration.GameWindowWidth, GameConfiguration.GameWindowWidth));

                    if (intersectionPosition != null)
                    {
                        previousIntersection = currentIntersection = Intersection.BottomWall;

                        var bottomWindow = this.CurrentGame.Map.Windows.FirstOrDefault(w => w.X == currentWindow.X && w.Y == currentWindow.Y + 1);
                        if (bottomWindow != null)
                        {
                            neighbourWindow = bottomWindow;
                        }
                        else
                        {
                            borderPositionCorrection.Y -= BallRadius;
                        }
                    }
                }

                if (currentIntersection != Intersection.None && intersectionPosition != null)
                {
                    var newPosition = (Point)intersectionPosition;

                    // Apply correction
                    newPosition.X += borderPositionCorrection.X;
                    newPosition.Y += borderPositionCorrection.Y;

                    currentTask.Steps.Add(AnimationHelpers.GetPointAnimation(currentBallPosition, newPosition));

                    // If the ball intersects a hole, end the calculation after this step
                    if (currentIntersection == Intersection.Hole)
                    {
                        currentTask.IsLastAnimation = true;
                        currentTask.IntersectsWithHole = true;
                        animationQueue.Enqueue(currentTask);
                        return animationQueue;
                    }

                    currentBallPosition = newPosition;

                    // Ball will leave the window 
                    if (neighbourWindow != null)
                    {
                        animationQueue.Enqueue(currentTask);
                        currentWindow = neighbourWindow;
                        currentTask = new BallAnimationTask { Window = currentWindow };
                        windowPreviouslyChanged = true;

                        // Set new initial position for the next window
                        switch (currentIntersection)
                        {
                            case Intersection.TopWall:
                                currentBallPosition.Y = GameConfiguration.GameWindowWidth;
                                break;
                            case Intersection.LeftWall:
                                currentBallPosition.X = GameConfiguration.GameWindowWidth;
                                break;
                            case Intersection.RightWall:
                                currentBallPosition.X = 0;
                                break;
                            case Intersection.BottomWall:
                                currentBallPosition.Y = 0;
                                break;
                        }
                    }
                    else
                    {
                        // Change ball direction within the current window
                        if (currentIntersection == Intersection.TopWall || currentIntersection == Intersection.BottomWall)
                        {
                            currentDirection.Y *= -1;
                        }
                        else
                        {
                            currentDirection.X *= -1;
                        }

                        windowPreviouslyChanged = false;
                    }
                }
            }

            // Add the last animation task to the queue because we had no wall hit (timeout)
            if (currentTask.Steps.Count > 0)
            {
                animationQueue.Enqueue(currentTask);
            }

            return animationQueue;
        }
    }
}
