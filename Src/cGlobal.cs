using System.Diagnostics;
using System.Reflection;
using Server;
using Server.System;
using Game.Code;
using Game;

namespace System
{
    static class cGlobal
    {


        public static bool Run;

        public static string SrvVersion { get { return new Version(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion).ToString(); } }
        public static Server.Config.Settings SrvSettings;

        public static DataBase.CharacterDataBase gCharacterDataBase;
        public static DataBase.UserDataBase gUserDataBase;
        public static DataBase.GameDataBase gGameDataBase;
        public static DataBase.PortalDataBase gPortalDataBase;

        public static DataFiles.PhxItemDat ItemDatManager;
        public static DataFiles.PhxTalkDat TalkDatManager;
        public static DataFiles.PhxMarkDat MarkDatManager;
        public static Wonderland_Private_Server.DataManagement.DataFiles.cCompound2Dat gCompoundDat;
        // EveManager moved to GameDataBase

        public static LoginServer gLoginServer;
        public static ItemMallServer gItemMallServer;
        public static WorldServer gWorld;
        public static Server.API.RegistrationServer gRegistrationServer;

        #region Systems
#pragma warning disable CS0649
        public static TaskManager ApplicationTasks;
        public static UpdateSystem Update_System;
#pragma warning restore CS0649
        #endregion

        #region Settings
        #endregion



    }
}
