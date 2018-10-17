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

namespace HirosakiUniversity.Aldente.RAStoCSV
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		public async Task ConvertToCsv()
		{
			string source = string.Empty;
			// 読み込む

			using (StreamReader reader = new StreamReader(source))
			{
				var line = await reader.ReadLineAsync();
				if (line != "*RAS_DATA_START")
				{
					// RASデータファイルではない！
				}


				// データ部読み取りモードなら．
				Dictionary<decimal, CountData> seriesData = new Dictionary<decimal, CountData>();
				// ※スキャン回数をキーにもつべきか？

				while (!reader.EndOfStream)
				{
					line = await reader.ReadLineAsync();

					// データを読み取るなら...
					var cols = line.Split(' ');
					if (cols.Length > 2)
					{
						seriesData.Add(Decimal.Parse(cols[0]), new CountData(int.Parse(cols[1]), decimal.Parse(cols[2])));
					}
					else
					{
						// データ形式がイクナイ．
					}

					switch (line)
					{
						case "*RAS_HEADER_START":
							// ヘッダモードへ突入．
							break;
						case "*RAS_HEADER_END":
							// ヘッダモード終了．
							break;
						case "*RAS_INT_START":
							// データ部の開始．
							break;
						case "*RAS_INT_END":
							// データ部の終了．
							break;
						default:
							// データの読み取り
							break;
					}
				}


			}

			// ヘッダを読み込む

			// データを読み込む



		}




	}

	public struct CountData
	{
		int Count;
		decimal Factor;

		public CountData(int count, decimal factor)
		{
			this.Count = count;
			this.Factor = factor;
		}
	}


}
