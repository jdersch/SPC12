using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPC12
{
    internal class Helpers
    {
        public static string ToOctal(ushort value)
        {
            return ToOctal(value, 4);
        }

        public static string ToOctal(ushort value, int digits)
        {
            string octalString = Convert.ToString(value, 8);
            return new String('0', digits - octalString.Length) + octalString;
        }

        public static ushort GetUshortForOctalString(string octal)
        {
            ushort value = Convert.ToUInt16(octal, 8);
            return value;
        }
    }
}
