# Weather Particle Engine and Ambient Scene Effects

## 1. Executive Summary & Scene Effects Architecture

Atmospheric immersion in Wonderland Online is produced by real-time particle simulation systems compiled into client units `pFT_SceneEffect.pas` and `se_CloudFloat.pas`.

Reverse engineering of subroutines `FUN_0034f5f8` (Snow Generator) and `FUN_003503f4` (Rain Generator) in [`aLogin_decompiled.c`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c) (lines 329490 to 330200) details how DirectDraw hardware surfaces (`DXDraw1`) render dynamic environmental weather over 2.5D isometric terrain.

---

## 2. Snow Particle Simulation Engine (`FUN_0034f5f8`)

Subroutine `FUN_0034f5f8` initializes dynamic snow storms using 3 distinct flake sprite sizes:

```
+-----------------------------------------------------------------------------------+
|                           SNOW PARTICLE ENGINE METRICS                            |
+-----------------------------------------------------------------------------------+
| Parameter / Property  | Decompiled Value              | Architectural Meaning     |
+-----------------------+-------------------------------+---------------------------+
| Flake Sprites         | icon_Snow1, icon_Snow2, icon3 | 3-tier flake texture size |
| Active Flake Count    | Rand(3) * 100 + 200           | 200 to 500 flakes spawned |
| Spawn Horizon Width   | 800 Pixels                    | Full viewport horizontal  |
| Vertical Bounds       | Viewport Y to Y + 600         | Full viewport vertical    |
| Lateral Drift Delta   | Rand(5) * 50 Pixels           | -250px to +250px X drift  |
| Flake Fall Velocity   | Rand(3) + 1                   | 1 to 4 pixels / tick step |
| Size Distribution     | Probability: 60%, 30%, 10%    | Small, medium, large bias |
| Time Tracking         | GetTickCount()                | Millisecond step timer    |
+-----------------------+-------------------------------+---------------------------+
```

### Snow Spawner Decompiled Algorithm:
```c
// Decompiled from aLogin_decompiled.c lines 329500-329558
void InitializeSnowSystem(SnowContext *ctx) {
    ctx->active_state = 0;
    ctx->flake_count = Rand(3) * 100 + 200; // 200..500 flakes
    ctx->fall_speed = Rand(3) + 1;
    
    // Load flake textures from resource archive
    ctx->tex_small  = ResolveResource(PTR_DAT_004c8a94, "icon_Snow1");
    ctx->tex_medium = ResolveResource(PTR_DAT_004c8a94, "icon_Snow2");
    ctx->tex_large  = ResolveResource(PTR_DAT_004c8a94, "icon_Snow3");
    
    // Horizontal and vertical viewport bounding
    ctx->spawn_x = Rand(800) + Camera->X;
    ctx->spawn_y = Camera->Y;
    ctx->limit_y = Camera->Y + 600;
    
    // Wind drift vector
    if (Rand(2) == 0) {
        ctx->drift_x = ctx->spawn_x + (Rand(5) * 50);
    } else {
        ctx->drift_x = ctx->spawn_x - (Rand(5) * 50);
    }
    ctx->last_tick = GetTickCount();
}
```

---

## 3. Rain Particle Simulation Engine (`FUN_003503f4`)

Subroutine `FUN_003503f4` simulates realistic precipitation with angled raindrops and ground splash triggers:

```
+-----------------------------------------------------------------------------------+
|                           RAIN PARTICLE ENGINE METRICS                            |
+-----------------------------------------------------------------------------------+
| Parameter / Property  | Decompiled Value              | Architectural Meaning     |
+-----------------------+-------------------------------+---------------------------+
| Raindrop Sprite       | icon_Rains                    | Angled water streak sprite|
| Horizontal Horizon    | 0x4B0 (1,200 Pixels)          | Extended viewport width   |
| Vertical Traversal    | 0x1C2 + 0x96 (600 Pixels)     | Full viewport fall height |
| Isometric Slant Angle | -300 Pixels X offset          | 45-degree down-left slant |
| Density Partition     | 90% Light / 10% Heavy         | Dynamic precipitation bias|
| Active Pool Size      | 0x96 (150 Streams)            | Simultaneous rain streaks |
+-----------------------+-------------------------------+---------------------------+
```

### Rain Spawner Decompiled Algorithm:
```c
// Decompiled from aLogin_decompiled.c lines 330089-330141
void InitializeRainSystem(RainContext *ctx) {
    ctx->pool_capacity = 150;
    ctx->fall_speed = Rand(3) + 1;
    ctx->tex_rain = ResolveResource(PTR_DAT_004c8a94, "icon_Rains");
    
    // 1200px wide spawn coverage centered on camera
    ctx->spawn_x = Rand(1200) + Camera->X;
    ctx->spawn_y = Camera->Y;
    ctx->limit_y = Camera->Y + Rand(450) + 150;
    
    // 45-degree isometric rainfall slant
    ctx->target_x = ctx->spawn_x - 300;
    ctx->last_tick = GetTickCount();
}
```

---

## 4. Cloud Particle Drift & Weather Synchronization

1. **`se_CloudFloat.pas` Engine:** Independent from precipitation, cloud shadows and high-altitude cirrus clouds drift across the map based on ambient wind parameters. Clouds cast translucent shadow sprites (`D3DRM` alpha blend) onto the ground tile plane `rDZ_Ground`.
2. **Server-Side Weather Replication:** The server controls map climate states via `SceneData.dat` default ambient flags or dynamic `ActionCode` events:
   - State `0`: Clear / Sunny
   - State `1`: Light Rain
   - State `2`: Heavy Thunderstorm
   - State `3`: Light Flurries
   - State `4`: Heavy Blizzard
   - State `5`: Dense Fog / Sea Mist
