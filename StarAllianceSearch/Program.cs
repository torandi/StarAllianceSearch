namespace StarAllianceSearch
{
	class Program
	{
		public static async System.Threading.Tasks.Task<int> Main(string[] args)
		{

			var client = new ConsoleClient();

			if (!client.ParseArguments(args))
				return 1;

			await client.Run();

			return 0;
		}
	}
}
