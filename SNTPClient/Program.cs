using System;
using System.Collections.Generic;
using System.Linq;

internal class Program
{
    //private static readonly string TimeServer = "tick.usno.navy.mil";
    //private static readonly string TimeServer = "0.pl.pool.ntp.org";
    private static readonly string TimeServer = "localhost";
    
    public static int Main(string[] args)
    {
        NTPClient client;
        try {
            client = new NTPClient(TimeServer, 10000, 2000);
        }
        catch(Exception e)
        {
            Console.WriteLine("ERROR: {0}", e.Message);
            return -1;
        }

        var offsets = new List<long>();
        
        for (int i = 0; i < 10; ++i)
        {
            try
            {
                client.Connect(false);
                if (i != 0) offsets.Add(client.LocalClockOffset);
                Console.WriteLine(i + " " + client.LocalClockOffset);
            }
            catch(Exception e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
            }

            if (i % 10 == 0 && offsets.Count > 0)
            {
                Console.WriteLine("Avg offset {0}ms from {1} records", 1.0 * offsets.Average() / TimeSpan.TicksPerMillisecond, offsets.Count);
            }
        }

        return 0;
    }
}
