using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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
//using System.Windows.Shapes;



namespace HirosakiUniversity.Aldente.RAStoCSV.RAStoCSV6
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		#region プロパティ

		public RASData MyData
		{
			get => _myData;
		}
		RASData _myData = new RASData();

		/// <summary>
		/// CSVでの数値の表記フォーマットを取得／設定します．
		/// </summary>
		public string DecimalFormat { get; set; }

		// 手抜きっぽいなぁ．
		const string FORMAT_EXPONENTIAL = "e";
		const string FORMAT_FIXED = "f3";

		#endregion


		public MainWindow()
		{
			_myData.Clear();
			infoTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };

			InitializeComponent();

			radioButtonExponential.IsChecked = true;
			DecimalFormat = FORMAT_EXPONENTIAL;

		}



		#region 出力関連


		#region *RASをCSVに変換する(Convert)
		public async Task<int> Convert(IEnumerable<string> files)
		{
			int succeeded = 0;
			foreach (string source in files)
			{
				try
				{
					await MyData.LoadFrom(source);

					var destination = $"{Path.Combine(Path.GetDirectoryName(source) ?? string.Empty, Path.GetFileNameWithoutExtension(source) + ".csv")}";
					try
					{
						await MyData.OutputTo(destination, radioButtonCps.IsChecked == true ? OutputUnit.CountRate : OutputUnit.Count, this.DecimalFormat, checkBoxUseTotal.IsChecked == true);
						succeeded += 1;
					}
					catch (Exception ex)
					{
						MessageBox.Show($"ファイル {destination} の出力中に，次のエラーが発生しました．：{ex.Message}");
					}
				}
				catch (RasFormatException ex)
				{
					MessageBox.Show($"ファイル {source} の読み込み中に，次のエラーが発生しました．：{ex.Message}");
				}
				finally
				{
					_myData.Clear();
				}
			}
			return succeeded;
		}
		#endregion

		#endregion

		#region InfoLabel関連

		System.Windows.Threading.DispatcherTimer infoTimer;

		void infoTimer_Tick(object? sender, EventArgs e)
		{
			textBlockInfo.Text = string.Empty;
			infoTimer.Stop();
		}

		protected void SayInfo(string message)
		{
			textBlockInfo.Text = message;
			infoTimer.Start();
		}


		#region *ユーザに挨拶(Greet)
		protected string Greet(DateTime time)
		{
			if (time.AddDays(8).DayOfYear <= 2)	// 12/24 or 12/25
			{
				return "Happy Holidays!";
			}
			else if (time.Hour >= 4 && time.Hour < 12)
			{
				return "おはようございます";
			}
			else if (time.Hour >= 12 && time.Hour < 19)
			{
				return "こんにちは";
			}
			else if (time.Hour >= 19)
			{
				return "今日もお疲れ様です";
			}
			else
			{
				return "(=_=) zzz...";
			}
		}
		#endregion

		#endregion

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			SayInfo(Greet(DateTime.Now));
		}

		private void Window_Initialized(object sender, EventArgs e)
		{
			// infoTimerを初期化．
			infoTimer.Tick += new EventHandler(infoTimer_Tick);

		}

		private void RadioButtonDecimalFormat_Checked(object sender, RoutedEventArgs e)
		{
			if (radioButtonExponential.IsChecked == true)
			{
				this.DecimalFormat = FORMAT_EXPONENTIAL;
			}
			else if (radioButtonFixed.IsChecked == true)
			{
				this.DecimalFormat = FORMAT_FIXED;
			}
		}

		#region コマンドハンドラ

		private async void SelectFolder_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new Aldentea.Wpf.Controls.FolderBrowserDialog
			{
				Description = "選択したフォルダ以下にあるすべてのRASファイルをCSV形式にエクスポートします．",
				DisplaySpecialFolders = Aldentea.Wpf.Controls.SpecialFoldersFlag.MyDocuments
			};

			if (dialog.ShowDialog() == true)
			{
				var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.ras", SearchOption.AllDirectories);
				int n = files.Count();
				if (n > 20)
				{
					if (MessageBox.Show(
						$"{n} 個のファイルについて処理します．よろしいですか？", "実行確認（ファイルがたくさん）",
						MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
					{
						SayInfo("処理を中止しました．");
						return;
					}
				}
				int succeeded = await Convert(files);
				SayInfo($"出力処理が完了しました．({succeeded}/{files.Count()})");
			}
			else
			{
				SayInfo("処理を中止しました．");
			}
		}

		private async void Load_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new OpenFileDialog { Filter = "RASファイル(*.ras)|*.ras", Multiselect = true };
			if (dialog.ShowDialog() == true)
			{
				var files = dialog.FileNames;
				var succeeded = await Convert(files);
				SayInfo($"出力処理が完了しました．({succeeded}/{files.Count()})");

			}
			else
			{
				SayInfo("処理を中止しました．");
			}
		}


		#endregion
	}
}
