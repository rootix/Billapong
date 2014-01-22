﻿namespace Billapong.GameConsole.Service
{
    using System;
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.Windows;
    using System.Windows.Threading;
    using Contract.Data.Map;
    using Contract.Service;
    using Converter.Map;
    using Core.Client.Helper;
    using Models.Events;

    /// <summary>
    /// The callback client for the game console service
    /// </summary>
    [CallbackBehavior(UseSynchronizationContext = true)]
    public class GameConsoleCallback : IGameConsoleCallback
    {
        /// <summary>
        /// Occurs when the game starts.
        /// </summary>
        public event EventHandler<GameStartedEventArgs> GameStarted = delegate { };

        /// <summary>
        /// Starts the game with a specific id.
        /// </summary>
        /// <param name="gameId">The game identifier.</param>
        /// <param name="map">The map.</param>
        /// <param name="opponentName">Name of the opponent.</param>
        /// <param name="visibleWindows">The visible windows.</param>
        /// <param name="startGame">if set to <c>true</c> the player who receives this callback should start the game.</param>
        public void StartGame(Guid gameId, Map map, string opponentName, IEnumerable<long> visibleWindows, bool startGame)
        {
            var args = new GameStartedEventArgs(gameId, map.ToEntity(visibleWindows), opponentName, startGame);
            ThreadContext.InvokeOnUiThread(() => this.OnGameStarted(args));
        }

        /// <summary>
        /// Raises the <see cref="E:GameStarted" /> event.
        /// </summary>
        /// <param name="args">The <see cref="GameStartedEventArgs"/> instance containing the event data.</param>
        private void OnGameStarted(GameStartedEventArgs args)
        {
            this.GameStarted(this, args);
        }

        public void GameError(Guid gameId)
        {
            MessageBox.Show("Upps something went wrong, need to cancel the game...");
        }

        public void CancelGame(Guid gameId)
        {
            MessageBox.Show("Someone/-thing has canceled the game...");
        }
    }
}