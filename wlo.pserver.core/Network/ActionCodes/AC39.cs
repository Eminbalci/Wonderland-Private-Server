using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;

namespace Network.ActionCodes
{
    public class AC39 : AC
    {
        public override int ID { get { return 39; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null) return;

            byte subAction = r.B ?? 0;
            DebugSystem.Write($"[AC39] Player {p.CharName} sent Guild SubAction {subAction}");

            switch (subAction)
            {
                case 1: // Create Guild
                    try
                    {
                        string guildName = r.UnpackString();
                        uint icon = 3402;
                        try { icon = r.Unpack32(); } catch { }
                        if (icon == 0) icon = 3402;

                        if (p.CurGuild != null)
                        {
                            SendPacket pErr = new SendPacket();
                            pErr.Pack8(39);
                            pErr.Pack8(255);
                            pErr.PackString("You are already in a guild!");
                            p.Send(pErr);
                            return;
                        }

                        // Create Guild fee (20,000 gold)
                        if (p.Gold < 20000)
                        {
                            SendPacket pErr = new SendPacket();
                            pErr.Pack8(39);
                            pErr.Pack8(255);
                            pErr.PackString("You need at least 20,000 gold to establish a guild!");
                            p.Send(pErr);
                            return;
                        }

                        p.TakeGold(20000);
                        GuildManager.CreateGuild(p, guildName, icon);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:1] Exception creating guild: {ex.Message}");
                    }
                    break;

                case 2: // Invite Player to Guild
                    try
                    {
                        uint targetCharId = r.Unpack32();
                        if (p.CurGuild == null) return;
                        if (!p.CurGuild.IsLeader(p.CharID) && !p.CurGuild.IsViceLeader(p.CharID))
                        {
                            SendPacket pErr = new SendPacket();
                            pErr.Pack8(39);
                            pErr.Pack8(255);
                            pErr.PackString("Only guild leaders and vice-leaders can invite members!");
                            p.Send(pErr);
                            return;
                        }

                        Player target = null;
                        if (p.CurMap is GameMap curMap)
                        {
                            target = curMap.PlayersList.FirstOrDefault(x => x.CharID == targetCharId);
                        }

                        if (target != null)
                        {
                            if (target.CurGuild != null)
                            {
                                SendPacket pErr = new SendPacket();
                                pErr.Pack8(39);
                                pErr.Pack8(255);
                                pErr.PackString("That player is already in a guild!");
                                p.Send(pErr);
                                return;
                            }

                            // Send Invite Dialog to Target
                            SendPacket pInv = new SendPacket();
                            pInv.Pack8(39);
                            pInv.Pack8(2);
                            pInv.Pack32(p.CharID);
                            pInv.Pack16(p.CurGuild.GuildID);
                            pInv.PackString(p.CurGuild.GuildName);
                            target.Send(pInv);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:2] Exception inviting player: {ex.Message}");
                    }
                    break;

                case 3: // Respond to Guild Invite (Accept / Decline)
                    try
                    {
                        byte response = r.Unpack8(); // 1 = Accept, 0 = Decline
                        ushort guildId = r.Unpack16();

                        if (response == 1)
                        {
                            var guild = GuildManager.GetGuild(guildId);
                            if (guild != null && p.CurGuild == null)
                            {
                                guild.AddMember(p);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:3] Exception in invite response: {ex.Message}");
                    }
                    break;

                case 4: // Leave Guild
                    try
                    {
                        if (p.CurGuild != null)
                        {
                            var g = p.CurGuild;
                            if (g.IsLeader(p.CharID))
                            {
                                GuildManager.DisbandGuild(p);
                            }
                            else
                            {
                                g.RemoveMember(p.CharID);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:4] Exception leaving guild: {ex.Message}");
                    }
                    break;

                case 5: // Kick Member
                    try
                    {
                        uint targetCharId = r.Unpack32();
                        if (p.CurGuild != null && (p.CurGuild.IsLeader(p.CharID) || p.CurGuild.IsViceLeader(p.CharID)))
                        {
                            if (p.CurGuild.IsLeader(targetCharId)) return; // Cannot kick leader
                            p.CurGuild.RemoveMember(targetCharId);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:5] Exception kicking member: {ex.Message}");
                    }
                    break;

                case 6: // Disband Guild
                    try
                    {
                        if (p.CurGuild != null && p.CurGuild.IsLeader(p.CharID))
                        {
                            GuildManager.DisbandGuild(p);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:6] Exception disbanding guild: {ex.Message}");
                    }
                    break;

                case 7: // Update Notice / Rules
                    try
                    {
                        string notice = r.UnpackString();
                        if (p.CurGuild != null && (p.CurGuild.IsLeader(p.CharID) || p.CurGuild.IsViceLeader(p.CharID)))
                        {
                            p.CurGuild.SetNotice(notice);
                            GuildManager.SaveGuild(p.CurGuild);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:7] Exception updating notice: {ex.Message}");
                    }
                    break;

                case 8: // Request Guild Info / Member List
                    try
                    {
                        p.CurGuild?.SendInfo(p);
                    }
                    catch (Exception ex)
                    {
                        DebugSystem.Write($"[AC39:8] Exception requesting guild info: {ex.Message}");
                    }
                    break;

                default:
                    DebugSystem.Write($"[AC39] Unhandled Guild subaction {subAction}");
                    break;
            }
        }
    }
}
