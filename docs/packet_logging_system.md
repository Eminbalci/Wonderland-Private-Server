# Comprehensive Packet Logging System Specification

## 1. Overview
To facilitate rapid debugging and packet protocol reverse-engineering, the socket dispatcher now outputs the full raw hex payload for **every incoming packet** regardless of whether an ActionCode handler is registered.

## 2. Log Output Format
Incoming packets are printed in the following structured format:
```text
[RECV PKT] [<CharName>] AC=<ActionCode>, Sub=<SubCode> (Handler: <HandlerClass>) Len=<Length> Hex: <XX XX XX ...>
```

## 3. Implementation
- [`Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs) `ProcessSocket(IPacket g)`: Formats and prints the raw hex stream via `BitConverter.ToString(p.Buffer).Replace("-", " ")` for both handled and unhandled packets.
