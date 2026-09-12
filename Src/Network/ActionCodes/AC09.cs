using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using RCLibrary.Core.Networking;
using Game;
using Game.Code;

namespace Network.ActionCodes
{
    public class AC09 : AC
    {
        public override int ID { get { return 09; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.Unpack8())
            {
                case 1: Recv1(ref r, p); break;
                case 2: Recv2(r, p); break;
            }
        }

        void Recv1(ref Player tp, RecievePacket e)
        {
            try
            {
                tp.Eqs.Body = (BodyStyle)e.Unpack16();
                tp.Eqs.Head = (byte)e.Unpack16();
                tp.HairColor = e.Unpack16();
                tp.SkinColor = e.Unpack16();
                tp.ClothingColor = e.Unpack16();
                tp.EyeColor = e.Unpack16();
                tp.Eqs.Element = (Affinity)e.Unpack8();
                tp.Eqs.Str = e.Unpack8();
                tp.Eqs.Agi = e.Unpack8();
                tp.Eqs.Wis = e.Unpack8();
                tp.Eqs.Int = e.Unpack8();
                tp.Eqs.Con = e.Unpack8();
                if (string.IsNullOrEmpty(tp.UserAcc.Cipher))
                {
                    try
                    {
                        string cipherStr = e.UnpackString();
                        if (!string.IsNullOrEmpty(cipherStr) && cipherStr.Length >= 6 && cipherStr.Length <= 14)
                        {
                            tp.UserAcc.Cipher = cipherStr;
                        }
                    }
                    catch { /* optional cipher string not present */ }
                }

                // Determine slot: slot 1 if free, else slot 2
                if (cGlobal.gCharacterDataBase.GetCharacterData(tp.UserAcc.Character1ID) != null)
                {
                    tp.Slot = 2;
                }
                else
                {
                    tp.Slot = 1;
                }

                // Fallback charName safety
                if (string.IsNullOrEmpty(tp.CharName) || tp.CharName.Length < 4 || tp.CharName.Length > 14)
                {
                    tp.CharName = tp.UserAcc.UserName;
                }

                tp.SetBeginnerOutfit();
                tp.Eqs.ApplyCharacterBaseStats();

                tp.FillHP(); tp.FillSP();
                tp.Settings.ChannelCode = (ChannelCodeType)31;
                tp.Settings.TRADABLE = true;
                tp.Settings.PKABLE = false;
                tp.Settings.JOINABLE = true;
                tp.Eqs.TotalExp = 6;
                tp.LoginMap = 10017; //ship map 10017;
                tp.CurX = 1042; // ship x 1042;
                tp.CurY = 1075; //ship y 1075;
                tp.SetGold(0);
                // Give starter quest
                tp.Started_Quests.Add(new Quest() { QID = 1 });

                // Deliver authentic starter item pack (persisted directly into inventory DB via WriteNewPlayer)
                Game.PlayerRelated.StarterPackManager.DeliverToPlayer(tp, sendData: false);

                if (cGlobal.gCharacterDataBase.WriteNewPlayer(tp.CharID, tp))
                {
                    //cGlobal.gUserDataBase.Update_Player_ID(tp.UserAcc.DataBaseID, tp.CharID, (byte)tp.Slot);
                    cGlobal.gUserDataBase.UpdateUser(tp.UserAcc.DataBaseID, tp.UserAcc.Cipher);
                    cGlobal.gWorld.OnLogin(tp);
                }
                else
                    throw new Exception("unable to write player");

                //tp.CharacterState = GameCharacter.PlayerState.Logging_In;

            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
                tp.Send(Tools.FromFormat("bb", 0, 30));
            }


        }

        void Recv2(Player tp, Packet e)
        {
            int nameLen = e.Count - 2;
            string name = e.UnpackStringN();
            if ((nameLen < 4) || (nameLen > 14) || !cGlobal.gCharacterDataBase.LockName(tp.UserAcc.DataBaseID, name))
            {
                tp.Send(Tools.FromFormat("bbb", 9, 3, 1));
                return;
            }
            tp.CharName = name;
            tp.Send(Tools.FromFormat("bbb", 9, 3, 0));
        }
    }
}
