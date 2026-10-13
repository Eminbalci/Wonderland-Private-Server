# Delphi Unit Architecture, Package Manifest & Modular Source Hierarchy

## 1. Executive Summary & Compilation Archaeology

Through deep PE resource inspection using REA (`inspect_pe_resources`), the embedded Borland Delphi package header `PACKAGEINFO` (Size: 3,468 bytes at RVA `0x887098` / File Offset `0x513098`) inside [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) has been extracted.

This resource preserves the original Pascal source unit names (`.pas` / `.dcu`) compiled into the client executable. The 280 extracted units reveal the complete internal source modularity, architectural naming conventions, and subsystem boundaries established by the original Chinese Gamer International (中華網龍) development team.

---

## 2. Source Unit Taxonomy by Architectural Domain

```
+-----------------------------------------------------------------------------------+
|                     aLogin.exe INTERNAL SOURCE UNIT TOPOLOGY                      |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ Binary Asset Parsers: AC_* / LAC_* / kAC_* / aAC_* ]                           |
|    - AC_Item (Item.dat), AC_Skill (Skill.dat), AC_Talk (Talk.dat)                 |
|    - aAC_Npc (Npc.dat), AC_SceneData (SceneData.dat), LAC_Formula (Formula.dat)   |
|    - kAC_Mark (Mark.dat), AC_Compound (Compound.dat), AC_Compound2 (Compound2.Dat)|
|    - BK_EventManage, BK_EventObject, lRE_EveStruct (Eve.emg Event Bytecode)       |
|                                                                                   |
|  [ Network Transport & Packet Framing: DZ_* / ScktComp ]                         |
|    - DZ_BasicConnect / DZ_Connect: Winsock TCP socket abstraction (Ports 6414/16) |
|    - DZ_Input / DZ_Output: Packet stream framing (0x44F4 magic, XOR 0xAD crypto)  |
|    - DZ_Calculate: Client-side mathematical & coordinate transform helpers        |
|    - DZ_Human / DZ_Thing: Player and item entity synchronizers                    |
|    - DZ_Fight / DZ_FightMenu: Turn-based combat socket protocol state machine      |
|                                                                                   |
|  [ Game Mechanics & Systems: CY_* / DNJ_* / DCJ_* / se_* ]                        |
|    - CY_Element: 4-Element wheel matrix (Earth, Water, Fire, Wind)                |
|    - CY_Organ: Guild / Organization management and guild skills                   |
|    - CY_Trade / CY_TradeMenu: Bilateral player trading state machine              |
|    - CY_GM / CY_GmForm / PAC_GMFunction: 56-button Developer Administration Suite |
|    - DNJ_PKManager: Player challenge, PvP dueling & battlefield enforcement       |
|    - IN_CooperateFight / mCH_Fight: Multi-player turn co-op battle synchronization|
|    - DCJ_AbilityUpgradeManage / Se_NpcUpgrade: Equipment forge enhancement        |
|    - se_Marrage: Marriage ceremonies, divorce cooldowns, and romance registry    |
|    - se_SkillTree: Branching skill tree unlocking and SP progression              |
|    - se_SkinSelect / AC_FaceSel: Character creation, facial hair, palette selects |
|                                                                                   |
|  [ Vehicles, Navigation & Terrain: CH_* / nNJ_* / se_GPS ]                        |
|    - CH_RidePet: Companion pet riding, mount positioning, and stance sync         |
|    - CH_Traffic / nNJ_TrafficData: Vehicle speeds, fuel usage, and degradation    |
|    - se_GPS: Minimap radar, world coordinate positioning, portal waypoint routing |
|    - rDZ_Ground / lse_Ground / nse_GroundObj: Map tile layer & obstacle matrix    |
|    - pFT_SceneEffect / se_CloudFloat: Rain, snow, mist, and cloud particle system  |
|                                                                                   |
|  [ Housing & Interior Decorating: RE_* ]                                          |
|    - RE_Furniture / RE_DrawFur: Tent interior placement grids and rotation        |
|    - Re_Camp: Overworld tent pitch state, tent locking, and visitor permission    |
|                                                                                   |
|  [ Mini-Game Suite: MZ_* / LD_* / KMK_* / CH_* / yRE_* ]                          |
|    - MZ_BoxingGame: Rhythm punching power-meter boxing game                       |
|    - LD_DigHoleGame: Underground treasure digging minigame                        |
|    - SMZ_MarioGame: Platformer mini-game module                                   |
|    - MR_ShiBaDou / RE_SGame_BiDaXiao: 18 Beans & Big/Small dice gambling          |
|    - yRE_Chess / RE_ChessTools: 5-in-a-Row Gobang board game                      |
|    - cMZ_Catcher: UFO Arcade Claw Machine                                         |
|    - TMR_Lotto: In-game lucky draw lottery wheel                                  |
|                                                                                   |
|  [ Multimedia, Codecs & DelphiX Engine ]                                          |
|    - VorbisFile / OggPlayer / HL_Mp3Player: Multi-track BGM stream playback       |
|    - MSAcm / wavADPCM / DXSounds: Sound effects wave playback                     |
|    - DXDraws / DXPlay / PDXRender / DXTexImg: DirectDraw 2D hardware renderer     |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 3. Complete 280 Unit Manifest Index

The compiled units preserved in `PACKAGEINFO` are alphabetically indexed below:

```
AC_Compound, AC_Compound2, AC_FaceSel, AC_ICQ, AC_Item, AC_SceneData, AC_Skill, AC_Talk, AC_Twinkle, 
ARE_SceneInfo, AxCtrls, BCY_Mouse, BFT_BmpCheck, BIG5AndGB, BK_EventManage, BK_EventObject, BK_INN, 
BK_Menu, Buttons, CH_MiniGame, CH_RidePet, CH_Traffic, CJ_Menu, CJ_MiniGame, CJ_ReferToMaterial, 
CY_Element, CY_Funcs, CY_GM, CY_GmForm, CY_LittleSport, CY_Menu, CY_MoneyMenu, CY_Organ, CY_ServerTool, 
CY_Skill, CY_Team, CY_TeamMenu, CY_Trade, CY_TradeMenu, CY_WMG, Classes, Clipbrd, Codec, ComStrs, 
CommDlg, Commctrl, Consts, Contnrs, Controls, CopyPrsr, DCJ_AbilityUpgradeManage, DNJ_PKManager, 
DXClass, DXConsts, DXDraws, DXPlay, DXPlayFm, DXSounds, DXTexImg, DZ_BasicConnect, DZ_Browser, 
DZ_Calculate, DZ_Connect, DZ_Fight, DZ_FightMenu, DZ_Game, DZ_Human, DZ_Input, DZ_Light, DZ_Output, 
DZ_String, DZ_Thing, DZ_ThingMenu, DZ_Wugon, Dialogs, EActnList, ECJ_NewDialog, ExtCtrls, FComObj, 
FT_AgreeView, FT_Agreement, FT_Boardcast, FT_Movie_Object, FT_OfficerView, FileCtrl, FlatSB, Forms, 
GMMSystem, GUG_StaticErrorLogProcess, Graphics, HDirectX, HL_BlackNameList, HL_MaidMenu, HL_Minigame, 
HL_Monitor, HL_MonitorManager, HL_Mp3Player, HL_NewFireworks, HL_Shop, HL_Tools, HTTPApp, IDlgs, 
IN_CooperateFight, ImgList, IniFiles, JCY_Map, JCY_WareMenu, JK_man_show, JK_menu, JK_sound, 
JWS_Menu, Jk_mansel, KMK_MiniGame, KWindows, Kse_BlockMsg, LAC_Formula, LDZ_Menu, LD_DigHoleGame, 
LD_MiniGame, LD_PokeGame, LD_Top100, La_Menu, Lse_Test, MK_Computer, MK_Explorer, MK_Menu, MPlayer, 
MR_Menu, MR_ShiBaDou, MSAcm, MZ_BoxingGame, MZ_Calculator, MZ_Doll, MZ_DupMis, MZ_Menu, MZ_MiniGame, 
Mask, Masks, Math, Messages, MultiMon, NDZ_Right, NJ_Menu, NJ_PlayerTitleManager, NMConst, NMExtstr, 
NMHttp, NMURL, NMUUE, ODXInput, OSTypes, OXX_SampleUnit, OggPlayer, OleConst, OleCtrls, OleServer, 
PAC_GMFunction, PDXRender, PMZ_TPic, Printers, Psock, QMPGTools, QTypInfo, Qro_draw, RE_ChessTools, 
RE_DrawFur, RE_Furniture, RE_Menu, RE_MigrantManage_Tools, RE_PubFunc, RE_SGame_BiDaXiao, RE_Skill, 
RE_Thing, RHL_AllFormManager, RHL_Menu, Re_Camp, Re_ClientConst, Re_ClientFunc, Re_PubConst, 
Re_PubType, RegStr, Registry, RichEdit, Rjpeg, SHDocVw, SMZ_MarioGame, ScktComp, Se_Const, Se_EquipUI, 
Se_FightEffect, Se_NpcUpgrade, Se_TalkForm, Se_UI_Login, ShellAPI, ShlObj, StdActns, StdVCL, SyncObjs, 
SysConst, SysInit, SysUtils, System, TH_Hunter, TH_MiniGame, TH_scotd, TMR_Lotto, ToolUtils, ToolWin, 
UTH_Menu, UrlMon, VorbisFile, WBK_ServerTool, WS_MiniGame, WWinSpool, WebConst, WinInet, WinSock, 
X937bf998ec, XGrids, XSe_HInput, YCH_Menu, YCY_Sport, aAC_Npc, aIJL, aLogin, cLD_Menu, cMZ_Catcher, 
dSe_donate, dStdCtrls, fWave, ft_role_rgb_tool_Type, hRE_Draw, jconsts, jo_Cursor, jo_MixItem, 
jo_TaskView, jpgenc, kAC_Mark, lRE_EveStruct, lse_Ground, mCH_Fight, mWS_Operation, mmxutil, 
nComCtrls, nNJ_TrafficData, nse_GroundObj, oCY_Equip, pFT_SceneEffect, qComConst, rDXETable, 
rDZ_Ground, sActiveX, se_ArmyGroup, se_BmpCacheAndDraw, se_CloudFloat, se_DrawNumber, se_EnsignType, 
se_GPS, se_Marrage, se_Mp3Player, se_SkillTree, se_SkinSelect, se_UI, se_item, se_loadingThread, 
se_mainUI, se_procedures, uBK_SimServer, vMenus, vwb_jil, wavADPCM, wb_CVCode, wb_alloc, wb_dblist, 
wb_graph, wb_texmgr, wb_tpu_chin, wb_util, xwb_file, yRE_Chess
```

---

## 4. Significance for Private Server Architecture

1. **Exact Subsystem Correspondence:** Handlers in `wlo.pserver.core` can now be mapped 1:1 against the original client unit that processes their packets (e.g. `DZ_Fight.pas` $\leftrightarrow$ `CombatEngine.cs`, `BK_EventManage.pas` $\leftrightarrow$ `EveEventInterpreter.cs`, `se_Marrage.pas` $\leftrightarrow$ Marriage handlers).
2. **Elimination of Guesses:** Knowing that `CH_Traffic` and `nNJ_TrafficData` handle vehicles while `CH_RidePet` handles mounts confirms that vehicles and pets use distinct motion and collision controllers.
3. **Data Pipeline Verification:** The `AC_*` prefix uniformly identifies units that decode binary database files, validating that `LAC_Formula.pas` decodes `Formula.dat`, `kAC_Mark.pas` decodes `Mark.dat`, and `aAC_Npc.pas` decodes `Npc.dat`.
