# Native client pet protocol investigation

Client inspected: `D:\Game Private\WLRI\aLogin.exe`, x86 Delphi, static disassembly only. No live rendering confirmation. Do not treat server round-trip tests as gameplay proof.

- AC15:1 parser 0x409820: 54-byte payload including AC/sub. Owner DWORD at2, template DWORD at6, ownership byte at10, STR/CON/INT/WIS/AGI WORDs at11..20, level BYTE at21, cumulative EXP DWORD at22. Three BYTE+DWORD skill progress records at26..40. Amity at41, WORD42, BYTE44, reborn45, job46, BYTE47..49, WORD50, WORD52. HP/SP are NOT fields in this packet.
- AddPetByID 0x423958 rejects duplicate template IDs, allocates the first free internal slot (0x424534), appends visual order (pet+0x21bc). It does not prepend the roster. Internal slots and displayed ordinal are separate; deleting changes displayed ordinals but leaves internal holes. RegisterClientPet maintains session slots without rewriting DB slots. Legacy duplicate species are retained in DB, not sent as a second client entry.
- AC8:2 handler 0x2e1689 / 0x416ebc: selector BYTE4, internal slot WORD, stat BYTE, sign BYTE, value DWORD, skill ID DWORD. Names/EXP/stats/selection must all target the same session identity.
- AC15:9 parser 0x40ac44: owner DWORD, internal slot BYTE, raw remaining name bytes. AC15:6 is not an S->C rename notification.
- AC19:1 0x40c13c / 0x427260 selects owner pet ID. AC19:2 rests owner. AC19:4 0x407f64 takes ONE DWORD pet ID and overwrites local active selection; sending ownerID+petID makes ownerID the active pet. Never broadcast it. AC19:5 ignored client. AC19:7 0x40a100 takes owner DWORD to rest a remote character.
- AC15:4 0x40a72c describes a remote map character pet. Do not send to self. Owner DWORD, pet DWORD, ride BYTE, ownership BYTE, length-prefixed name, trailing WORD+WORD+BYTE+BYTE+WORD.
- AC11:5 0x38f600 divides remaining fighter bytes by31. Previous pet record29bytes yielded zero parsed fighters. Type4 pet branch at0x38f9d9. Append trailing WORD to make31bytes.

Tests: Test-PetNativeProtocol.ps1 (55 checks, Windows PowerShell); Test-PetBattleRoster.ps1 (actual AC19 with isolated transport double, run pwsh for modern C#); Test-PetProgression.ps1 (1010 checks). Build in .codex-verify/main-bin; main executable is obj/Debug. No DB repairs/reset done in this change. Client reconnect required to rebuild corrupt old roster state.

Not addressed: legacy admin CharacterDataEditorForm live pet reload still uses old re-recruit/sync logic. Do not use that admin action during verification. Unused CreatePetListPacket AC15:8 has not been verified; do not enable it.
