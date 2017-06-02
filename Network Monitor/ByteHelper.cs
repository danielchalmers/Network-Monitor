namespace Network_Monitor
{
    public static class ByteHelper
    {
        // Adapted from https://stackoverflow.com/a/11124118.
        public static string BytesToReadableString(long bytes)
        {
            var absoluteBytes = (bytes < 0 ? -bytes : bytes);

            // Determine the suffix and readable value.
            string suffix;
            double readable;
            if (absoluteBytes >= 0x1000000000000000) // Exabyte.
            {
                suffix = "E";
                readable = (bytes >> 50);
            }
            else if (absoluteBytes >= 0x4000000000000) // Petabyte.
            {
                suffix = "P";
                readable = (bytes >> 40);
            }
            else if (absoluteBytes >= 0x10000000000) // Terabyte.
            {
                suffix = "T";
                readable = (bytes >> 30);
            }
            else if (absoluteBytes >= 0x40000000) // Gigabyte.
            {
                suffix = "G";
                readable = (bytes >> 20);
            }
            else if (absoluteBytes >= 0x100000) // Megabyte.
            {
                suffix = "M";
                readable = (bytes >> 10);
            }
            else if (absoluteBytes >= 0x400) // Kilobyte.
            {
                suffix = "K";
                readable = bytes;
            }
            else
            {
                return bytes.ToString("0B"); // Byte.
            }

            // Divide by 1024 to get fractional value.
            readable = (readable / 1024);

            return readable.ToString("0") + suffix;
        }
    }
}