using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.PlayerRelated;

internal static class MessageBoardTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void CheckBytes(byte[] actual, int offset, byte[] expected, string message)
    {
        if (actual == null) throw new Exception(message + ": actual is null");
        if (actual.Length < offset + expected.Length)
            throw new Exception(string.Format("{0}: length {1} < offset {2} + expected {3}", message, actual.Length, offset, expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[offset + i] != expected[i])
            {
                throw new Exception(string.Format("{0}: mismatch at index {1} (offset {2}): expected {3} got {4}", message, i, offset + i, expected[i], actual[offset + i]));
            }
        }
    }

    public static int Main(string[] args)
    {
        try
        {
            // OleDate tests
            Check(MessageBoardWire.OleDate(0) == 25569, "OleDate(0) == 25569");
            Check(MessageBoardWire.OleDate(86400000) == 25570, "OleDate(86400000) == 25570");

            // ValidText tests
            Check(MessageBoardWire.ValidText(Encoding.ASCII.GetBytes("abc"), 30, false), "ValidText accepts 'abc'");
            Check(!MessageBoardWire.ValidText(new byte[0], 30, false), "ValidText rejects empty");
            Check(!MessageBoardWire.ValidText(new byte[31], 30, false), "ValidText rejects 31 bytes at limit 30");
            Check(!MessageBoardWire.ValidText(new byte[] { 7 }, 30, true), "ValidText rejects byte 7");
            Check(!MessageBoardWire.ValidText(new byte[] { 10 }, 30, false), "ValidText rejects newline when singleline");
            Check(MessageBoardWire.ValidText(new byte[] { 10 }, 30, true), "ValidText accepts newline only when multiline");

            // LastPage tests
            Check(MessageBoardWire.LastPage(0) == 1, "LastPage(0) == 1");
            Check(MessageBoardWire.LastPage(10) == 1, "LastPage(10) == 1");
            Check(MessageBoardWire.LastPage(11) == 2, "LastPage(11) == 2");

            // BuildPagination test: BuildPagination(12, 2) after 4-byte header is 82, 4, 12,0,0,0, 2
            byte[] pag = MessageBoardWire.BuildPagination(12, 2);
            CheckBytes(pag, 4, new byte[] { 82, 4, 12, 0, 0, 0, 2 }, "BuildPagination(12, 2)");

            // BuildEntries with one post (Id 7, AuthorId 9, author "Al", title "Hi", PostedAtMillis 0)
            // after the header is 82, 1, 2,0,0,0, 65,108, 2,0,0,0, 72,105, the 8 bytes of BitConverter.GetBytes(25569.0), 9,0,0,0, 7,0,0,0
            var post = new MessageBoardPost
            {
                Id = 7,
                AuthorId = 9,
                AuthorName = Encoding.ASCII.GetBytes("Al"),
                Title = Encoding.ASCII.GetBytes("Hi"),
                Content = Encoding.ASCII.GetBytes("Yo"),
                PostedAtMillis = 0
            };

            var expectedEntries = new List<byte> { 82, 1, 2, 0, 0, 0, 65, 108, 2, 0, 0, 0, 72, 105 };
            expectedEntries.AddRange(BitConverter.GetBytes(25569.0));
            expectedEntries.AddRange(new byte[] { 9, 0, 0, 0, 7, 0, 0, 0 });

            byte[] entries = MessageBoardWire.BuildEntries(new[] { post });
            CheckBytes(entries, 4, expectedEntries.ToArray(), "BuildEntries");

            // BuildContent for the same post with content "Yo"
            // after the header is 82, 2, 2,0,0,0, 65,108, 2,0,0,0, 72,105, the date bytes, 2,0,0,0, 9,0,0,0, 89,111
            var expectedContent = new List<byte> { 82, 2, 2, 0, 0, 0, 65, 108, 2, 0, 0, 0, 72, 105 };
            expectedContent.AddRange(BitConverter.GetBytes(25569.0));
            expectedContent.AddRange(new byte[] { 2, 0, 0, 0, 9, 0, 0, 0, 89, 111 });

            byte[] content = MessageBoardWire.BuildContent(post);
            CheckBytes(content, 4, expectedContent.ToArray(), "BuildContent");

            Console.WriteLine("MessageBoardTests passed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
