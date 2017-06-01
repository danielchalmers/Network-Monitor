using System;

namespace Network_Monitor
{
    public static class ByteHelper
    {
        // https://stackoverflow.com/a/4975942/5889966
        public static string BytesToString(long byteCount)
        {
            // Longs run out around EB.
            string[] suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
            {
                return "0" + suffix[0];
            }
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return Math.Sign(byteCount) * num + suffix[place];
        }
    }
}