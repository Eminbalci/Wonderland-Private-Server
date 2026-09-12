using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;


namespace Game.Battle
{
    /*
     * 
     * 50,1 s 50,1(player x,y,enermy x,y,skill ushort, timer,0
( (flag:gb)  50.1 s (player x, y, enermy x, y, ushort skill, timer 0 )
     * 
     * */
    public class BattleAction
    {
        public Fighter src;
        public Fighter dst;
        //public Wonderland_Private_Server.DataManagement.DataFiles.Skill skill;  // Commented out due to cross-project reference issue

        public byte unknownbyte;
        public byte unknownbyte2;
    }

    public class Battle
    {
        readonly object mylock = new object();
        bool blockupdt;

        #region Definitions
        DateTime roundend_time = DateTime.MinValue;

        public Dictionary<byte, BattleScene> Side;

        public eBattleType TypeofBattle;
        public eBattleState BattleState;
        public eBattleRoundState RoundState;

        public UInt16 Background, battleID;
        public Fighter startedby;

        #endregion
        public UInt16 BattleID { get { return battleID; } }
        public BattleScene this[BattleRole key]
        {
            get
            {
                return Side[(byte)key];
            }
        }


        public Battle(UInt16 BG, int BattleID)
        {
            Background = BG;
            Side = new Dictionary<byte, BattleScene>();
            Side.Add(2, new BattleScene((BattleRole)2, this));
            Side.Add(4, new BattleScene((BattleRole)4, this));
            Side.Add(5, new BattleScene((BattleRole)5, this));
        }


        public int FighterCnt
        {
            get
            {
                return 0;// Side[2].Count + Side[5].Count;
            }
        }

        public IEnumerable<IReadOnlyList<Fighter>> AllFighters
        {
            get
            {
                return (from list in (from cell in Side.Values select cell.FighterList) select list);
            }
        }
        bool AllReady { get { return (Side[2].EveryoneReady && Side[5].EveryoneReady); } }

        #region Processing

        public void Process()
        {
            if (blockupdt) return;
            blockupdt = true;
            if (BattleState == eBattleState.Active)//battle has activated
            {
                //check if each side has players that are alive
                if (!(Side[2].Total_Fighters_Alive > 0 && Side[5].Total_Fighters_Alive > 0) && RoundState != eBattleRoundState.CalculatingState)
                {
                    BattleState = eBattleState.Ended;
                    EndBattle(eBattleLeaveType.BattleFinished); return;
                }
                //check if every1 sent a command during ready round (
                if (AllReady && /*!HasOrders &&*/ RoundState == eBattleRoundState.ReadyState)//every1 ready no action
                    RoundState = eBattleRoundState.EndedState;
                else if (RoundState == eBattleRoundState.EndedState /*&& HasOrders*/)//action finished more left
                    RoundState = eBattleRoundState.ReadyState;
                else if (RoundState == eBattleRoundState.EndedState /*&& !HasOrders*/)//Round over no action
                    StartRound();
                else if (roundend_time < DateTime.Now && RoundState == eBattleRoundState.PrepState || AllReady && RoundState == eBattleRoundState.PrepState)//Planning stage Every1 ready/timefinished
                    RoundState = eBattleRoundState.ReadyState;
                //else if (AllReady && HasOrders && RoundState == eBattleRoundState.ReadyState)//every1 ready has orders ready to do action i guess
                //    Calculate();
            }
            blockupdt = false;
        }

        //Send StartRd info
        public void StartRound()
        {
            RoundState = eBattleRoundState.PrepState;
            Side[2].OnNewRound();
            //foreach (var h in Side[2].Total_Fighters_Alive)
            //    h.RdEndTime = DateTime.Now.AddSeconds(20);
            Side[5].OnNewRound();
            //foreach (var h in Side[5].Total_Fighters_Alive)
            //    h.RdEndTime = DateTime.Now.AddSeconds(20);
        }
        //Rcv Attk
        public void PLayer_BattleAction(BattleAction data)
        {
        }

        //End Battle for all players
        public void StartBattle()
        {
            BattleState = eBattleState.Active;
        }

        public void EndBattle(eBattleLeaveType t)
        {
            BattleState = eBattleState.Ended;
        }

        #endregion



        #region BattleSide Control
        public void onJoinBattle(Fighter f)
        {
        }

        //Watching Battle
        public void onWatchBattle(Fighter f)
        {
        }

        public void RemFighter(eBattleLeaveType o, Fighter src)
        {
        }

        public void RemFighter(eBattleLeaveType o, uint ID)
        {
            RemFighter(o, FindFighter(ID));
        }

        public Fighter FindFighter(uint ID)
        {
            return null;
        }

        public Fighter FindFighter(byte x, byte y)
        {
            foreach (var t in Side.Values.ToList())
                foreach (Fighter r in t.FighterList)
                    if (r.GridX == x && r.GridY == y)
                        return r;
            return null;
        }
        #endregion

        #region Calculations
        double GetAtkDamage(ushort atk_matk, ushort SkillPower, ushort Def_mdef, float elementCorr)
        {
            var est = 0.0;
            if (atk_matk >= Def_mdef)
            {
                est = Math.Round(((atk_matk * (.5 + new Random().NextDouble()) + SkillPower) - (Def_mdef * 0.98)) * elementCorr);
            }
            else if (atk_matk <= Def_mdef)
            {
                est = ((atk_matk * (.5 + new Random().NextDouble()) + SkillPower) - ((int)Def_mdef * 0.98)) * elementCorr;
                if (est < 0) est = 1;
            }
            if (est < 0)
                return Def_mdef / 3;
            else return est;
        }
        double GetMatkDamage(int atk_matk, int SkillPower, int Def_mdef, float elementCorr)
        {
            var est = 0.0;
            if (atk_matk >= Def_mdef)
            {
                est = ((atk_matk * (1 + new Random().NextDouble()) + SkillPower) - ((int)Def_mdef * 0.98)) * elementCorr;
            }
            else if (atk_matk <= Def_mdef)
            {
                est = ((atk_matk * (1.1 + new Random().NextDouble()) + SkillPower) - ((int)Def_mdef * 1.3)) * elementCorr;
            }
            if (est < 0)
                return Def_mdef / 3;
            else return est;
        }
        double GetElementCorrection(Affinity hitter, Affinity target)
        {
            switch (hitter)
            {
                case Affinity.Fire:
                    {
                        switch (target)
                        {
                            case Affinity.Normal: return 1.0;
                            case Affinity.Fire: return 1.0;
                            case Affinity.Earth: return 1.0;
                            case Affinity.Water: return 0.6;
                            case Affinity.Wind: return 1.5;
                        }
                    }
                    break;
                case Affinity.Earth:
                    {
                        switch (target)
                        {
                            case Affinity.Normal: return 1;
                            case Affinity.Fire: return 1.0;
                            case Affinity.Earth: return 1.0;
                            case Affinity.Water: return 1.7;
                            case Affinity.Wind: return 0.6;
                        }
                    }
                    break;
                case Affinity.Water:
                    {
                        switch (target)
                        {
                            case Affinity.Normal: return 1;
                            case Affinity.Fire: return 1.7;
                            case Affinity.Earth: return 0.6;
                            case Affinity.Water: return 1.0;
                            case Affinity.Wind: return 1.0;
                        }
                    }
                    break;
                case Affinity.Wind:
                    {
                        switch (target)
                        {
                            case Affinity.Normal: return 1;
                            case Affinity.Fire: return 0.4;
                            case Affinity.Earth: return 1.7;
                            case Affinity.Water: return 1.0;
                            case Affinity.Wind: return 1.0;
                        }
                    }
                    break;
                case Affinity.Normal:
                    {
                        switch (target)
                        {
                            case Affinity.Normal: return 1;
                            case Affinity.Fire: return 1.3;
                            case Affinity.Earth: return 1.3;
                            case Affinity.Water: return 1.3;
                            case Affinity.Wind: return 1.3;
                            default: return 1.0;
                        }
                    }
                case Affinity.Dark:
                case Affinity.Undefined:
                    {
                        switch (target)
                        {
                            case Affinity.Normal: return 1.0;
                            case Affinity.Fire: return 1.35;
                            case Affinity.Earth: return 1.35;
                            case Affinity.Water: return 1.35;
                            case Affinity.Wind: return 1.35;
                            default: return 1.0;
                        }
                    }
            }
            return 1.0;
        }
        bool CanFlee(byte srclvel, byte dstlevl)
        {
            if (srclvel < dstlevl)
            {
                int y = dstlevl / 6;
                return Succuss_miss(75 - y, 100, 0);
            }
            else
                return true;
        }
        bool Apply_Crit(byte crit_perc)
        {
            return true;
        }
        bool Succuss_miss(double pert, int outof, double bonus)
        {
            if ((new Random().Next(0, outof) + new Random().NextDouble() + bonus) >=
                (outof - (double)((pert * 100) / outof)))
            {
                return true;
            }
            return false;
        }
        #endregion
    }
}

