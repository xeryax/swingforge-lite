#region License
/*
Copyright © Joan Charmant 2011.
jcharmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Kinovea.Services
{
    public static class LanguageManager
    {
        private const string DefaultCulture = "en";

        public static Dictionary<string, string> Languages
        {
            get { return new Dictionary<string, string> { { "en", "English" } }; }
        }

        public static bool IsSupportedCulture(CultureInfo ci)
        {
            return string.Equals(ci.Name, DefaultCulture, StringComparison.OrdinalIgnoreCase)
                || (!ci.IsNeutralCulture && string.Equals(ci.Parent.Name, DefaultCulture, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetCurrentCultureName()
        {
            return DefaultCulture;
        }

        public static Dictionary<string, string> GetEnabledLanguages(bool enableAllLanguages)
        {
            return new Dictionary<string, string> { { "en", "English" } };
        }
    }
}
