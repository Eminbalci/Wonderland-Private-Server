# Safe Server Shutdown & Instant Save System

## 1. Overview
The **Safe Server Shutdown** and **Save All Data Now** buttons provide deterministic, atomic persistence of all game data prior to shutting down or during runtime operations.

---

## 2. UI Controls ([MainForm1.Designer.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.Designer.cs))

Located prominently on the **Status** tab (`tabPage7`) directly above the server console:
1. `btnSaveAllNow` (**💾 Save All Data Now**):
   * Flushes all active online character profiles, bag inventories, equipped gear, player stats, gold, quest journals, tent contents, and map states to SQLite.
   * Flushes drop configurations to `Data/chest_drops.txt`.
   * Saves server configuration to `%AppData%/PServer/Config.settings.wlo`.
   * Server continues running without interruption.
2. `btnSafeShutdown` (**🛡️ Safe Server Shutdown**):
   * Prompts user confirmation via `MessageBox.Show`.
   * Broadcasts a server shutdown alert (`AC 23 Sub 57`) to all connected game clients: `"Server is performing a safe shutdown. Saving all data..."`.
   * Executes the full atomic data save for all players, drops, and configurations.
   * Gracefully shuts down network listeners (`gLoginServer.Kill()`, `gWorld.Kill()`).
   * Cleanly closes the application (`Application.Exit()`).

---

## 3. Implementation Details ([MainForm1.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs))

```csharp
private void btnSafeShutdown_Click(object sender, EventArgs e)
{
    // 1. Confirmation prompt
    // 2. In-game broadcast to all players
    // 3. Complete database write (cGlobal.gCharacterDataBase.WritePlayer)
    // 4. Save drop tables & configs (ChestDropManager.SaveToFile)
    // 5. Terminate listeners and exit gracefully
}
```
