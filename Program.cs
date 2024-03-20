using System.Net.NetworkInformation;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

[DllImport("iphlpapi.dll", ExactSpelling = true)]
static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

HttpClient _client = new();
async Task<string> GetNameByDictionary(string macAddress)
{
    if (NetworkInterface.GetIsNetworkAvailable())
    {
        var response = await _client.GetAsync($"https://api.macvendors.com/{macAddress}");

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
    }
    return "Неизвестно";
}
IPAddress GetSubnetMask(IPAddress address)
{
    foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
    {
        foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
        {
            if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                if (address.Equals(unicastIPAddressInformation.Address))
                {
                    return unicastIPAddressInformation.IPv4Mask;
                }
            }
        }
    }

    throw new ArgumentException($"Can't find subnetmask for IP address '{address}'");
}

uint IpAddressToUInt(IPAddress ip)
{
    byte[] addressBytes = ip.GetAddressBytes();
    Array.Reverse(addressBytes);
    return BitConverter.ToUInt32(addressBytes, 0);
}

string GetDeviceName(string ipAddress)
{
    try
    {
        IPHostEntry hostEntry = Dns.GetHostEntry(ipAddress);
        return hostEntry.HostName;
    }
    catch (Exception)
    {
        return "Неизвестно";
    }
}
string GetInfoAboutIP(IPAddress srcIP, IPAddress destIP)
{
    byte[] macAddr = new byte[6];
    uint macAddrLen = (uint)macAddr.Length;
    int dest = BitConverter.ToInt32(IPAddress.Parse(destIP.ToString()).GetAddressBytes(), 0);
    if (SendARP(dest, /*(int)IpAddressToUInt(srcIP)*/ 0, macAddr, ref macAddrLen) != 0)
    {
        var macAddrStr = string.Join(":", macAddr.Select(x => x.ToString("X2")));
        Console.WriteLine($"При отправке ARP-запроса по IP-адресу {destIP.ToString()} возникла ошибка.");
        return "";
    }
    else
    { //E0: DC: FF: 1F:0E:32
        var macAddrStr = string.Join(":", macAddr.Select(x => x.ToString("X2")));
        string result = "-------------------------------------------\n" +
             "IP: " + destIP.ToString() + "\nMAC: " + macAddrStr + "\nПроизводитель: " + GetNameByDictionary(macAddrStr).Result +
             "\nИмя: " + GetDeviceName(destIP.ToString()) +
             "\n-------------------------------------------\n";
        return result;
    }
}


Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

var adapters = NetworkInterface.GetAllNetworkInterfaces();
Console.WriteLine($"Обнаружено {adapters.Length} устройств");
foreach (NetworkInterface adapter in adapters)
{
    Console.WriteLine("=====================================================================");
    Console.WriteLine();
    Console.WriteLine($"ID устройства: ------------- {adapter.Id}");
    Console.WriteLine($"Имя устройства: ------------ {adapter.Name}");
    Console.WriteLine($"Описание: ------------------ {adapter.Description}");
    Console.WriteLine($"Тип интерфейса: ------------ {adapter.NetworkInterfaceType}");
    Console.WriteLine($"Физический адрес: ---------- {adapter.GetPhysicalAddress()}");
    Console.WriteLine($"Статус: -------------------- {adapter.OperationalStatus}");
    Console.WriteLine($"Скорость: ------------------ {adapter.Speed}");
    Console.WriteLine("=====================================================================");
}

string host = Dns.GetHostName();
Console.WriteLine($"Имя хоста: {host}");
IPAddress[] ipAddresses = Dns.GetHostAddresses(host);

foreach (IPAddress ipAddress in ipAddresses)
{
    if ((ipAddress.AddressFamily == AddressFamily.InterNetwork) && (ipAddress.ToString() != "127.0.0.1"))
    {
        IPAddress subnetMask = GetSubnetMask(ipAddress);
        Console.WriteLine($"IP-адрес: {ipAddress}");
        Console.WriteLine($"Маска подсети: {subnetMask}");
        
        uint ipAddressNumber = IpAddressToUInt(ipAddress);
        uint maskNumber = IpAddressToUInt(subnetMask);

        Console.WriteLine($"Кол-во возможных узлов в подсети: {~maskNumber}");

        for (byte i = 1; i < ~maskNumber; i++)
        {
            var destIP = IpAddressToUInt(new IPAddress((IpAddressToUInt(ipAddress) & IpAddressToUInt(subnetMask)) + i));
            Console.WriteLine(GetInfoAboutIP(ipAddress, new IPAddress(destIP)));
        }

    }

}

