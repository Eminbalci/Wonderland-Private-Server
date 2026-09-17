using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RCLibrary.Core.Networking;
using Network;
using Game;

namespace Network.ActionCodes
{
    public class AC63 : AC
    {
        public override int ID { get { return 63; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            byte subCommand = r.Unpack8();
            DebugSystem.Write($"[AC63] Received sub-command: {subCommand}");

            switch (subCommand)
            {
                case 0:
                //case 3: Recv3(ref p, r); break;
                case 2: Recv2(ref p, r); break;
                case 4: Recv4(ref p, r); break;
                default: DebugSystem.Write($"[AC63] Unknown sub-command: {subCommand}"); break;
            }

        }

        void Recv3(ref Player p, RecievePacket r)
        {
            //try
            //{
            //    cGlobal.gCharacterDataBase.unLockName(p.CharacterName);
            //    p.UserName = "";
            //    p.DataBaseID = 0;
            //}
            //catch (Exception t) { Utilities.LogServices.Log(t); }
        }

        void Recv2(ref Player p, RecievePacket e)
        {
            DebugSystem.Write($"[AC63.Recv2] Client selected a character slot");
            try
            {
                byte charNum = e.Unpack8();

                if ((charNum < 1) || (charNum > 2))//by userid
                {
                    p.Send(Tools.FromFormat("bb", 0, 32));
                    return;
                }

                p.Slot = charNum;

                if (!cGlobal.gCharacterDataBase.GetCharacterData((p.Slot == 1) ? p.UserAcc.Character1ID : p.UserAcc.Character2ID, ref p)) // char is not created
                {
                    #region Create Character
                    //cGlobal.gUserDataBase.Update_Player_ID(p.UserAcc.DataBaseID, p.CharID, charNum);
                    //p.State = PlayerState.Connected_CharacterCreation;

                    SendPacket tmp = new SendPacket();
                    tmp.Pack8(1);
                    tmp.Pack8(3);
                    tmp.PackBool((!string.IsNullOrEmpty(p.UserAcc.Cipher)));
                    p.Send(tmp);
                    #endregion
                }
                else
                {
                    #region Login
                    cGlobal.gWorld.OnLogin(p);
                    #endregion
                }
            }
            catch (Exception t) { DebugSystem.Write(new ExceptionData(t)); throw; }
        }

        /// <summary>
        /// Recieved Login info from Client
        /// </summary>
        /// <param name="p"></param>
        /// <param name="r"></param>
        void Recv4(ref Player p, RecievePacket r)
        {
            try
            {
                DebugSystem.Write("[AC63.Recv4] Starting login validation");
                int loginState = 0; //0-good login  1-bad un/pw  2-dup log 3-wrong version 4-need update

                // Check if client sent 2-byte version prefix (e.g. 1205)
                int startPtr = r.GetPtr();
                ushort checkVer = r.Unpack16();
                if (checkVer < 1000 || checkVer > 2000)
                {
                    r.SetPtr(startPtr);
                }

                //sending username and password
                string name = r.UnpackString();
                string password = r.UnpackString();
                name = name.ToLower();

                DebugSystem.Write($"[AC63.Recv4] Username: '{name}', Password length: {password.Length}");

                string[] userdata = null; // data of user


                // Validate username and password length
                if ((name.Length < 4) || (name.Length > 14))
                {
                    DebugSystem.Write($"[AC63.Recv4] Invalid username length: {name.Length}");
                    loginState = 1;
                }
                else if ((password.Length < 4) || (password.Length > 14))
                {
                    DebugSystem.Write($"[AC63.Recv4] Invalid password length: {password.Length}");
                    loginState = 1;
                }
                else if (loginState == 0)
                {
                    DebugSystem.Write("[AC63.Recv4] Calling GetUserData...");
                    uint dbid = 0;
                    #region Validate Account

                    if (cGlobal.gUserDataBase.GetUserData(name, password, out dbid, out userdata))
                    {
                        DebugSystem.Write($"[AC63.Recv4] GetUserData SUCCESS! dbid={dbid}");
                        if ((p.UserAcc.DataBaseID = dbid) != 0)
                        {
                            if (cGlobal.gLoginServer.IsOnline(p.UserAcc.UserID))
                            {
                                DebugSystem.Write($"[AC63.Recv4] User already online! UserID={p.UserAcc.UserID}");
                                loginState = 2;
                            }
                            else if (userdata != null)
                            {
                                p.UserAcc.UserName = userdata[0];
                                p.UserAcc.Cipher = userdata[1];
                                p.UserAcc.IM = int.Parse(userdata[2]);
                                DebugSystem.Write($"[AC63] User '{name}' logged in successfully. DataBaseID={dbid}, UserID={p.UserAcc.UserID}");
                            }
                            else
                            {
                                DebugSystem.Write("[AC63.Recv4] userdata is null!");
                                loginState = 1;
                            }
                        }
                        else
                        {
                            DebugSystem.Write($"[AC63.Recv4] dbid is 0!");
                            loginState = 1;
                        }
                    }
                    else
                    {
                        DebugSystem.Write($"[AC63.Recv4] GetUserData FAILED for user '{name}'");
                        loginState = 1;
                    }
                    #endregion
                }
                //if (userdata != null)
                //    if (userdata.Length != 6)
                //loginState = 1;
                //else if (userdata.Length == 6 && userdata[4].ToString() == "0" && myhost.GameWorld.ServerStatus == ServerMode.TestMode)
                //    loginState = 6;

                #region Result of Login State
                // here we do the results of loginstate
                switch (loginState)
                {
                    case 0:
                        {
                            SendPacket tmp = new SendPacket();
                            tmp.Pack8(63);
                            tmp.Pack8(2);
                            tmp.Pack32(p.UserAcc.UserID);
                            p.Send(tmp);

                            tmp = new SendPacket();
                            tmp.Pack8(63);
                            tmp.Pack8(1);
                            DebugSystem.Write("[AC63] Encrypting Character List Packet");

                            var char1 = cGlobal.gCharacterDataBase.GetCharacterData(p.UserAcc.Character1ID);
                            DebugSystem.Write($"[AC63] GetCharacterData({p.UserAcc.Character1ID}) returned: {(char1 == null ? "NULL" : $"CharID={char1.CharID}, Name={char1.CharName}")}");

                            // Always send character 1 data (create empty if null)
                            if (char1 == null)
                            {
                                DebugSystem.Write("[AC63] Creating empty Character 1...");
                                // Don't send empty character - just skip it
                                // Client expects only existing characters
                            }
                            else
                            {
                                try
                                {
                                    var char1Data = char1.ToArray();
                                    if (char1Data == null)
                                    {
                                        DebugSystem.Write("[AC63] ERROR: char1.ToArray() returned NULL!");
                                    }
                                    else
                                    {
                                        tmp.PackArray(char1Data);
                                        DebugSystem.Write($"[AC63] Character 1 data packed ({char1Data.Count()} bytes)");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DebugSystem.Write($"[AC63] ERROR packing Character 1: {ex.Message}\n{ex.StackTrace}");
                                }
                            }

                            var char2 = cGlobal.gCharacterDataBase.GetCharacterData(p.UserAcc.Character2ID);
                            DebugSystem.Write($"[AC63] GetCharacterData({p.UserAcc.Character2ID}) returned: {(char2 == null ? "NULL" : $"CharID={char2.CharID}, Name={char2.CharName}")}");

                            // Always send character 2 data (create empty if null)
                            if (char2 == null)
                            {
                                DebugSystem.Write("[AC63] Creating empty Character 2...");
                                // Don't send empty character - just skip it
                            }
                            else
                            {
                                try
                                {
                                    var char2Data = char2.ToArray();
                                    if (char2Data == null)
                                    {
                                        DebugSystem.Write("[AC63] ERROR: char2.ToArray() returned NULL!");
                                    }
                                    else
                                    {
                                        tmp.PackArray(char2Data);
                                        DebugSystem.Write($"[AC63] Character 2 data packed ({char2Data.Count()} bytes)");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    DebugSystem.Write($"[AC63] ERROR packing Character 2: {ex.Message}\n{ex.StackTrace}");
                                }
                            }

                            try
                            {
                                p.Send(tmp);
                                DebugSystem.Write("[AC63] Character List Sent successfully");
                            }
                            catch (Exception ex)
                            {
                                DebugSystem.Write($"[AC63] ERROR sending character list: {ex.Message}\n{ex.StackTrace}");
                            }

                            //p.State = PlayerState.Connected_CharacterSelection;
                            p.Send(Tools.FromFormat("bb", 35, 11));
                            DebugSystem.Write("[AC63] Sent packet 35,11 - Waiting for client response...");

                            // Start 5-second timeout for character selection
                            Player playerCopy = p; // Create local copy for lambda
                            Task.Run(async () =>
                            {
                                await Task.Delay(60000); // 60 seconds

                                // Check if player is still waiting for character selection
                                if (!playerCopy.isDisconnected() && playerCopy.Slot == 0)
                                {
                                    DebugSystem.Write($"[AC63] Player {playerCopy.UserAcc?.UserName ?? "Unknown"} timeout - no character selected in 60 seconds. Disconnecting...");
                                    try
                                    {
                                        playerCopy.Disconnect();
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugSystem.Write($"[AC63] Error disconnecting timed-out player: {ex.Message}");
                                    }
                                }
                            });

                        }
                        break;
                    case 1:
                        {
                            p.Send(Tools.FromFormat("bb", 63, 2));
                            p.Send(Tools.FromFormat("bb", 1, 6));
                        }
                        break;
                    case 2:
                        {
                            //if (status != null) status("Server", "Already logged in. ( " + p.UserName + " )");
                            p.Send(Tools.FromFormat("bb", 63, 2));
                            p.Send(Tools.FromFormat("bb", 0, 19));
                            cGlobal.gLoginServer.Disconnect(p.UserAcc.UserID);
                        }
                        break;
                    case 3:
                        {
                            SendPacket sp = new SendPacket();// PSENDPACKET PackSend = new SENDPACKET;
                            //PackSend->Clear();
                            p.Send(Tools.FromFormat("bb", 0, 17));
                            p.Send(sp);
                        }
                        break;
                    case 4:
                        {
                            SendPacket sp = new SendPacket();// PSENDPACKET PackSend = new SENDPACKET;
                            //PackSend->Clear();
                            p.Send(Tools.FromFormat("bb", 0, 65));
                            p.Send(sp);
                        }
                        break;
                    case 5:
                        {
                            SendPacket sp = new SendPacket();// PSENDPACKET PackSend = new SENDPACKET;
                            //PackSend->Clear();
                            p.Send(Tools.FromFormat("bb", 1, 7));
                            p.Send(sp);
                        }
                        break;
                    case 6:
                        {
                            SendPacket sp = new SendPacket();// PSENDPACKET PackSend = new SENDPACKET;
                            //PackSend->Clear();
                            p.Send(Tools.FromFormat("bb", 0, 79));
                            p.Send(sp);
                        }
                        break;
                }
                #endregion
            }
            catch { throw; }
        }
    }
}
