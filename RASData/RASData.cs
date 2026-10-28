using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace HirosakiUniversity.Aldente.RAStoCSV
{
	public class RASData
	{

		//public const string FORMAT_EXPONENTIAL = "e";

		/// <summary>
		/// format for x-axis.
		/// </summary>
		public const string FORMAT_FIXED = "f3";

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


        #region *CSVとして出力する(OutputData)
        public string OutputData(string cpsFormat, string countFormat, bool useTotal = true)
        {

            if (SeriesCollection.All(series => series.IsPoleFigure))
            {
                // 極点測定データとして出力．
                return OutputPoleFigureData(cpsFormat, countFormat);
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
                    return OutputCumulationData(axis_names.Single(), cpsFormat, countFormat);
                }
                else
                {
                    // [2]通常出力
                    return OutputNormalData(cpsFormat, countFormat);
                }
            }

        }
        #endregion



		public string OutputNormalData(string cpsFormat, string countFormat)
		{
			StringBuilder sb = new StringBuilder();

            foreach (var series in SeriesCollection)
            {
                // ヘッダ出力
                var columns = new List<string> { series.AxisName };
                if (string.IsNullOrEmpty(cpsFormat) == false)
                {
                    columns.Add("yobs[cps]");
                }
                if (string.IsNullOrEmpty(countFormat) == false)
                {
                    columns.Add("yobs");
                }
                string header_line = $"# {string.Join(", ", columns)}";
                sb.AppendLine(header_line);

                // データ出力
                foreach (var x in series.Keys)
                {
                    List<string> values = new List<string> { x.ToString(FORMAT_FIXED) };
                    if (string.IsNullOrEmpty(cpsFormat) == false)
                    {
                        values.Add((series[x].SubstantialCount / series.DwellTime).ToString(cpsFormat));
                    }
                    if (string.IsNullOrEmpty(countFormat) == false)
                    {
                        values.Add(series[x].SubstantialCount.ToString(countFormat));
                    }

                    sb.AppendLine(string.Join(", ", values));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }


		public string OutputCumulationData(string axisName, string cpsFormat, string countFormat)
		{

			// Column1 x
			// Column2 cps
			// Column3 Total counts
			// Column4... each count
			StringBuilder sb = new StringBuilder();

			// ヘッダ出力
			var columns = new List<string> { axisName };
			if (string.IsNullOrEmpty(cpsFormat) == false)
			{
				columns.Add("Total[cps]");
			}
			if (string.IsNullOrEmpty(countFormat) == false)
			{
				columns.Add("Total");
			}
			string header_line = $"# {string.Join(", ", columns)}";
			sb.AppendLine(header_line);


			// データ出力
			var keys = SeriesCollection.SelectMany(series => series.Keys).Distinct().OrderBy(pos => pos);
			foreach (var x in keys)
			{
				List<string> values = new List<string> { x.ToString(FORMAT_FIXED) };

				var counts = SeriesCollection.Select(series => series[x].SubstantialCount);
				//var count_rates = SeriesCollection.Select(series => series[x].SubstantialCount / series.DwellTime);

				if (string.IsNullOrEmpty(cpsFormat) == false)
				{
					var total_dwell_time = SeriesCollection.Sum(series => series.DwellTime);
					var total_cps = total_dwell_time > 0 ? counts.Sum() / total_dwell_time : 0;
					values.Add(total_cps.ToString(cpsFormat));
				}

				if (string.IsNullOrEmpty(countFormat) == false)
				{
					var total_count = counts.Sum();
					values.Add(total_count.ToString(countFormat));
					// 各系列のカウントを追加
					values.AddRange(counts.Select(c => c.ToString(countFormat)));
                }
				sb.AppendLine(string.Join(", ", values));
            }

			return sb.ToString();

        }

		// (0.2.1) 軸名をalphaとbetaに変更．
		public string OutputPoleFigureData(string cpsFormat, string countFormat)
        {
            // とりあえず1次元テーブルで出力してみる．
            // ※2次元テーブル出力の実装は後で考える．
            StringBuilder sb = new StringBuilder();

<<<<<<< HEAD
			// ステップ軸はAlphaで決め打ちする．
			await Output2dData(writer, outputUnit, decimalFormat, "Alpha");

		}

		private async Task Output2dData(string cpsFormat, string countFormat, string step_axis)
		{

			bool first = true;

			foreach (var series in SeriesCollection)
			{
				if (first)
				{
					// ヘッダを出力する．
					{
						string y_caption = outputUnit == OutputUnit.CountRate ? "yobs[cps]" : "yobs";
						string header_line = $"# {step_axis}, {series.AxisName}, {y_caption}";
						await writer.WriteLineAsync(header_line);
					}

					first = false;
				}
				// データ出力
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
=======
            // Column 1 alpha
            // Column 2 beta
            // Column 3 cps
            // Column 4 count


            // ヘッダ出力
            var columns = new List<string> { "alpha", "beta" };
            if (string.IsNullOrEmpty(cpsFormat) == false)
            {
                columns.Add("yobs[cps]");
            }
            if (string.IsNullOrEmpty(countFormat) == false)
            {
                columns.Add("yobs");
            }
            string header_line = $"# {string.Join(", ", columns)}";
            sb.AppendLine(header_line);
>>>>>>> 9d58566 (Enable to output both cps and counts.)

            // データ出力
            foreach (var series in SeriesCollection)
            {
                foreach (var x in series.Keys)
                {
                    List<string> values = new List<string> { series.Alpha.ToString(FORMAT_FIXED), x.ToString(FORMAT_FIXED) };
                    if (string.IsNullOrEmpty(cpsFormat) == false)
                    {
                        values.Add((series[x].SubstantialCount / series.DwellTime).ToString(cpsFormat));
                    }
                    if (string.IsNullOrEmpty(countFormat) == false)
                    {
                        values.Add(series[x].SubstantialCount.ToString(countFormat));
                    }
                    sb.AppendLine(string.Join(", ", values));
                }
            }
            return sb.ToString();
        }

        #endregion

    }
}
