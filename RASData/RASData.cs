using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;

namespace HirosakiUniversity.Aldente.RAStoCSV
{
	public class RASData
	{

		#region プロパティ

		public ObservableCollection<SeriesData> SeriesCollection
		{
			get => _seriesCollection;
		}
		ObservableCollection<SeriesData> _seriesCollection = new ObservableCollection<SeriesData>();

		#endregion

		public void Clear()
		{
			SeriesCollection.Clear();
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
								case "*FILE_DATA_TYPE":
									var data_type = cols[1].Trim('"');
									if (data_type == "RAS_3DE_POLEFIG")
									{
										// 極点測定だよ！
										seriesData.IsPoleFigure = true;
									}
									break;
								case "*MEAS_3DE_ALPHA_ANGLE":
									// ※とりあえず決め打ちにしておく．
									var alpha = decimal.Parse(cols[1].Trim('"'));
									seriesData.Alpha = alpha;
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
								SeriesCollection.Add(seriesData);
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

		// (0.1.2) useTotal引数を追加．
		#region *指定したファイルへ出力する(OutputTo)
		public async Task OutputTo(string destination, OutputUnit outputUnit, string decimalFormat, bool useTotal = true)
		{
			using (var stream = new FileStream(destination, FileMode.Create))
			{
				using (var writer = new StreamWriter(stream, Encoding.UTF8))
				{
					await Output(writer, outputUnit, decimalFormat, useTotal);
				}
			}
		}
		#endregion

		// (0.2.1) なぜかコメントアウトされていた極点測定出力を復活．
		// (0.1.2) useTotal引数を追加．
		#region *CSVとして出力する(Output)
		public async Task Output(StreamWriter writer, OutputUnit outputUnit, string decimalFormat, bool useTotal = true)
		{

			if (SeriesCollection.All(series => series.IsPoleFigure))
			{
				// 極点測定データとして出力．
				await OutputPoleFigure(writer, outputUnit, decimalFormat);
			}
			else
			{
				var axis_names = SeriesCollection.Select(series => series.AxisName).Distinct();
				// 積算対応する必要があるか？
				if (useTotal &&
					(SeriesCollection.Count() > 1 &&
						axis_names.Count() == 1 &&
						SeriesCollection.Select(series => series.ScanStep).Distinct().Count() == 1 &&
						SeriesCollection.Select(series => series.ScanStart).Distinct().Count() == 1 &&
						SeriesCollection.Select(series => series.ScanStop).Distinct().Count() == 1))
				{
					// [1]積算対応出力
					await OutputCumulation(writer, axis_names.Single(), outputUnit, decimalFormat);
				}
				else
				{
					// [2]通常出力
					await OutputNormal(writer, outputUnit, decimalFormat);
				}
			}

		}
		#endregion


		public async Task OutputNormal(StreamWriter writer, OutputUnit outputUnit, string decimalFormat)
		{
			foreach (var series in SeriesCollection)
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
						line = $"{x.ToString(decimalFormat)},{(series[x].SubstantialCount / series.DwellTime).ToString(decimalFormat)}";
					}
					else
					{
						line = $"{x.ToString(decimalFormat)},{series[x].SubstantialCount.ToString(decimalFormat)}";
					}
					await writer.WriteLineAsync(line);
				}
				await writer.WriteLineAsync();
			}

		}

		public async Task OutputCumulation(StreamWriter writer, string axisName, OutputUnit outputUnit, string decimalFormat)
		{
			string total_caption = outputUnit == OutputUnit.CountRate ? "Total[cps]" : "Total";
			string header_line = $"# {axisName}, {total_caption}";
			await writer.WriteLineAsync(header_line);

			// データ出力
			var keys = SeriesCollection.SelectMany(series => series.Keys).Distinct().OrderBy(pos => pos);
			foreach (var x in keys)
			{
				var counts = SeriesCollection.Select(series => series[x].SubstantialCount);
				var count_rates = SeriesCollection.Select(series => series[x].SubstantialCount / series.DwellTime);
				if (outputUnit == OutputUnit.CountRate)
				{
					var total_dwell_time = SeriesCollection.Sum(series => series.DwellTime);
					writer.WriteLine($"{x.ToString(decimalFormat)},{(counts.Sum() / total_dwell_time).ToString(decimalFormat)},{string.Join(",", count_rates.Select(r => r.ToString(decimalFormat)).ToArray())}");
				}
				else
				{
					writer.WriteLine($"{x.ToString(decimalFormat)},{counts.Sum().ToString(decimalFormat)},{string.Join(",", counts.Select(c => c.ToString(decimalFormat)).ToArray())}");
				}
			}

		}

		// (0.2.1) 軸名をalphaとbetaに変更．
		public async Task OutputPoleFigure(StreamWriter writer, OutputUnit outputUnit, string decimalFormat)
		{
			// とりあえず1次元テーブルで出力してみる．
			// ※2次元テーブル出力の実装は後で考える．

			// ヘッダ出力
			// 軸名はalphaとbetaで決め打ちする．
			string y_caption = outputUnit == OutputUnit.CountRate ? "yobs[cps]" : "yobs";
			string header_line = $"# alpha, beta, {y_caption}";
			await writer.WriteLineAsync(header_line);

			// データ出力
			foreach (var series in SeriesCollection)
			{
				foreach (var x in series.Keys)
				{
					string line;
					if (outputUnit == OutputUnit.CountRate)
					{
						line = $"{series.Alpha.ToString(decimalFormat)},{x.ToString(decimalFormat)},{(series[x].SubstantialCount / series.DwellTime).ToString(decimalFormat)}";
					}
					else
					{
						line = $"{series.Alpha.ToString(decimalFormat)},{x.ToString(decimalFormat)},{series[x].SubstantialCount.ToString(decimalFormat)}";
					}
					await writer.WriteLineAsync(line);
				}
			}

		}

		#endregion

	}
}
