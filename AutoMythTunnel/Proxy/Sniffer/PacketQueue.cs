using MineSharp.Core.Common;
using MineSharp.Core.Serialization;

namespace AutoMythTunnel.Proxy.Sniffer;

public class PacketQueue
{
    List<PacketBuffer> queue = new List<PacketBuffer>();
    
    public void Enqueue(PacketBuffer packet)
    {
        lock (queue)
        {
            queue.Add(packet);
        }
    }
    
    public PacketBuffer? Dequeue()
    {
        lock (queue)
        {
            if (queue.Count == 0)
            {
                return null;
            }
            PacketBuffer packet = queue[0];
            queue.RemoveAt(0);
            return packet;
        }
    }
    
    public void Clear()
    {
        lock (queue)
        {
            queue.Clear();
        }
    }
    
    public int Count()
    {
        lock (queue)
        {
            return queue.Count;
        }
    }
}