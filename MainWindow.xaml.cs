using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;

namespace MSB_SERVER
{
	public partial class MainWindow
	{
		private App serverApplication;

		private bool WINDOW_STATE_MAXIMIZED;

		public MainWindow()
		{
			serverApplication = (App) Application.Current;
			Closing += OnWindowClosing;
			InitializeComponent();
			InitializeTitleBar();
		}

		private void InitializeTitleBar()
		{
			WINDOW_STATE_MAXIMIZED = false;
			titleBar.MouseDown += OnTitleBarMouseDown;
			titleMainIcon.Source = new BitmapImage(new Uri(@"/MSB_SERVER;component/Resources/icon.png", UriKind.Relative));
			titleMinIcon.Source = new BitmapImage(new Uri(@"/MSB_SERVER;component/Resources/title_min.png", UriKind.Relative));
			titleMaxIcon.Source = new BitmapImage(new Uri(@"/MSB_SERVER;component/Resources/title_max.png", UriKind.Relative));
			titleEndIcon.Source = new BitmapImage(new Uri(@"/MSB_SERVER;component/Resources/title_end.png", UriKind.Relative));
			titleMinIcon.MouseLeftButtonUp += OnTitleMinClickedM;
			titleMaxIcon.MouseLeftButtonUp += OnTitleMaxClickedM;
			titleEndIcon.MouseLeftButtonUp += OnTitleEndClickedM;
		}

		private void OnTitleMinClicked(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}
		
		private void OnTitleMinClickedM(object sender, MouseEventArgs e)
		{
			OnTitleMinClicked(sender, new RoutedEventArgs());
		}

		private void OnTitleMaxClicked(object sender, RoutedEventArgs e)
		{
			if (WINDOW_STATE_MAXIMIZED)
			{
				WindowState = WindowState.Normal;
				WINDOW_STATE_MAXIMIZED = false;
			} else
			{
				WindowState = WindowState.Maximized;
				WINDOW_STATE_MAXIMIZED = true;
			}
		}
		
		private void OnTitleMaxClickedM(object sender, MouseEventArgs e)
		{
			OnTitleMaxClicked(sender, new RoutedEventArgs());
		}

		private void OnTitleEndClicked(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}
		
		private void OnTitleEndClickedM(object sender, MouseEventArgs e)
		{
			OnTitleEndClicked(sender, new RoutedEventArgs());
		}

		private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}

		private void OnServerButtonClicked(object sender, RoutedEventArgs e)
		{
			string SERVER_IP = "localhost";
			int SERVER_PORT = 9993;
			try
			{
				if (mainEditorIP.GetLineText(0) != null && mainEditorIP.GetLineText(0).Trim().Length != 0)
				{
					SERVER_IP = mainEditorIP.GetLineText(0).Trim();
				}
				SERVER_PORT = int.Parse(mainEditorPort.GetLineText(0).Trim());
			} catch { }
			serverApplication.graphicalManager.OnUserServerButton(SERVER_IP, SERVER_PORT);
		}

		private void OnCommandKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				string inputCommand = mainEditorCommand.GetLineText(0).Trim();
				if (!string.IsNullOrEmpty(inputCommand))
				{
					serverApplication.commandManager.ApplyCommand(inputCommand);
					mainEditorCommand.Clear();
				}
			}
		}

		private void OnWindowClosing(object sender, CancelEventArgs e)
		{
			try
			{
				serverApplication.networkManager.ServerStop();
			}
			catch { }
			Application.Current.Shutdown();
		}
	}
}
