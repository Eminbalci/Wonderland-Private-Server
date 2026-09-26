# Character selection Gold

Native AC63:1 dispatch 0x2ed7f6 invokes 0x402468. Reader 0x40264c reads EXP DWORD at record nameLength+20 into +0x1ad; 0x402686 reads Gold DWORD at nameLength+24 into +0x1b1. Server Character.ToArray previously wrote ulong TotalExp, so normal EXP produced a zero high DWORD in the Gold field. Replace with uint EXP then uint Gold, preserving the 54+nameLength record size, equipment and subsequent character boundary.

Read-only live database at investigation: Mizaki charID4510001, slot2, gold464. No database or currency changes made. A fixture Gold448 reproduced native login Gold0 with the previously deployed storage-bin binary. Patched build passed 123 native-layout assertions for amounts0/448/464/999999, varying names, both slots, EXP and no mutation; 22 database character-list/cached selection/fresh selection/outfit assertions passed with unchanged fixture database. Compile passed with existing CS1998 warnings; git diff --check passed. Live login UI replay remains pending deployment.

login-gold-bin includes pending Item Mall category correction and removal of vault-open notices. Deploy-LoginGold.ps1 backs up and validates four files and unchanged database, leaves server stopped. Supersedes mall-category-bin staging. Not deployed yet.
