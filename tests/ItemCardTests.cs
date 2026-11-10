using System;
using System.IO;
using DataFiles;
using Game.PlayerRelated;

internal static class ItemCardTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (DirectoryInfo cur = dir.Parent; cur != null; cur = cur.Parent)
        {
            string dataDir = Path.Combine(cur.FullName, "Data");
            if (Directory.Exists(dataDir))
            {
                return cur.FullName;
            }
        }
        if (Directory.Exists(Path.Combine(dir.FullName, "Data")))
        {
            return dir.FullName;
        }
        throw new DirectoryNotFoundException("Could not find repository root containing Data folder by walking up from: " + AppDomain.CurrentDomain.BaseDirectory);
    }

    public static int Main(string[] args)
    {
        try
        {
            string repoRoot = FindRepoRoot();
            string itemDatPath = Path.Combine(repoRoot, "Data", "Item.dat");
            Check(File.Exists(itemDatPath), "Data/Item.dat does not exist at: " + itemDatPath);

            var itemData = new PhxItemDat();
            itemData.onDebug = delegate(object obj) { };
            itemData.Load(itemDatPath).Wait();

            string itemCardsJsonPath = Path.Combine(repoRoot, "Data", "item_cards.json");
            Check(File.Exists(itemCardsJsonPath), "Data/item_cards.json does not exist at: " + itemCardsJsonPath);

            ItemCardManager.Load(itemData, itemCardsJsonPath);

            // Checks:
            // 1. At least 200 cards load
            int loadedCount = ItemCardManager.Count;
            Console.WriteLine(string.Format("Loaded cards count: {0}", loadedCount));
            Check(loadedCount >= 200, string.Format("Expected at least 200 cards to load, but got {0}", loadedCount));

            // 2. ItemCardManager.IsCard(50001) is true and IsCard(1) is false
            Check(ItemCardManager.IsCard(50001), "ItemCardManager.IsCard(50001) should be true");
            Check(!ItemCardManager.IsCard(1), "ItemCardManager.IsCard(1) should be false");

            // 3. BuildIndex() returns bytes where, after the 4 byte packet header:
            //    bytes are 23, 108, 1, then a little-endian ushort count equal to the number of loaded cards,
            //    and the total length is 4 + 5 + 3 * count, with card ids ascending
            byte[] indexPacket = ItemCardManager.BuildIndex();
            Check(indexPacket != null, "BuildIndex() returned null");

            int expectedIndexLength = 4 + 5 + 3 * loadedCount;
            Check(indexPacket.Length == expectedIndexLength,
                string.Format("BuildIndex() length mismatch: expected {0}, got {1}", expectedIndexLength, indexPacket.Length));

            // Header check: packet length field at offset 2 equals total length minus 4
            ushort indexPayloadLen = BitConverter.ToUInt16(indexPacket, 2);
            Check(indexPayloadLen == indexPacket.Length - 4,
                string.Format("BuildIndex() packet length field at offset 2 ({0}) != total length minus 4 ({1})", indexPayloadLen, indexPacket.Length - 4));

            Check(indexPacket[4] == 23, string.Format("BuildIndex() offset 4 expected 23, got {0}", indexPacket[4]));
            Check(indexPacket[5] == 108, string.Format("BuildIndex() offset 5 expected 108, got {0}", indexPacket[5]));
            Check(indexPacket[6] == 1, string.Format("BuildIndex() offset 6 expected 1, got {0}", indexPacket[6]));

            ushort indexCount = BitConverter.ToUInt16(indexPacket, 7);
            Check(indexCount == loadedCount,
                string.Format("BuildIndex() count at offset 7 ({0}) does not match loaded cards ({1})", indexCount, loadedCount));

            ushort previousCardId = 0;
            for (int i = 0; i < loadedCount; i++)
            {
                int offset = 9 + (i * 3);
                ushort cardId = BitConverter.ToUInt16(indexPacket, offset);
                byte flag = indexPacket[offset + 2];
                Check(flag == 1, string.Format("Card entry at index {0} flag expected 1, got {1}", i, flag));
                if (i > 0)
                {
                    Check(cardId > previousCardId,
                        string.Format("Card ids not ascending at index {0}: current {1} <= previous {2}", i, cardId, previousCardId));
                }
                previousCardId = cardId;
            }

            // 4. For card 50001 (rewards 34086 x1 and 34090 x1) BuildDetails of that single card is, after the 4 byte header:
            //    23, 108, 2, ushort 1, ushort 50001, 1, 2, ushort 34086, 1, ushort 34090, 1;
            //    the packet length field at offset 2 equals total length minus 4.
            ItemCardManager.Entry card50001;
            Check(ItemCardManager.TryGetCard(50001, out card50001), "TryGetCard(50001) failed");
            Check(card50001 != null, "card 50001 is null");

            byte[] detailsPacket = ItemCardManager.BuildDetails(new[] { card50001 });
            Check(detailsPacket != null, "BuildDetails returned null");

            ushort detailsPayloadLen = BitConverter.ToUInt16(detailsPacket, 2);
            Check(detailsPayloadLen == detailsPacket.Length - 4,
                string.Format("BuildDetails packet length field at offset 2 ({0}) != total length minus 4 ({1})", detailsPayloadLen, detailsPacket.Length - 4));

            int expectedDetailsLength = 4 + 15;
            Check(detailsPacket.Length == expectedDetailsLength,
                string.Format("BuildDetails length expected {0}, got {1}", expectedDetailsLength, detailsPacket.Length));

            Check(detailsPacket[4] == 23, string.Format("BuildDetails offset 4 expected 23, got {0}", detailsPacket[4]));
            Check(detailsPacket[5] == 108, string.Format("BuildDetails offset 5 expected 108, got {0}", detailsPacket[5]));
            Check(detailsPacket[6] == 2, string.Format("BuildDetails offset 6 expected 2, got {0}", detailsPacket[6]));

            ushort detailsCardCount = BitConverter.ToUInt16(detailsPacket, 7);
            Check(detailsCardCount == 1, string.Format("BuildDetails offset 7 expected ushort 1, got {0}", detailsCardCount));

            ushort detailsCardId = BitConverter.ToUInt16(detailsPacket, 9);
            Check(detailsCardId == 50001, string.Format("BuildDetails offset 9 expected ushort 50001, got {0}", detailsCardId));

            Check(detailsPacket[11] == 1, string.Format("BuildDetails offset 11 expected 1, got {0}", detailsPacket[11]));
            Check(detailsPacket[12] == 2, string.Format("BuildDetails offset 12 expected 2 (rewards count), got {0}", detailsPacket[12]));

            ushort reward1Id = BitConverter.ToUInt16(detailsPacket, 13);
            Check(reward1Id == 34086, string.Format("BuildDetails offset 13 expected reward item id 34086, got {0}", reward1Id));
            Check(detailsPacket[15] == 1, string.Format("BuildDetails offset 15 expected reward quantity 1, got {0}", detailsPacket[15]));

            ushort reward2Id = BitConverter.ToUInt16(detailsPacket, 16);
            Check(reward2Id == 34090, string.Format("BuildDetails offset 16 expected reward item id 34090, got {0}", reward2Id));
            Check(detailsPacket[18] == 1, string.Format("BuildDetails offset 18 expected reward quantity 1, got {0}", detailsPacket[18]));

            Console.WriteLine("ItemCardTests passed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
