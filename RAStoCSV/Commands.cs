using System.Windows.Input;	// for RoutedCommand

namespace HirosakiUniversity.Aldente.RAStoCSV.RAStoCSV6
{

	#region [static]Commandsクラス
	public static class Commands
	{
		public static RoutedCommand LoadCommand = new RoutedCommand();
		public static RoutedCommand SelectFolderCommand = new RoutedCommand();
	}
	#endregion

}
