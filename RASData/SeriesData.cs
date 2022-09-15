using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HirosakiUniversity.Aldente.RAStoCSV
{

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
		/// 極点測定のデータかどうかを示す値を取得／設定します．
		/// </summary>
		public bool IsPoleFigure { get; set; }

		/// <summary>
		/// 極点測定の場合のαの値を取得／設定します．
		/// </summary>
		public decimal Alpha { get; set; }

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

}
