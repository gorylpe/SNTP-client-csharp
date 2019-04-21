using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

// Leap indicator field values
public enum _LeapIndicator
{
    NoWarning, // 0 - No warning
    LastMinute61, // 1 - Last minute has 61 seconds
    LastMinute59, // 2 - Last minute has 59 seconds
    Alarm // 3 - Alarm condition (clock not synchronized)
}

//Mode field values
public enum _Mode
{
    SymmetricActive, // 1 - Symmetric active
    SymmetricPassive, // 2 - Symmetric pasive
    Client, // 3 - Client
    Server, // 4 - Server
    Broadcast, // 5 - Broadcast
    Unknown // 0, 6, 7 - Reserved
}

// Stratum field values
public enum _Stratum
{
    Unspecified, // 0 - unspecified or unavailable
    PrimaryReference, // 1 - primary reference (e.g. radio-clock)
    SecondaryReference, // 2-15 - secondary reference (via NTP or SNTP)
    Reserved // 16-255 - reserved
}

/// <summary>
/// NTPClient is a C# class designed to connect to time servers on the Internet.
/// The implementation of the protocol is based on the RFC 2030.
/// 
/// Public class members:
///
/// LeapIndicator - Warns of an impending leap second to be inserted/deleted in the last
/// minute of the current day. (See the _LeapIndicator enum)
/// 
/// VersionNumber - Version number of the protocol (3 or 4).
/// 
/// Mode - Returns mode. (See the _Mode enum)
/// 
/// Stratum - Stratum of the clock. (See the _Stratum enum)
/// 
/// PollInterval - Maximum interval between successive messages.
/// 
/// Precision - Precision of the clock.
/// 
/// RootDelay - Round trip time to the primary reference source.
/// 
/// RootDispersion - Nominal error relative to the primary reference source.
/// 
/// ReferenceID - Reference identifier (either a 4 character string or an IP address).
/// 
/// ReferenceTimestamp - The time at which the clock was last set or corrected.
/// 
/// OriginateTimestamp - The time at which the request departed the client for the server.
/// 
/// ReceiveTimestamp - The time at which the request arrived at the server.
/// 
/// Transmit Timestamp - The time at which the reply departed the server for client.
/// 
/// RoundTripDelay - The time between the departure of request and arrival of reply.
/// 
/// LocalClockOffset - The offset of the local clock relative to the primary reference
/// source.
/// 
/// Initialize - Sets up data structure and prepares for connection.
/// 
/// Connect - Connects to the time server and populates the data structure.
///	It can also set the system time.
/// 
/// IsResponseValid - Returns true if received data is valid and if comes from
/// a NTP-compliant time server.
/// 
/// ToString - Returns a string representation of the object.
/// 
/// -----------------------------------------------------------------------------
/// Structure of the standard NTP header (as described in RFC 2030)
///                       1                   2                   3
///   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |LI | VN  |Mode |    Stratum    |     Poll      |   Precision   |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                          Root Delay                           |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                       Root Dispersion                         |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                     Reference Identifier                      |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                   Reference Timestamp (64)                    |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                   Originate Timestamp (64)                    |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                    Receive Timestamp (64)                     |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                    Transmit Timestamp (64)                    |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                 Key Identifier (optional) (32)                |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                                                               |
///  |                 Message Digest (optional) (128)               |
///  |                                                               |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// 
/// -----------------------------------------------------------------------------
/// 
/// NTP Timestamp Format (as described in RFC 2030)
///                         1                   2                   3
///     0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
/// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// |                           Seconds                             |
/// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// |                  Seconds Fraction (0-padded)                  |
/// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// 
/// </summary>
public class NTPClient
{
    private string TimeServer;
    private int Port;
    private int Timeout;

    // NTP Data Structure Length
    private const byte NTPDataLength = 48;

    // NTP Data Structure (as described in RFC 2030)
    public byte[] NTPData = new byte[NTPDataLength];

    // Offset constants for timestamps in the data structure
    private const byte offReferenceID = 12;
    private const byte offReferenceTimestamp = 16;
    private const byte offOriginateTimestamp = 24;
    private const byte offReceiveTimestamp = 32;
    public const byte offTransmitTimestamp = 40;

    private readonly DateTime ntpStartDate = new DateTime(1900, 1, 1);

    // Leap Indicator
    public _LeapIndicator LeapIndicator => (_LeapIndicator) (NTPData[0] >> 6);

    // Version Number
    public byte VersionNumber
    {
        get
        {
            // Isolate bits 3 - 5
            byte val = (byte) ((NTPData[0] & 0x38) >> 3);
            return val;
        }
    }

    // Mode
    public _Mode Mode
    {
        get
        {
            // Isolate bits 0 - 3
            byte val = (byte) (NTPData[0] & 0x7);
            switch (val)
            {
                case 0: goto default;
                case 6: goto default;
                case 7: goto default;
                default:
                    return _Mode.Unknown;
                case 1:
                    return _Mode.SymmetricActive;
                case 2:
                    return _Mode.SymmetricPassive;
                case 3:
                    return _Mode.Client;
                case 4:
                    return _Mode.Server;
                case 5:
                    return _Mode.Broadcast;
            }
        }
    }

    // Stratum
    public _Stratum Stratum
    {
        get
        {
            byte val = (byte) NTPData[1];
            if (val == 0) return _Stratum.Unspecified;
            else if (val == 1) return _Stratum.PrimaryReference;
            else if (val <= 15) return _Stratum.SecondaryReference;
            else
                return _Stratum.Reserved;
        }
    }

    // Poll Interval
    public uint PollInterval
    {
        get { return (uint) Math.Round(Math.Pow(2, NTPData[2])); }
    }

    // Precision (in milliseconds)
    public double Precision
    {
        get { return (1000 * Math.Pow(2, NTPData[3])); }
    }

    // Root Delay (in milliseconds)
    public double RootDelay
    {
        get
        {
            int temp = 0;
            temp = 256 * (256 * (256 * NTPData[4] + NTPData[5]) + NTPData[6]) + NTPData[7];
            return 1000 * (((double) temp) / 0x10000);
        }
    }

    // Root Dispersion (in milliseconds)
    public double RootDispersion
    {
        get
        {
            int temp = 0;
            temp = 256 * (256 * (256 * NTPData[8] + NTPData[9]) + NTPData[10]) + NTPData[11];
            return 1000 * (((double) temp) / 0x10000);
        }
    }

    // Reference Identifier
    public string ReferenceID
    {
        get
        {
            string val = "";
            switch (Stratum)
            {
                case _Stratum.Unspecified:
                    goto case _Stratum.PrimaryReference;
                case _Stratum.PrimaryReference:
                    val += (char) NTPData[offReferenceID + 0];
                    val += (char) NTPData[offReferenceID + 1];
                    val += (char) NTPData[offReferenceID + 2];
                    val += (char) NTPData[offReferenceID + 3];
                    break;
                case _Stratum.SecondaryReference:
                    switch (VersionNumber)
                    {
                        case 3: // Version 3, Reference ID is an IPv4 address
                            string Address = NTPData[offReferenceID + 0].ToString() + "." +
                                             NTPData[offReferenceID + 1].ToString() + "." +
                                             NTPData[offReferenceID + 2].ToString() + "." +
                                             NTPData[offReferenceID + 3].ToString();
                            try
                            {
                                IPHostEntry Host = Dns.GetHostByAddress(Address);
                                val = Host.HostName + " (" + Address + ")";
                            }
                            catch (Exception)
                            {
                                val = "N/A";
                            }

                            break;
                        default:
                            val = "N/A";
                            break;
                    }

                    break;
            }

            return val;
        }
    }

    public DateTime ReferenceTimestamp => TimeZone.CurrentTimeZone.ToLocalTime(TimeStampToDateTime(offReferenceTimestamp));
    public DateTime OriginateTimestamp => TimeStampToDateTime(offOriginateTimestamp);
    public DateTime ReceiveTimestamp => TimeZone.CurrentTimeZone.ToLocalTime(TimeStampToDateTime(offReceiveTimestamp));
    public DateTime TransmitTimestamp
    {
        get => TimeZone.CurrentTimeZone.ToLocalTime(TimeStampToDateTime(offTransmitTimestamp));
        set => DateTimeToTimeStamp(value, offTransmitTimestamp);
    }
    public DateTime ReceptionTimestamp;

    public int RoundTripDelay => (int) ((ReceiveTimestamp - OriginateTimestamp) + (ReceptionTimestamp - TransmitTimestamp)).Ticks;

    public int LocalClockOffset => (int) ((ReceiveTimestamp - OriginateTimestamp) - (ReceptionTimestamp - TransmitTimestamp)).Ticks / 2;

    // Compute date using NTP timestamp
    private DateTime TimeStampToDateTime(int offset)
    {
        ulong seconds = 0, fraction = 0;
        for (var i = 0; i <= 3; i++)
            seconds = seconds * 256 + NTPData[offset + i];
        for (var i = 4; i <= 7; i++)
            fraction = fraction * 256 + NTPData[offset + i];
        
        var ticks = seconds * TimeSpan.TicksPerSecond + ((fraction * TimeSpan.TicksPerSecond) >> 32);

        return ntpStartDate.AddTicks((long) ticks);
    }

    private void DateTimeToTimeStamp(DateTime date, int offset)
    {
        var ticks = (date - ntpStartDate).Ticks;

        var seconds = ticks / TimeSpan.TicksPerSecond;
        var fraction = ticks % TimeSpan.TicksPerSecond * 0x100000000L / TimeSpan.TicksPerSecond;

        var temp = seconds;
        for (var i = 3; i >= 0; i--)
        {
            NTPData[offset + i] = (byte) (temp % 256);
            temp = temp / 256;
        }

        temp = fraction;
        for (var i = 7; i >= 4; i--)
        {
            NTPData[offset + i] = (byte) (temp % 256);
            temp = temp / 256;
        }
    }

    // Initialize the NTPClient data
    private void Initialize()
    {
        // Set version number to 4 and Mode to 3 (client)
        NTPData[0] = 0x1B;
        // Initialize all other fields with 0
        for (int i = 1; i < 48; i++)
        {
            NTPData[i] = 0;
        }

        // Initialize the transmit timestamp
        TransmitTimestamp = DateTime.Now;
    }

    public NTPClient(string host, int port = 123, int timeout = 1000)
    {
        TimeServer = host;
        Port = port;
        Timeout = timeout;
    }

    Stopwatch stopwatch = new Stopwatch();

    // Connect to the time server and update system time
    public void Connect(bool UpdateSystemTime)
    {
        try
        {
            // Resolve server address
            IPHostEntry hostadd = Dns.Resolve(TimeServer);
            IPEndPoint EPhost = new IPEndPoint(hostadd.AddressList[0], Port);

            //Connect the time server
            UdpClient timeSocket = new UdpClient();
            timeSocket.Client.SendTimeout = Timeout;
            timeSocket.Client.ReceiveTimeout = Timeout;

            timeSocket.Connect(EPhost);
            Initialize();
            timeSocket.Send(NTPData, NTPData.Length);
            NTPData = timeSocket.Receive(ref EPhost);
            if (!IsResponseValid())
            {
                throw new Exception("Invalid response from " + TimeServer);
            }
            ReceptionTimestamp = DateTime.Now;
        }
        catch (SocketException e)
        {
            throw new Exception(e.Message);
        }
    }

    // Check if the response from server is valid
    public bool IsResponseValid()
    {
        if (NTPData.Length < NTPDataLength || Mode != _Mode.Server)
            return false;

        return true;
    }
}