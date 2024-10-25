namespace System;

public static class ObjectExt {

    public static void LogInfo(this object src, string info) {

        Console.WriteLine((object)info);
        DebugSystem.Write(info);
    }
}
