using System;
using System.Collections.Generic;
using System.Globalization;

namespace TestDrive
{
    public class ObdParser
    {
        private static int ParseString(string str, int bytes)
        {
            return int.Parse(str.Substring(4, bytes * 2), NumberStyles.HexNumber);
        }

        public static string ParseObd01Msg(string input)
        {
            string str = input.Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace(">", "").Trim();
            if (!str.StartsWith("41") || str.Length < 6)
                return "-255";
            string pid = str.Substring(2, 2);
            int result;
            switch (pid)
            {
                case "04": //EngineLoad
                    return (ParseString(str, 1) * 100 / 255).ToString();
                case "06": //ShortTermFuelBank1
                    return ((ParseString(str, 1) - 128) * 100 / 128).ToString();
                case "07": //LongTermFuelBank1
                    return ((ParseString(str, 1) - 128) * 100 / 128).ToString();
                case "0C": //RPM
                    result = (ParseString(str, 2) / 4);
                    if (result < 0 || result > 16383)
                        result = -255;
                    return result.ToString();
                case "0D": //Speed
                    result = ParseString(str, 1);
                    if (result < 0 || result > 255)
                        result = -255;
                    return result.ToString();
                case "0F": //InsideTemperature
                    return (ParseString(str, 1) - 40).ToString();
                case "10": //MAF air flow rate
                    result = (ParseString(str, 2) / 100);
                    if (result < 0 || result > 655)
                        result = -255;
                    return result.ToString();
                case "11": //Throttle position
                    return (ParseString(str, 1) * 100 / 255).ToString();
                case "1F": //Runtime 
                    return ParseString(str, 2).ToString();
                case "21": //DistancewithML  
                    return ParseString(str, 2).ToString();
                case "2C": //Commanded EGR
                    return (ParseString(str, 1) * 100 / 255).ToString();
                case "2D": //EGR Error
                    return ((ParseString(str, 1) - 128) * 100 / 128).ToString();
                case "33": //BarometricPressure
                    return ParseString(str, 1).ToString();
                case "45": //Relative throttle position
                    return (ParseString(str, 1) * 100 / 255).ToString();
                case "46": //OutsideTemperature
                    return (ParseString(str, 1) - 40).ToString();
                case "5E": //EngineFuelRate
                    result = (ParseString(str, 2) / 20);
                    if (result < 0 || result > 3212)
                        result = -255;
                    return result.ToString();
            }
            return "ERROR";
        }

        public static string ParseVINMsg(string result) // VIN
        {
            try
            {
                if (result.Contains("STOPPED")) return result;

                if (result.Contains("NO DATA") || result.Contains("ERROR")) return result;

                string temp = result.Replace("\r\n", "");

                int index = temp.IndexOf("0: ");

                if (index < 0) return ParseLongVIN(result);

                temp = temp.Substring(index);
                temp = temp.Replace("0: ", string.Empty);
                temp = temp.Replace("1: ", string.Empty);
                temp = temp.Replace("2: ", string.Empty);
                temp = temp.Trim();

                if (temp.Length > 59) temp = temp.Substring(0, 59);

                var items = temp.Split(' ');

                string ret = string.Empty;

                foreach (var s in items)
                {
                    ret += (char)int.Parse(s, NumberStyles.HexNumber);
                }

                if (ret.Length > 17)
                {
                    ret = ret.Substring(ret.Length - 17);
                }

                // mask last 7 digits
                ret = ret.Substring(0, 10);
                ret += "0000000";

                return ret;
            }
            catch (Exception exp)
            {
                return exp.Message;
            }
        }

        public static string ParseLongVIN(string result) //VIN
        {
            if (result.Contains("STOPPED")) return result;

            if (result.Contains("NO DATA") || result.Contains("ERROR")) return result;

            var items = result.Replace("\r\n", "").Split(' ');

            if (items.Length < 36) return "ERROR";

            if (items[0].Trim() != "49") return "ERROR";

            string ret = string.Empty;
            int tint;
            char tchar;

            switch (items[1])
            {
                case "02": // VIN
                    tint = int.Parse(items[6], NumberStyles.HexNumber);
                    tchar = (char)tint;
                    ret += tchar.ToString();

                    for (int i = 10; i < 14; i++)
                    {
                        tint = int.Parse(items[i], NumberStyles.HexNumber);
                        tchar = (char)tint;
                        ret += tchar.ToString();
                    }

                    for (int i = 17; i < 21; i++)
                    {
                        tint = int.Parse(items[i], NumberStyles.HexNumber);
                        tchar = (char)tint;
                        ret += tchar.ToString();
                    }

                    for (int i = 24; i < 28; i++)
                    {
                        tint = int.Parse(items[i], NumberStyles.HexNumber);
                        tchar = (char)tint;
                        ret += tchar.ToString();
                    }

                    for (int i = 31; i < 35; i++)
                    {
                        tint = int.Parse(items[i], NumberStyles.HexNumber);
                        tchar = (char)tint;
                        ret += tchar.ToString();
                    }

                    // mask last 7 digits
                    ret = ret.Substring(0, 10);
                    ret += "0000000";

                    return ret;
            }

            return "ERROR";
        }

        public static Dictionary<string, string> GetParameterIds()
        {
            var ret = new Dictionary<string, string> //<cmd, key>
            {
                {"0110", "fr"},     // EngineLoad
                {"0104", "el"},     // ShortTermFuelBank1
                {"0106", "stfb"},   // LongTermFuelBank1
                {"0107", "ltfb"},   // EngineRPM
                {"010C", "rpm"},    // Speed
                {"010D", "spd"},    // MAFFlowRate
                {"0111", "tp"},     // ThrottlePosition
                {"011F", "rt"},     // Runtime
                {"0121", "dis"},    // DistancewithMIL
                {"0145", "rtp"},    // RelativeThrottlePosition
                {"0146", "ot"},     // OutsideTemperature
                {"015E", "efr"}     // EngineFuelRate
            };

            return ret;
        }
    }
}