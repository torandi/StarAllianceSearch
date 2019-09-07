using System;
using System.Collections;
using System.Reflection;

namespace StarAllianceSearch
{
	[FlagsAttribute]
	enum ArgumentFlag
	{
		None = 0,
		Required = 1,
		List = 2,
		NoDefault = 4
	}

	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	class Argument : Attribute
	{

		public string Name { get; set; }
		public string Description { get; set; } = "";
		public ArgumentFlag Flags { get; set; }
		public Type PropertyType { get; set; }
		public object Object { get; set; }

		public String DefaultValue { get; set; } // only used for help message

		public Argument(string name, ArgumentFlag flags = ArgumentFlag.None)
		{
			Name = name;
			Flags = flags;
		}

		public void Parse(string argument, PropertyInfo property)
		{
			if (!Flags.HasFlag(ArgumentFlag.List))
			{
				property.SetValue(Object, Convert.ChangeType(argument.Trim(), PropertyType));
			}
			else
			{
				ParseList(argument, property);
			}
		}

		private void ParseList(string argument, PropertyInfo property)
		{
			IList list = (IList)property.GetValue(Object);
			string[] arguments = argument.Split(",");
			foreach (string a in arguments)
			{
				list.Add(Convert.ChangeType(a.Trim(), PropertyType));
			}
		}
	}
}
