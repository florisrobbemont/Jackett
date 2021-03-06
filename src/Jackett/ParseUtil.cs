﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jackett
{
    public static class ParseUtil
    {
        public static float CoerceFloat(string str)
        {
            return float.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public static int CoerceInt(string str)
        {
            return int.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public static long CoerceLong(string str)
        {
            return long.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
        }


        public static bool TryCoerceFloat(string str, out float result)
        {
            return float.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryCoerceInt(string str, out int result)
        {
            return int.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryCoerceLong(string str, out long result)
        {
            return long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

    }
}
