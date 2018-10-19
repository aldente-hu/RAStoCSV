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
using System.IO;
using System.Collections.ObjectModel;
using Microsoft.Win32;  // for FileDialog.

namespace HirosakiUniversity.Aldente.RAStoCSV
{

	#region MainWindowクラス
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{

		#region プロパティ

		public ObservableCollection<SeriesData> MyData
		{
			get => _myData;
		}
		ObservableCollection<SeriesData> _myData = new ObservableCollection<SeriesData>();

		#endregion


		public MainWindow()
		{
			Initialize();

			InitializeComponent();
		}


		public void Initialize()
		{
			_myData.Clear();
		}

		#region 読み込み関連

		#region *データを読み込む(Load)
		private async Task Load(StreamReader reader)
		{
			var currentState = RasReadingState.Neutral;
			char[] headerSeparators = new char[] { ' ' };

			var line = await reader.ReadLineAsync();
			if (line != "*RAS_DATA_START")
			{
				// RASデータファイルではない！
				throw new RasFormatException("RASファイルのフォーマットが不適切です． #000");
			}


			// データ部読み取りモードなら．
			SeriesData seriesData = new SeriesData();
			// ※スキャン回数をキーにもつべきか？

			while (!reader.EndOfStream)
			{
				line = await reader.ReadLineAsync();

				switch (currentState)
				{
					case RasReadingState.Neutral:
						switch (line)
						{
							case "*RAS_HEADER_START":
								// ヘッダモードへ突入．
								seriesData = new SeriesData();
								currentState = RasReadingState.Header;
								break;
							case "*RAS_DATA_END":
								break;
							default:
								// 何かオカシイ？
								throw new RasFormatException("RASファイルのフォーマットが不適切です． #010");
						}
						break;
					case RasReadingState.AfterHeader:
						switch (line)
						{
							case "*RAS_INT_START":
								// データ部の開始．
								currentState = RasReadingState.Data;
								break;
							default:
								// 何かオカシイ？
								throw new RasFormatException("RASファイルのフォーマットが不適切です． #011");
						}
						break;
					case RasReadingState.Header:
						var cols = line.Split(headerSeparators);
						try
						{
							switch (cols.First())
							{
								case "*RAS_HEADER_END":
									// ヘッダモード終了．
									currentState = RasReadingState.AfterHeader;
									break;
								// 必要なヘッダを読み取る．
								case "*MEAS_SCAN_AXIS_X_INTERNAL":
									seriesData.AxisName = cols[1].Trim('"');
									break;
								case "*MEAS_SCAN_SPEED":
									var speed = decimal.Parse(cols[1].Trim('"'));
									seriesData.ScanSpeed = speed;
									break;
								case "*MEAS_SCAN_SPEED_UNIT":
									// "deg/min"以外にあるのかな？
									break;
								//case "*MEAS_SCAN_SPEED_USER":
								//	break;
								case "*MEAS_SCAN_STEP":
									var step = decimal.Parse(cols[1].Trim('"'));
									seriesData.ScanStep = step;
									break;
								case "*MEAS_SCAN_START":
									var start = decimal.Parse(cols[1].Trim('"'));
									seriesData.ScanStart = start;
									break;
								case "*MEAS_SCAN_STOP":
									var stop = decimal.Parse(cols[1].Trim('"'));
									seriesData.ScanStop = stop;
									break;
								case "*RAS_HEADER_START":
								case "*RAS_INT_START":
								case "*RAS_INT_END":
								case "*RAS_DATA_END":
									throw new RasFormatException("RASファイルのフォーマットが不適切です． #013");
								default:
									// 興味のないヘッダなので，何もしない．
									break;
							}
						}
						catch (IndexOutOfRangeException)
						{
							throw new RasFormatException("RASファイルのフォーマットが不適切です． #012");
						}
						break;
					case RasReadingState.Data:
						switch (line)
						{
							case "*RAS_INT_END":
								// データ部の終了．
								MyData.Add(seriesData);
								currentState = RasReadingState.Neutral;
								break;
							case "*RAS_INT_START":
							case "*RAS_HEADER_START":
							case "*RAS_HEADER_END":
							case "*RAS_DATA_END":
								throw new RasFormatException("RASファイルのフォーマットが不適切です． #021");
							default:
								// データの読み取り
								cols = line.Split(' ');
								try
								{
									seriesData.Add(Decimal.Parse(cols[0]), new CountData(decimal.Parse(cols[1]), decimal.Parse(cols[2])));
								}
								catch
								{
									throw new RasFormatException("RASファイルのフォーマットが不適切です． #020");
								}
								break;
						}
						break;
				}

			}

		}
		#endregion

		#region *1ファイルからデータを読み込む(LoadFrom)
		public async Task LoadFrom(string source)
		{
			using (StreamReader reader = new StreamReader(source))
			{
				await Load(reader);
			}

		}
		#endregion

		#region RasReadingState列挙体
		enum RasReadingState
		{
			/// <summary>
			/// 初期状態，もしくはデータ読み取り終了後．
			/// </summary>
			Neutral,
			/// <summary>
			/// ヘッダ読み取り中．
			/// </summary>
			Header,
			/// <summary>
			/// ヘッダ読み取り後．
			/// </summary>
			AfterHeader,
			/// <summary>
			/// データ読み取り中．
			/// </summary>
			Data
		}
		#endregion

		#endregion

		#region 出力関連

		#region *CSVとして出力する(Output)
		public async Task Output(StreamWriter writer, OutputUnit outputUnit)
		{

			var axis_names = MyData.Select(series => series.AxisName).Distinct();
			// 積算対応する必要があるか？
			if (MyData.Count() > 1 &&
					axis_names.Count() == 1 &&
					MyData.Select(series => series.ScanStep).Distinct().Count() == 1 &&
					MyData.Select(series => series.ScanStart).Distinct().Count() == 1 &&
					MyData.Select(series => series.ScanStop).Distinct().Count() == 1)
			{
				// [1]積算対応出力

				string total_caption = outputUnit == OutputUnit.CountRate ? "Total[cps]" : "Total";
				string header_line = $"# {axis_names.Single()}, {total_caption}";
				await writer.WriteLineAsync(header_line);

				// データ出力
				var keys = MyData.SelectMany(series => series.Keys).OrderBy(pos => pos);
				foreach (var x in keys)
				{
					var counts = MyData.Select(series => series[x].SubstantialCount);
					var count_rates = MyData.Select(series => series[x].SubstantialCount / series.DwellTime);
					if (outputUnit == OutputUnit.CountRate)
					{
						var total_dwell_time = MyData.Sum(series => series.DwellTime);
						writer.WriteLine($"{x:0.000000e+000},{counts.Sum()/total_dwell_time:0.000000e+000},{count_rates.Select(y => string.Join(", ", string.Format("0.000000e+000", y)))}");
					}
					else
					{
						writer.WriteLine($"{x:0.000000e+000},{counts.Sum():0.000000e+000},{counts.Select(y => string.Join(", ", string.Format("0.000000e+000", y)))}");
					}
				}
			}
			else
			{
				// [2]通常出力

				foreach (var series in MyData)
				{
					// ヘッダ出力
					string y_caption = outputUnit == OutputUnit.CountRate ? "yobs[cps]" : "yobs";
					string header_line = $"# {series.AxisName}, {y_caption}";
					await writer.WriteLineAsync(header_line);
					foreach (var x in series.Keys)
					{
						string line;
						if (outputUnit == OutputUnit.CountRate)
						{
							line = $"{x:0.000000e+000},{series[x].SubstantialCount / series.DwellTime:0.000000e+000}";
						}
						else
						{
							line = $"{x:0.000000e+000},{series[x].SubstantialCount:0.000000e+000}";
						}
						await writer.WriteLineAsync(line);
					}
					await writer.WriteLineAsync();
				}

			}

		}
		#endregion

		#region *指定したファイルへ出力する(OutputTo)
		public async Task OutputTo(string destination, OutputUnit outputUnit)
		{
			using (var stream = new FileStream(destination, FileMode.Create))
			{
				using (var writer = new StreamWriter(stream, Encoding.UTF8))
				{
					await Output(writer, outputUnit);
				}
			}
		}
		#endregion

		#region *RASをCSVに変換する(Convert)
		public async Task Convert(IEnumerable<string> files)
		{
			foreach (var source in files)
			{
				try
				{
					await LoadFrom(source);

					Path.GetFileNameWithoutExtension(source);
					var destination = $"{Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + ".csv")}";
					try
					{
						await OutputTo(destination, radioButtonCps.IsChecked == true ? OutputUnit.CountRate : OutputUnit.Count);
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
					Initialize();
				}
			}

		}
		#endregion

		#region OutputUnit列挙体
		public enum OutputUnit
		{
			/// <summary>
			/// カウント数を出力します．
			/// </summary>
			Count,
			/// <summary>
			/// カウント率を出力します．
			/// </summary>
			CountRate
		}
		#endregion

		#endregion


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
				if (n > 25)
				{
					if (MessageBox.Show($"{n} 個のファイルについて処理します．よろしいですか？", "実行確認（ファイルがたくさん）", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
					{
						return;
					}
				}
				await Convert(files);
			}
		}

		private async void Load_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new OpenFileDialog { Filter = "RASファイル(*.ras)|*.ras", Multiselect = true };
			if (dialog.ShowDialog() == true)
			{
				await Convert(dialog.FileNames);
			}
		}

		#endregion


	}
	#endregion


	#region CountData構造体
	public struct CountData
	{
		/// <summary>
		/// カウント数．なぜかdecimal型．
		/// </summary>
		decimal Count;
		/// <summary>
		/// アッテネータ係数．
		/// </summary>
		decimal Factor;

		/// <summary>
		/// (アッテネータ係数を考慮した)実質的なカウントを取得します．
		/// </summary>
		public decimal SubstantialCount
		{
			get => Count * Factor;
		}

		public CountData(decimal count, decimal factor)
		{
			this.Count = count;
			this.Factor = factor;
		}


	}
	#endregion

	#region SeriesDataクラス
	public class SeriesData : Dictionary<decimal, CountData>
	{
		/// <summary>
		/// スキャン軸の名前を取得／設定します．
		/// </summary>
		public string AxisName { get; set; }

		/// <summary>
		/// スキャンスピードを取得／設定します．単位はdeg/minです．
		/// </summary>
		public decimal ScanSpeed { get; set; }

		/// <summary>
		/// スキャンステップを取得／設定します．単位はdegです．
		/// </summary>
		public decimal ScanStep { get; set; }

		/// <summary>
		/// スキャン開始位置を取得／設定します．
		/// </summary>
		public decimal ScanStart { get; set; }

		/// <summary>
		/// スキャン終了位置を取得／設定します．
		/// </summary>
		public decimal ScanStop { get; set; }

		/// <summary>
		/// 1点あたりの計測時間を取得します．単位はsecです．
		/// </summary>
		public decimal DwellTime
		{
			get => 60 * ScanStep / ScanSpeed;
		}
	}
	#endregion

	#region [static]Commandクラス
	public static class Commands
	{
		public static RoutedCommand LoadCommand = new RoutedCommand();
		public static RoutedCommand SelectFolderCommand = new RoutedCommand();
	}
	#endregion

	#region RasFormatExceptionクラス
	[System.Serializable]
	public class RasFormatException : Exception
	{
		public RasFormatException() { }
		public RasFormatException(string message) : base(message) { }
		public RasFormatException(string message, Exception inner) : base(message, inner) { }
		protected RasFormatException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
	#endregion

}
