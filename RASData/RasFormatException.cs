using System;
using System.Collections.Generic;
using System.Text;

namespace HirosakiUniversity.Aldente.RAStoCSV
{
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