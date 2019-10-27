using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

namespace MSB_SERVER
{
	public partial class MainWindow : Window
	{
		private MSB_SERVER.App serverApplication;

		private bool WINDOW_STATE_MAXIMIZED;

		public MainWindow()
		{
			serverApplication = (MSB_SERVER.App) Application.Current;
			this.Closing += OnWindowClosing;
			InitializeComponent();
			InitializeTitleBar();
		}

		private void InitializeTitleBar()
		{
			WINDOW_STATE_MAXIMIZED = false;
			titleBar.MouseDown += new MouseButtonEventHandler(OnTitleBarMouseDown);
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
			this.WindowState = WindowState.Minimized;
		}
		
		private void OnTitleMinClickedM(object sender, MouseEventArgs e)
		{
			OnTitleMinClicked(sender, new RoutedEventArgs());
		}

		private void OnTitleMaxClicked(object sender, RoutedEventArgs e)
		{
			if (WINDOW_STATE_MAXIMIZED)
			{
				this.WindowState = WindowState.Normal;
				WINDOW_STATE_MAXIMIZED = false;
			} else
			{
				this.WindowState = WindowState.Maximized;
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
			this.DragMove();
		}

		private void OnServerButtonClicked(object sender, RoutedEventArgs e)
		{
			string SERVER_IP = "localhost";
			int SERVER_PORT = 8888;
			try
			{
				if (mainEditorIP.GetLineText(0) != null && mainEditorIP.GetLineText(0).Trim().Length != 0)
				{
					SERVER_IP = mainEditorIP.GetLineText(0).Trim();
				}
				SERVER_PORT = Int32.Parse(mainEditorPort.GetLineText(0).Trim());
			} catch { }
			serverApplication.graphicalManager.OnUserServerButton(SERVER_IP, SERVER_PORT);
		}

		private void OnCommandKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				string inputCommand = mainEditorCommand.GetLineText(0).Trim();
				if (!String.IsNullOrEmpty(inputCommand))
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
			System.Windows.Application.Current.Shutdown();
		}
	}
}
