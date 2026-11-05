using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.Code;
using Game.Maps;
using Network;
using RCLibrary.Core.Networking;

namespace Network.ActionCodes
{
    public class AC33 : AC
    {
        public override int ID { get { return 33; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Settings. Sub: {r.B}");

            if (p.Settings == null)
            {
                DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Error: Player settings is null!");
                return;
            }

            r.SetPtr(6);
            switch (r.B)
            {
                case 1: // Button 0 in form_module (PK Toggle) or legacy setting toggle
                    HandleSubCode1(p, r);
                    break;
                case 2: // Button 1 in form_module (Join/Fight Toggle) or Get settings
                    HandleSubCode2(p, r);
                    break;
                case 3: // Channel setting
                    HandleChannelSetting(p, r);
                    break;
                case 4: // Trade toggle
                    HandleTradeToggle(p, r);
                    break;
                case 5: // Team Follow / Walk Along setting (21 05 01)
                    HandleTeamFollow(p, r);
                    break;
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Unknown SubAction: {r.B}");
                    break;
            }
        }

        void HandleSubCode1(Player p, RecievePacket r)
        {
            // Client FUN_003c4138 param_2==0 sends [0x21, 1, 0] with 0 extra arguments for PK button.
            // If additional payload is present, inspect settingType.
            if (r.Buffer != null && r.Buffer.Length > 6)
            {
                byte settingType = r.Unpack8();
                switch (settingType)
                {
                    case 1:
                        TogglePk(p);
                        break;
                    case 2:
                        ToggleJoin(p);
                        break;
                    case 3:
                        if (r.Buffer.Length > 7)
                        {
                            byte channel = r.Unpack8();
                            p.Settings.ChannelCode = (ChannelCodeType)channel;
                            PersistSettings(p);
                        }
                        break;
                    case 4:
                        ToggleTrade(p);
                        break;
                    default:
                        // Default fallback for Subcode 1: Toggle PK
                        TogglePk(p);
                        break;
                }
            }
            else
            {
                // Direct PK button click
                TogglePk(p);
            }
        }

        void HandleSubCode2(Player p, RecievePacket r)
        {
            // Client FUN_003c4138 param_2==1 sends [0x21, 2, 0] with 0 extra arguments for Join/Fight button.
            // If payload contains query flags, send settings; otherwise toggle Join.
            if (r.Buffer != null && r.Buffer.Length > 6)
            {
                SendCurrentSettings(p);
            }
            else
            {
                ToggleJoin(p);
            }
        }

        void HandleChannelSetting(Player p, RecievePacket r)
        {
            if (r.Buffer != null && r.Buffer.Length > 6)
            {
                byte channel = r.Unpack8();
                p.Settings.ChannelCode = (ChannelCodeType)channel;
                PersistSettings(p);
                SendCurrentSettings(p);
            }
        }

        void HandleTradeToggle(Player p, RecievePacket r)
        {
            ToggleTrade(p);
        }

        void HandleTeamFollow(Player p, RecievePacket r)
        {
            // ActionCode 33 Subcode 5: Team Follow / Auto-Walk Along Toggle (21 05 01)
            // Verified from official packet capture 'partyegiripharitadegistirdim.pcapng' (Frame 228 & 369)
            byte followVal = 1;
            if (r.Buffer != null && r.Buffer.Length > 6)
            {
                followVal = r[6];
            }
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Team Follow toggle: {followVal} for {p.CharName}");

            // Confirm Team Follow status back to client: S->C [33, 5, followVal]
            SendPacket pkt = new SendPacket();
            pkt.PackArray(new byte[] { 33, 5 });
            pkt.Pack8(followVal);
            p.Send(pkt);
        }

        void TogglePk(Player p)
        {
            p.Settings.PKABLE = !p.Settings.PKABLE;
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 PKABLE toggled to: {p.Settings.PKABLE} for {p.CharName}");
            PersistSettings(p);
            BroadcastSettingChange(p, 1, p.Settings.PKABLE);
        }

        void ToggleJoin(Player p)
        {
            p.Settings.JOINABLE = !p.Settings.JOINABLE;
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 JOINABLE toggled to: {p.Settings.JOINABLE} for {p.CharName}");
            PersistSettings(p);
            BroadcastSettingChange(p, 2, p.Settings.JOINABLE);
        }

        void ToggleTrade(Player p)
        {
            p.Settings.TRADABLE = !p.Settings.TRADABLE;
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 TRADABLE toggled to: {p.Settings.TRADABLE} for {p.CharName}");
            PersistSettings(p);
            BroadcastSettingChange(p, 4, p.Settings.TRADABLE);
        }

        void PersistSettings(Player p)
        {
            try
            {
                if (cGlobal.gCharacterDataBase != null && p.CharID > 0)
                {
                    cGlobal.gCharacterDataBase.SaveSettings(p.CharID, p.Settings.ToString());
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(DebugItemType.Error, $"[AC33] Failed to persist settings: {ex.Message}");
            }
        }

        void BroadcastSettingChange(Player p, byte settingType, bool value)
        {
            // Send confirmation back to player
            SendPacket pkt = new SendPacket();
            pkt.PackArray(new byte[] { 33, 1 });
            pkt.Pack8(settingType);
            pkt.Pack8((byte)(value ? 1 : 2)); // 1 = on, 2 = off
            p.Send(pkt);

            // Also synchronize full settings state with client
            SendCurrentSettings(p);

            // Optionally broadcast to other players in map
            GameMap map = p.CurMap as GameMap;
            if (map != null)
            {
                // Other players might need to know about PK status change
                // Broadcast if needed
            }
        }

        void SendCurrentSettings(Player p)
        {
            // Send current settings to player
            byte[] settingsData = p.Settings.ToArray();
            SendPacket pkt = new SendPacket(settingsData);
            p.Send(pkt);
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Sent settings to {p.CharName}");
        }
    }
}
