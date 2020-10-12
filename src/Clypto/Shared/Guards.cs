using System;

namespace Clypto.Shared
{
    public static class Guards
    {
		public static void GuardNotNull(this string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				throw new ArgumentNullException(nameof(input));
			}
		}

		public static void GuardNotNull(this object input)
		{
			switch (input)
			{
				case string inputString when string.IsNullOrWhiteSpace(inputString):
					throw new ArgumentNullException(nameof(input));
				case null:
					throw new ArgumentNullException(nameof(input));
			}
		}
	}
}
