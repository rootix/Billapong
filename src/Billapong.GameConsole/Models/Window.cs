﻿namespace Billapong.GameConsole.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a window within a map
    /// </summary>
    public class Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Window"/> class.
        /// </summary>
        public Window()
        {
            this.Holes = new List<Hole>();
        }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the x coordinate.
        /// </summary>
        /// <value>
        /// The x coordinate.
        /// </value>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the y coordinate.
        /// </summary>
        /// <value>
        /// The y coordinate.
        /// </value>
        public int Y { get; set; }
        
        /// <summary>
        /// Gets the display name.
        /// </summary>
        /// <value>
        /// The display name.
        /// </value>
        public string DisplayName
        {
            get
            {
                return string.Format("X: {0} / Y: {1}", this.X, this.Y);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the window is used on this client.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the window is used on this client; otherwise, <c>false</c>.
        /// </value>
        public bool IsOwnWindow { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the window is visible.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the window is visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Gets the holes.
        /// </summary>
        /// <value>
        /// The holes.
        /// </value>
        public IList<Hole> Holes { get; private set; }
    }
}
