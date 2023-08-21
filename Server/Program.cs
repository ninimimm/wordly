using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

class Server
{
    private static Dictionary<string, (int, bool, bool)> allRooms = new ();
    private static Mutex mutex = new Mutex();
    private static int index;

    static async Task Main()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var address in host.AddressList)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                Console.WriteLine($"Local IP Address: {address}");
            }
        }

        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();

        Console.WriteLine("Server started. Waiting for connections...");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            Console.WriteLine("Client connected");

            _ = Task.Run(() => ProcessClientAsync(client)); // Start a new task to handle the client
        }
    }

    static async Task ProcessClientAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        var buffer = new byte[10000];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        var clientRoomList = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        var clientRooms = clientRoomList
            .Split(',')
            .Select(item => item.Split(':'))
            .ToDictionary(parts => parts[0], parts => (int.Parse(parts[1].Split(".")[0]), bool.Parse(parts[1].Split(".")[1]), bool.Parse(parts[1].Split(".")[2])));
        
        mutex.WaitOne();
        foreach (var room in clientRooms)
        {
            if (allRooms.ContainsKey(room.Key))
            {
                if (allRooms[room.Key].Item2)
                {
                    if (!allRooms[room.Key].Item3)
                    {
                        allRooms[room.Key] = (allRooms[room.Key].Item1, allRooms[room.Key].Item2, true);
                        continue;
                    }
                    if (room.Value.Item1 == 0)
                        allRooms[room.Key] = room.Value;
                    else if (room.Value.Item1 >= allRooms[room.Key].Item1)
                        allRooms[room.Key] = room.Value;
                }
                else
                {
                    if (room.Value.Item1 < allRooms[room.Key].Item1)
                        allRooms[room.Key] = room.Value;
                }
            }
            else allRooms[room.Key] = room.Value;

        }
        mutex.ReleaseMutex();  // Освобождаем мьютекс
        
        Console.WriteLine($"new rooms{index}");
        index++;
        foreach (var room in allRooms) 
            Console.WriteLine(room);

        var combinedRoomList = string.Join(",", allRooms.Select(kv => $"{kv.Key}:{kv.Value.Item1}.{kv.Value.Item2}.{kv.Value.Item3}"));
        var combinedBuffer = Encoding.ASCII.GetBytes(combinedRoomList);
        await stream.WriteAsync(combinedBuffer, 0, combinedBuffer.Length);

        _ = Task.Run(() => ProcessClientAsync(client));
    }
}