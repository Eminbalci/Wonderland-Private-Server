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
                case 1: // Toggle setting
                    HandleToggle(p, r);
                    break;
                case 2: // Get current settings
                    SendCurrentSettings(p);
                    break;
                case 5: // Team Follow / Walk Along setting (21 05 01)
                    HandleTeamFollow(p, r);
                    break;
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Unknown SubAction: {r.B}");
                    break;
            }
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

        void HandleToggle(Player p, RecievePacket r)
        {
            byte settingType = r.Unpack8();
            DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Toggle. Type: {settingType}");

            switch (settingType)
            {
                case 1: // PK Toggle
                    p.Settings.PKABLE = !p.Settings.PKABLE;
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 PKABLE toggled to: {p.Settings.PKABLE}");
                    BroadcastSettingChange(p, 1, p.Settings.PKABLE);
                    break;
                case 2: // Join/Fight Toggle
                    p.Settings.JOINABLE = !p.Settings.JOINABLE;
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 JOINABLE toggled to: {p.Settings.JOINABLE}");
                    BroadcastSettingChange(p, 2, p.Settings.JOINABLE);
                    break;
                case 3: // Chat channel
                    byte channel = r.Unpack8();
                    p.Settings.ChannelCode = (ChannelCodeType)channel;
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Channel set to: {p.Settings.ChannelCode}");
                    break;
                case 4: // Trade Toggle
                    p.Settings.TRADABLE = !p.Settings.TRADABLE;
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 TRADABLE toggled to: {p.Settings.TRADABLE}");
                    BroadcastSettingChange(p, 4, p.Settings.TRADABLE);
                    break;
                default:
                    DebugSystem.Write(DebugItemType.Error, $"[DEBUG] AC33 Unknown setting type: {settingType}");
                    break;
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

            // Optionally broadcast to other players in map
            GameMap map = p.CurMap as GameMap;
            if (map != null)
            {
                // Other players might need to know about PK status change
                // TODO: Broadcast if needed
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
