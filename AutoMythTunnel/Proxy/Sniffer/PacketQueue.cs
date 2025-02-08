using MineSharp.Core.Common;
using MineSharp.Core.Serialization;
using System.Threading;

namespace AutoMythTunnel.Proxy.Sniffer;

public class PacketQueue
{
    private readonly List<PacketBuffer> queue = new();
    private readonly object lockObject = new();

    public void Enqueue(PacketBuffer packet)
    {
        lock (lockObject)
        {
            queue.Add(packet);
            Monitor.Pulse(lockObject); // Notify waiting threads that a packet is available
        }
    }

    public PacketBuffer? Dequeue()
    {
        lock (lockObject)
        {
            while (queue.Count == 0)
            {
                Monitor.Wait(lockObject); // Wait until a packet is available
            }
            PacketBuffer packet = queue[0];
            queue.RemoveAt(0);
            return packet;
        }
    }

    public void Clear()
    {
        lock (lockObject)
        {
            queue.Clear();
            Monitor.PulseAll(lockObject); // Notify all waiting threads
        }
    }

    public int Count()
    {
        lock (lockObject)
        {
            return queue.Count;
        }
    }
}