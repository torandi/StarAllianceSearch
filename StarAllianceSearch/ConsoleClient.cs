using FlysasLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StarAllianceSearch
{
	class ConsoleClient
	{
		[Argument("From", ArgumentFlag.Required, Description = "Originating airport (code).")]
		public string From { get; set; }

		[Argument("To", ArgumentFlag.Required, Description = "Destination airport (code).")]
		public List<string> To { get; set; } = new List<string>();

		[Argument("Out",  ArgumentFlag.Required, Description = "The first day to start searching out trips from (format YYYY-MM-DD).")]
		public DateTime OutStart { get; set; }

		[Argument("In", ArgumentFlag.Required, Description = "The first day to start searching return trips from (format YYYY-MM-DD).")]
		public DateTime InStart { get; set; }

		[Argument("SearchSpan", Description = "Number of days to search in (each results in a new query).")]
		public int DaysSearch { get; set; } = 7;

		[Argument("BannedCarriers", ArgumentFlag.NoDefault, Description = "A comma separated list of carrier codes to filter out.")]
		public List<string> BannedCarriers { get; set; } = new List<string>();

		[Argument("MaxStops", Description = "Maximum numbers of stops to allow for the trip (-1 = no limit).")]
		public int MaxStops { get; set; } = -1;

		[Argument("MaxTransitStops", Description = "Maximum number of transit stops (stop without changing airplane). -1 = no limit).")]
		public int MaxTransitStops { get; set; } = 0;


		//[Argument("MaxQueries", Description = "Max number of queries that are allowed to be in flight at once.")]
		//public int MaxQueries { get; set; } = -1;

		[Argument("Config", ArgumentFlag.NoDefault, Description = "Config file to read options from. One option per line, format is argument=value. (Lines starting with # is ignored).")]
		public string Config { get; set; } = "";

		[Argument("TranslateCodes", Description = "Write a table after all trips with translations for all codes.")]
		public bool TranslateCodes { get; set; } = false;

		[Argument("Help", Description = "Print this help message.")]
		public bool Help { get; set; } = false;

		private readonly System.IO.TextWriter txtOut = Console.Out;
		private readonly SASRestClient client = new SASRestClient();

		struct ArgumentProperty
		{
			public Argument argument;
			public PropertyInfo property;
		};

		private readonly Dictionary<string, ArgumentProperty> arguments = new Dictionary<string, ArgumentProperty>();
		private readonly SortedDictionary<string, string> codes = new SortedDictionary<string, string>();

		private readonly HashSet<string> seenArguments = new HashSet<string>();

		struct Result
		{
			public IEnumerable<FlightBaseClass> OutBound;
			public IEnumerable<FlightBaseClass> InBound;
		};

		private static Argument FindArgument(PropertyInfo property)
		{
			object[] attributes = property.GetCustomAttributes(true);
			foreach (object attr in attributes)
			{
				if (attr is Argument argument)
					return argument;
			}
			return null;
		}

		public ConsoleClient()
		{
			PropertyInfo[] properties = this.GetType().GetProperties();
			foreach (PropertyInfo property in properties)
			{
				Argument argument = FindArgument(property);
				if (argument != null)
				{
					argument.Object = this;

					Type type = property.PropertyType;

					if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
					{
						argument.Flags |= ArgumentFlag.List;
					}

					if (argument.Flags.HasFlag(ArgumentFlag.List))
					{
						if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
						{
							type = type.GetGenericArguments()[0];
						}
						else
						{
							throw new Exception("Incorrect setup Arguments, list can only be used on List<>");
						}
					}

					if (argument.Flags.HasFlag(ArgumentFlag.Required))
						argument.DefaultValue = "required";
					else if (property.PropertyType == typeof(bool) || argument.Flags.HasFlag(ArgumentFlag.NoDefault))
						argument.DefaultValue = null;
					else
						argument.DefaultValue = property.GetValue(this).ToString();

					argument.PropertyType = type;

					arguments.Add(argument.Name.ToLower(), new ArgumentProperty
					{
						argument = argument,
						property = property
					});
				}

			}
		}

		public bool ParseArguments(string[] args)
		{
			bool anyError = false;

			for (int i = 0; i < args.Length; ++i)
			{
				if (args[i].StartsWith("-"))
				{
					string key = args[i];
					string value = "";
					if ((i + 1) < args.Length)
					{
						if (!args[i + 1].StartsWith("-"))
						{
							value = args[i + 1];
							++i; // skip parsing next argument (it's a value for this one)
						}
					}
					if (!ParseArgument(key, value)) // bool arguments can be treated as flags
						anyError = true;
				}
				else
				{
					Console.Error.WriteLine("Incorrect syntax ({0}), arguments must start with -. (add -help for help)", args[i]);
					return false;
				}
			}

			if (!Config.IsNullOrEmpty())
			{
				if (!ParseConfig(Config))
					return false;
			}

			if (Help)
			{
				Console.Error.WriteLine("STAR ALLIANCE BUSINESS CLASS SEARCH");
				Console.Error.WriteLine("Arguments: (default value in parethesis)");

				foreach (KeyValuePair<string, ArgumentProperty> kv in arguments)
				{
					string defaultValue = "";
					if (kv.Value.argument.DefaultValue != null)
						defaultValue = String.Format("({0})", kv.Value.argument.DefaultValue);
					Console.Error.WriteLine("-{0,-15} {1, -10} {2, -100}", kv.Value.argument.Name, defaultValue, kv.Value.argument.Description);
				}
				anyError = true; // don't execute after a help message
			}
			else
			{
				foreach (KeyValuePair<string, ArgumentProperty> kv in arguments)
				{
					if (kv.Value.argument.Flags.HasFlag(ArgumentFlag.Required) && !seenArguments.Contains(kv.Key))
					{
						Console.Error.WriteLine("Must provide argument {0}. See -help for more info.", kv.Value.argument.Name);
						anyError = true;
					}
				}
			}

			return !anyError;
		}

		private bool ParseConfig(string filePath)
		{
			string[] lines = File.ReadAllLines(filePath);
			foreach (string line in lines)
			{
				string tmp = line.Trim();
				if (tmp.StartsWith("#"))
					continue;

				string[] parts = tmp.Split("=");
				if (parts.Length != 2)
				{
					Console.Error.WriteLine("Invalid option format {0}. Format must be Argument=Value", line);
					return false;
				}
				if (!ParseArgument(parts[0], parts[1]))
					return false;
			}

			return true;
		}

		private bool ParseArgument(string name, string value)
		{
			string nameLower = name.ToLower();
			if (nameLower.StartsWith("-"))
				nameLower = nameLower.Substring(1);


			ArgumentProperty argument;
			if (arguments.TryGetValue(nameLower, out argument))
			{
				if (argument.property.PropertyType == typeof(bool) && value == "")
					value = "true";

				try
				{
					argument.argument.Parse(value, argument.property);
					seenArguments.Add(nameLower);
					return true;
				}
				catch
				{
					Console.WriteLine("Failed to parse argument {0} {1} as type {2}", name, value, argument.property.PropertyType.Name);
					return false;
				}
			}
			else
			{
				Console.Error.WriteLine("Unknown argument {0}", name);
				return false;
			}
		}

		public async Task Run()
		{
			string dateFormat = "yyyy-MM-dd";

			Console.Error.WriteLine("Searching for {0} to {1}", From, String.Join(", ", To));
			Console.Error.WriteLine("Searching for dates {0} - {1} (search range {2} days)"
				, OutStart.ToString(dateFormat, CultureInfo.InvariantCulture)
				, InStart.ToString(dateFormat, CultureInfo.InvariantCulture)
				, DaysSearch);
			foreach (var to in To)
			{
				Console.Error.WriteLine("Searching to {0}...", to);
				var results = await Task.WhenAll(Enumerable.Range(0, DaysSearch).Select(offset => Search(to, offset)));

				List<FlightBaseClass> OutBound = new List<FlightBaseClass>();
				List<FlightBaseClass> InBound = new List<FlightBaseClass>();
				foreach (var result in results)
				{
					OutBound.AddRange(result.OutBound);
					InBound.AddRange(result.InBound);
				}

				txtOut.WriteLine("Results to {0}", to);
				txtOut.WriteLine("***** Out-bound *****");
				PrintFlights(OutBound);
				txtOut.WriteLine("***** In-bound *****");
				PrintFlights(InBound);
				txtOut.WriteLine("----------------------------------------------");
			}

			if (TranslateCodes)
			{
				txtOut.WriteLine("");
				txtOut.WriteLine("Airline and Airport Codes");
				foreach (var entry in codes)
				{
					txtOut.WriteLine("{0,-10}{1,-20}", entry.Key, entry.Value);
				}
			}
		}

		private bool FilterFlight(FlightBaseClass flight)
		{
			if (flight.cabins.business == null)
				return false;

			if (MaxStops > -1 && flight.stops > MaxStops)
				return false;

			foreach (Segment seg in flight.segments)
			{
				if (seg.numberOfStops > MaxTransitStops)
					return false;

				if (BannedCarriers.Any(i => (i == seg.operatingCarrier.code || i == seg.marketingCarrier.code)))
					return false;
			}

			return true;
		}

		private void AddCode(FlysasLib.KeyValuePair pair)
		{
			if (TranslateCodes)
				codes.TryAdd(pair.code, pair.name);
		}

		private void PrintFlights(IEnumerable<FlightBaseClass> flights)
		{
			string dateFormat = "dd/MM HH:mm";

			StringBuilder sb = new StringBuilder();
			foreach (FlightBaseClass flight in flights)
			{
				var timeDelta = (flight.endTimeInLocal - flight.startTimeInLocal);

				sb.Clear();
				sb.Append(flight.startTimeInLocal.ToString(dateFormat, CultureInfo.InvariantCulture)).Append(" -> ").Append(flight.endTimeInLocal.ToString(dateFormat, CultureInfo.InvariantCulture)).Append(" | ");
				foreach (Segment segment in flight.segments)
				{
					sb.Append(segment.departureAirport.code).Append(" (").Append(segment.marketingCarrier.code);
					AddCode(segment.departureAirport);
					AddCode(segment.marketingCarrier);
					if (segment.marketingCarrier.code != segment.operatingCarrier.code)
					{
						sb.Append("[").Append(segment.operatingCarrier.code).Append("]");
						AddCode(segment.operatingCarrier);
					}
					sb.Append(") -> ").Append(segment.arrivalAirport.code).Append(" ");
					AddCode(segment.arrivalAirport);
				}

				sb.Append("TIME ").AppendFormat("{0:g}", timeDelta);

				txtOut.WriteLine(sb.ToString());
			}
		}

		private async Task<Result> Search(string to, int offsetDays)
		{
			SASQuery query = new SASQuery
			{
				Mode = "STAR",
				InDate = InStart.AddDays(offsetDays),
				OutDate = OutStart.AddDays(offsetDays),
				From = From,
				To = to
			};

			SearchResult result = null;
			try
			{
				result = await client.SearchAsync(query);
			}
			catch
			{
				Console.Error.WriteLine("Error in query");
			}
			if (result != null)
			{
				if (result.errors != null && result.errors.Any())
				{
					Console.Error.WriteLine("flysas.com says: " + result.errors.First().errorMessage);
				}

				{
					IEnumerable<FlightBaseClass> validOutbound = new List<FlightBaseClass>();
					IEnumerable<FlightBaseClass> validInbound = new List<FlightBaseClass>();
					if(result.outboundFlights != null)
						validOutbound = result.outboundFlights.Where(this.FilterFlight);
					if(result.inboundFlights != null)
						validInbound = result.inboundFlights.Where(this.FilterFlight);

					return new Result
					{
						OutBound = validOutbound,
						InBound = validInbound
					};
				}
			}
			return new Result
			{
				OutBound = new List<FlightBaseClass>(),
				InBound = new List<FlightBaseClass>()
			};
		}
	}
}
