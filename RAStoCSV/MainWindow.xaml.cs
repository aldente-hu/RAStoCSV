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
using Microsoft.Win32;	// for FileDialog.

namespace HirosakiUniversity.Aldente.RAStoCSV
{
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
		

		public async Task ConvertToCsv()
		{

			string source = string.Empty;

			List<SeriesData> data = new List<SeriesData>();

			#region 読み込む
			using (StreamReader reader = new StreamReader(source))
			{
				await Load(reader);
			}
			#endregion


			#region 出力する

			string destination = string.Empty;

			using (var stream = new FileStream(destination, FileMode.CreateNew)) {
				using (var writer = new StreamWriter(stream, Encoding.UTF8))
				{
					foreach (var series in MyData)
					{
						foreach (var x in series.Keys)
						{
							await writer.WriteLineAsync($"{x:0.000000e+000},{series[x]:0.000000e+000}");
						}
					}
				}
			}
			#endregion

		}

		#region *データを読み込む(Load)
		private async Task Load(StreamReader reader)
		{
			var currentState = RasReadingState.Neutral;
			char[] headerSeparators = new char[] { ' ' };

			var line = await reader.ReadLineAsync();
			if (line != "*RAS_DATA_START")
			{
				// RASデータファイルではない！
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
							default:
								// 何かオカシイ？
								break;
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
								break;
						}
						break;
					case RasReadingState.Header:
						var cols = line.Split(headerSeparators);
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
							default:
								// 興味のないヘッダなので，何もしない．
								break;
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
							default:
								// データの読み取り
								// データを読み取るなら...
								cols = line.Split(' ');
								if (cols.Length > 2)
								{
									seriesData.Add(Decimal.Parse(cols[0]), new CountData(decimal.Parse(cols[1]), decimal.Parse(cols[2])));
								}
								else
								{
									// データ形式がイクナイ．
								}
								break;
						}
						break;
				}

			}

		}
		#endregion

		public async Task LoadFrom(string source)
		{
			using (StreamReader reader = new StreamReader(source))
			{
				await Load(reader);
			}

		}

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


		public async Task Output(StreamWriter writer)
		{
			// とりあえずの手抜き実装．
			foreach (var series in MyData)
			{
				// ヘッダ出力
				await writer.WriteLineAsync($"# {series.AxisName}, yobs");
				foreach (var x in series.Keys)
				{
					await writer.WriteLineAsync($"{x:0.000000e+000},{series[x].SubstantialCount:0.000000e+000}");
				}
			}

		}

		public async Task OutputTo(string destination)
		{
			using (var stream = new FileStream(destination, FileMode.Create))
			{
				using (var writer = new StreamWriter(stream, Encoding.UTF8))
				{
					await Output(writer);
				}
			}
		}


		private async void Load_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new OpenFileDialog { Filter = "RASファイル(*.ras)|*.ras", Multiselect = true };
			if (dialog.ShowDialog() == true)
			{
				foreach (var source in dialog.FileNames)
				{
					await LoadFrom(source);

					Path.GetFileNameWithoutExtension(source);
					var destination = $"{Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + ".csv")}";
					await OutputTo(destination);

					Initialize();
				}
			}
		}
	}

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
		/// 1点あたりの計測時間を取得します．単位はsecです．
		/// </summary>
		public decimal DwellTime
		{
			get => 60 * ScanStep / ScanSpeed;
		}
	}
	#endregion

	public static class Commands
	{
		public static RoutedCommand LoadCommand = new RoutedCommand();
	}
}
