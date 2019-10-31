using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BitcoinNet.Protocol
{
	[AttributeUsage(AttributeTargets.Class)]
	public class PayloadAttribute : Attribute
	{
		private static readonly Dictionary<string, Type> NameToType;
		private static readonly Dictionary<Type, string> TypeToName;

		static PayloadAttribute()
		{
			NameToType = new Dictionary<string, Type>();
			TypeToName = new Dictionary<Type, string>();
			foreach (var pair in
				GetLoadableTypes(typeof(PayloadAttribute).GetTypeInfo().Assembly)
					.Where(t => t.Namespace == typeof(PayloadAttribute).Namespace)
					.Where(t => t.IsDefined(typeof(PayloadAttribute), true))
					.Select(t =>
						new
						{
							Attr = t.GetCustomAttributes(typeof(PayloadAttribute), true).OfType<PayloadAttribute>()
								.First(),
							Type = t
						}))
			{
				NameToType.Add(pair.Attr.Name, pair.Type.AsType());
				TypeToName.Add(pair.Type.AsType(), pair.Attr.Name);
			}
		}

		public PayloadAttribute(string commandName)
		{
			Name = commandName;
		}

		public string Name { get; set; }

		private static IEnumerable<TypeInfo> GetLoadableTypes(Assembly assembly)
		{
			try
			{
				return assembly.DefinedTypes;
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null).Select(t => t.GetTypeInfo());
			}
		}

		public static string GetCommandName<T>()
		{
			return GetCommandName(typeof(T));
		}

		public static Type GetCommandType(string commandName)
		{
			Type result;
			if (!NameToType.TryGetValue(commandName, out result))
			{
				return typeof(UnknowPayload);
			}

			return result;
		}

		internal static string GetCommandName(Type type)
		{
			if (!TypeToName.TryGetValue(type, out var result))
			{
				throw new ArgumentException(type.FullName + " is not a payload");
			}

			return result;
		}
	}
}