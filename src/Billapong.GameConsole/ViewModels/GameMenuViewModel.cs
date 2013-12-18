﻿namespace Billapong.GameConsole.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.Windows.Input;
    using Contract.Data.Tracing;
    using Contract.Service;
    using Core.Client.Tracing;

    public class GameMenuViewModel
    {
        public ICommand OpenGameWindowCommand
        {
            get
            {
                return new DelegateCommand(OpenGameWindow);
            }
        }

        public ICommand LogCommand
        {
            get
            {
                return new DelegateCommand(Log);
            }
        }

        public void OpenGameWindow(object parameter)
        {
            var viewModel = new GameWindowViewModel();

            int yPos = 10;
            int xPos = 200;
            for (var i = 0; i < 12; i++)
            {
                if (i > 0 && i%4 == 0)
                {
                    yPos += 325;
                    xPos = 200;
                }

                var gameWindow = new GameWindow();
                gameWindow.Left = xPos;
                gameWindow.Top = yPos;
                gameWindow.DataContext = viewModel;
                gameWindow.Show();

                    xPos += 305;
            }


            //var gameWindow = new GameWindow();
            //gameWindow.Show();
        }

        private void Log(object parameter)
        {
            var exception = new Exception();
            try
            {
                int.Parse("lala");
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            
            Tracer.Debug("this is log via tracer");
            Tracer.Info("this is a info via tracer");
            Tracer.Warn("WARNING");
            Tracer.Error("WOHO, error", exception);
        }
    }
}
