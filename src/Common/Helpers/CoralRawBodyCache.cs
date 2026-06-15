#region Using Directives

using System.Runtime.CompilerServices;
using Microsoft.ServiceBus.Messaging;

#endregion

namespace ServiceBusExplorer.Helpers
{
    /// <summary>
    /// Coral: stores the raw, pre-decompression body bytes of received messages so the
    /// hex viewer can show what came off the wire (before the ZIP inspector unzips it).
    /// Keyed weakly by message identity, so cached bytes are collected together with the
    /// message they belong to.
    /// </summary>
    public static class CoralRawBodyCache
    {
        static readonly ConditionalWeakTable<BrokeredMessage, byte[]> cache =
            new ConditionalWeakTable<BrokeredMessage, byte[]>();

        public static void Store(BrokeredMessage message, byte[] rawBody)
        {
            if (message == null || rawBody == null)
            {
                return;
            }
            cache.Remove(message);
            cache.Add(message, rawBody);
        }

        public static bool TryGet(BrokeredMessage message, out byte[] rawBody)
        {
            rawBody = null;
            return message != null && cache.TryGetValue(message, out rawBody);
        }
    }
}
