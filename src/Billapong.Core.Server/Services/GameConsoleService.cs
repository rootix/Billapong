﻿namespace Billapong.Core.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using Contract.Data.Map;
    using Contract.Service;
    using Converter.Map;
    using GamePlay;
    using Map;

    /// <summary>
    /// The game console service implementation.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class GameConsoleService : IGameConsoleService
    {
        /// <summary>
        /// Gets the maps.
        /// </summary>
        /// <returns>
        /// Playable maps in the database
        /// </returns>
        public IEnumerable<Map> GetMaps()
        {
            return MapController.Current.GetMaps(true).Select(map => map.ToContract()).ToList();
        }

        /// <summary>
        /// Opens a new game and go into the lobby.
        /// </summary>
        /// <param name="mapId">The map identifier.</param>
        /// <param name="visibleWindows">The visible windows.</param>
        /// <param name="username">The username.</param>
        /// <returns>
        /// Id of the game, used as correlation id
        /// </returns>
        public Guid OpenGame(long mapId, IEnumerable<long> visibleWindows, string username)
        {
            return GameController.Current.OpenGame(mapId, visibleWindows, username, this.GetCallback());
        }

        /// <summary>
        /// Joins a game.
        /// </summary>
        /// <param name="gameId">The game identifier / correlation id of the game.</param>
        /// <param name="username">The username.</param>
        public void JoinGame(Guid gameId, string username)
        {
            GameController.Current.JoinGame(gameId, username, this.GetCallback());
        }

        /// <summary>
        /// Gets the callback.
        /// </summary>
        /// <returns>Callback channel for the current context</returns>
        private IGameConsoleCallback GetCallback()
        {
            return OperationContext.Current.GetCallbackChannel<IGameConsoleCallback>();
        }
    }
}
