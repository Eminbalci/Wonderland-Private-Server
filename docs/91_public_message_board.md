# Public Message Board (AC82)

Ported from `Wonderland-Gonline` (`internal/server/message_board.go`, `internal/server/wire/message_board.go`,
`internal/store/message_board.go`, `docs/MESSAGE_BOARD.md`). The old AC82 cases 7, 8, 9, 11 (in-memory guild
bulletin, payload not parsed) are left in place; the native board uses cases 1 to 4.

## Opening

Eve action 7 with service 15 (for example Welling map 12050, NPC click 36) sends `20, 8` and then `82, 9`, which
opens the client's public notice window.

## Rules

- A post lives 24 hours. Expired rows stay in the database but are not listed, counted or readable.
- 3 active posts per character, 2550 active posts in total (255 pages of 10).
- Title 1..30 bytes, content 1..151 bytes, author name 1..14 bytes. Control bytes are rejected; content may hold
  line breaks and tabs.
- An identical active post by the same author returns the existing one (safe retry).
- Ids come from `message_board_sequence` and are never reused. Only the author can delete.
- Storage: `message_board_posts` / `message_board_sequence` in the character database; text columns hold Base64 of
  the raw client bytes.

## Packets

| Direction | Bytes | Meaning |
|---|---|---|
| C -> S | `82, 1, titleLen(u32), title, contentLen(u32), content[, 1]` | post |
| C -> S | `82, 2[, page[, 1]]` | page request (default page 1) |
| C -> S | `82, 3, id(u32)` or `82, 3, id(u32), page(u32), date(f64), 1` | read |
| C -> S | `82, 4, id(u32)` (same two shapes) | delete |
| S -> C | `82, 4, count(u32), page` | pagination |
| S -> C | `82, 1, [authorLen(u32), author, titleLen(u32), title, date(f64), authorId(u32), id(u32)]*` | page entries |
| S -> C | `82, 2, authorLen(u32), author, titleLen(u32), title, date(f64), contentLen(u32), authorId(u32), content` | entry |
| S -> C | `82, 5` / `82, 6` / `82, 7` | author limit / board full / missing |
| S -> C | `82, 8, 1` | posted (then page 1) |
| S -> C | `82, 9` | open the window |

`date` is an OLE automation date: `unixMillis / 86400000 + 25569`.

## Not verified

Not tested with a live client. Tests cover text validation, paging maths and the byte layout
(`tests/Run-MessageBoardTests.ps1`); the database methods and the AC82 handler have no automated tests.
