﻿namespace Billapong.Core.Client
{
    using Billapong.Contract.Exceptions;
    using Billapong.Core.Client.Exceptions;
    using System;
    using System.Diagnostics;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading.Tasks;

    /// <summary>
    /// Basic functionality for WCF clients
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    public class RichClientBase<TService>
    {
        /// <summary>
        /// Gets the proxy.
        /// </summary>
        /// <value>
        /// The proxy.
        /// </value>
        protected TService Proxy { get; private set; }

        /// <summary>
        /// Executes the specified delegated action.
        /// </summary>
        /// <param name="delegatedAction">The delegated action.</param>
        protected void Execute(Action delegatedAction)
        {
            try
            {
                this.ValidateProxy();
                delegatedAction();
            }
            catch (Exception ex)
            {
                this.HandleException(ex);
            }
        }

        /// <summary>
        /// Executes the specified delegated function.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="delegatedFunction">The delegated function.</param>
        /// <returns>The result.</returns>
        protected TResult Execute<TResult>(Func<TResult> delegatedFunction)
        {
            try
            {
                this.ValidateProxy();
                return delegatedFunction();
            }
            catch (Exception ex)
            {
                this.HandleException(ex);
                return default(TResult);
            }
        }

        /// <summary>
        /// Executes the action asynchronous.
        /// </summary>
        /// <param name="delegatedAction">The delegated action.</param>
        /// <returns>The task.</returns>
        protected async Task ExecuteAsync(Action delegatedAction)
        {
            await Task.Run(() => this.Execute(delegatedAction));
        }

        /// <summary>
        /// Executes the given function asynchronous
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="delgatedFunction">The delgated function.</param>
        /// <returns>The task result.</returns>
        protected async Task<TResult> ExecuteAsync<TResult>(Func<TResult> delgatedFunction)
        {
            return await Task.Run(() => this.Execute(delgatedFunction));
        }

        /// <summary>
        /// Creates the channel.
        /// </summary>
        /// <returns>The proxy</returns>
        protected virtual TService CreateChannel()
        {
            return new ChannelFactory<TService>("*").CreateChannel();
        }

        /// <summary>
        /// Validates the proxy.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Validate state of proxy failed.</exception>
        private void ValidateProxy()
        {
            if (this.Proxy == null)
            {
                this.CreateProxy();
            }
            else if (((IChannel)this.Proxy).State == CommunicationState.Opened)
            {
                return;
            }
            else if (((IChannel)this.Proxy).State == CommunicationState.Faulted)
            {
                ((IChannel)this.Proxy).Abort();
                this.CreateProxy();
            }
            else
            {
                throw new InvalidOperationException("Validate state of proxy failed.");
            }
        }

        /// <summary>
        /// Creates the proxy.
        /// </summary>
        private void CreateProxy()
        {
            this.Proxy = this.CreateChannel();
            ((IChannel)this.Proxy).Open();
        }

        /// <summary>
        /// Handles the exception.
        /// </summary>
        /// <param name="ex">The exception which was thrown.</param>
        private void HandleException(Exception ex)
        {
            Trace.TraceError(string.Format("Error while executing WCF request: {0}{1}", ex.Message, ex.StackTrace));
            
            // faultexception -> rethrow
            if (ex is FaultException)
            {
                throw ex;
            }
            
            // communication exception where the server has some troubles
            if (ex is EndpointNotFoundException
                || ex is CommunicationException
                || ex is InvalidOperationException)
            {
                throw new ServerUnavailableException(ex);
            }

            // rethrow all other stuff
            throw ex;
        }
    }
}
