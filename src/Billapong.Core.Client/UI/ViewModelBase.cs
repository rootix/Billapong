﻿namespace Billapong.Core.Client.UI
{
    using System.Windows;
    using Billapong.Core.Client.Exceptions;
    using Billapong.Core.Client.Properties;
    using Billapong.Core.Client.Tracing;

    /// <summary>
    /// Provides basic functionality to view models
    /// </summary>
    public abstract class ViewModelBase : NotificationObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
        /// </summary>
        protected ViewModelBase()
        {
            this.WindowManager = new WindowManager();
        }

        /// <summary>
        /// Gets the window manager.
        /// </summary>
        /// <value>
        /// The window manager.
        /// </value>
        protected WindowManager WindowManager { get; private set; }

        /// <summary>
        /// Gets called when the connected view closes
        /// </summary>
        public virtual void CloseCallback()
        {
            // to nothing by default
        }

        /// <summary>
        /// Shutdowns the application on an error and show a messagebox before.
        /// </summary>
        /// <param name="message">The message.</param>
        protected virtual void ShutdownApplication(string message = "")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = Resources.UnexpectedError;
            }

            MessageBox.Show(message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }

        protected virtual async void HandleServerException(ServerUnavailableException ex, bool withoutShutdown = false)
        {
            await Tracer.Error("Server not available", ex);
            if (!withoutShutdown)
            {
                this.ShutdownApplication(Resources.ServerUnavailable);   
            }
        }
    }
}