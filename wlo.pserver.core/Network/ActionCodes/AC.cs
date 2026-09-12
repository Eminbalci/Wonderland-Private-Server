using Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace Network.ActionCodes
{
    public class AC
    {
        public virtual int ID { get { return 0; } }
        public virtual void ProcessPkt(Player c, RecievePacket p)
        {
            switch (p.B)
            {
                default: DebugSystem.Write("Action Code " + p.A + "," + p.B + "has not been coded"); break;
            }
        }

        static readonly object mlock = new object();
        static Dictionary<int, AC> AcList = new Dictionary<int, AC>(100);

        public static AC GetAction(int ID)
        {
            //Console.WriteLine($"[DEBUG] GetAction called for ID={ID}, AcList has {AcList.Count} items");

            if (AcList.ContainsKey(ID))
            {
                //Console.WriteLine($"[DEBUG] Found AC {ID} in cache");
                return AcList[ID];
            }

            //Console.WriteLine($"[DEBUG] AC {ID} not in cache, scanning assemblies...");

            lock (mlock)
            {
                AC resp = null;
                if (resp == null)
                {
                    // Search all loaded assemblies for ActionCode classes
                    var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                    //DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Scanning {allAssemblies.Length} assemblies for ACs...");

                    foreach (var asm in allAssemblies)
                    {
                        // DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Scanning Assembly: {asm.GetName().Name}");
                        try
                        {
                            // Log all types in Network.ActionCodes namespace for debugging
                            var allTypesInNs = asm.GetTypes().Where(t => t.Namespace == "Network.ActionCodes" && t.IsClass && !t.IsAbstract && t.IsPublic);
                            foreach (var t in allTypesInNs)
                            {
                                bool isSubclass = t.IsSubclassOf(typeof(AC));
                                //DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Type: {t.Name}, Base: {t.BaseType?.Name}, IsSubclassOfAC: {isSubclass}");
                            }

                            var types = asm.GetTypes().Where(p => p.IsClass && !p.IsAbstract && p.IsPublic && p.IsSubclassOf(typeof(AC)));
                            foreach (var y in types)
                            {
                                try
                                {
                                    AC m = (Activator.CreateInstance(y) as AC);
                                    if (m != null)
                                    {
                                        if (!AcList.ContainsKey(m.ID))
                                        {
                                            AcList.Add(m.ID, m);
                                            //DebugSystem.Write(DebugItemType.Error, $"[DEBUG] Loaded AC {m.ID} from {y.Name}");
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    // After scanning all assemblies, check if requested ID is now in the list
                    if (AcList.ContainsKey(ID))
                        return AcList[ID];
                }
                return null;
            }
        }
    }
}
