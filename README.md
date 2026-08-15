WLO Private Server CheatEngine
=========================

Developing Tools: Visual Studio 2022

Database: sqlite bypass

Private Server + CheatEngine for Wonderland Online

Running steps:
1. Client Location: `D:\garipgudubetseyler\WLRI`
2. Ensure `SERVER.INI` in the client directory is set to `127.0.0.1`
3. Server DAT files in `./Data` are synchronized with the client data files (`Npc.dat`, `Item.dat`, `Skill.dat`, `Talk.dat`, `Eve.emg`, `Ground.MMG`, `SkillData.MBTM`, etc.).
   - Note: The large 1.42 GB sprite archive `odd.dat` can be downloaded directly from [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0).
4. Run `Wonderland Private Server.exe` in `bin/Debug` & wait until log shows "Now listening for clients..."
5. Run `aLogin.exe`, select server and login with `gmone` / `gmone`
Tips:
In the private server exe, click "Cheat" tab:
- Double click the item in list:
    > Maps : Teleports you to that ID
    
    > Vehicle : Ride the vehicle
    
    > Items : Adds the item to your inventory
    
    > Npc : Battle/ride the NPC or Pet
- Each lists have a search textbox on top of it:
    > Type in your search query and then hit Enter
    
    > To reload all, blank the search then hit Enter
- Press `F5` key anywhere in the GUI window to automatically launch `aLogin.exe`.
- Click the in-game PK button (sword icon) and click any monster/NPC to engage in turn-based combat. Supports attack, skills, defending, fleeing, XP/Gold rewards, and automatic battle exit.
- Real-time NPC movement and roaming (`AC 22 Sub 2`) ported from Python server with scripted waypoints and random wandering.
- Character skill unlocking system (`AC 5 Sub 11`, `AC 8 Sub 1`) with character-specific stunt skills, element skills, and `:skill <id> [grade]` chat command.
- In-Game GM Chat Commands:
    > `:heal [hp] [sp]` : Fully restores character HP and SP (or specified values).
    > `:level <1-200>` : Sets character level and recalculates stats.
    > `:gold <amount>` : Sets character gold.
    > `:stat <str> <con> <int> <wis> <agi>` : Sets base character stats.
    > `:item <id> [amount]` : Adds item(s) to inventory.
    > `:skill <id> [grade]` : Unlocks or upgrades a skill.
    > `:warp <map_id> <x> <y>` : Teleports player to map coordinates.
    > `:help` : Shows command help in chat.

