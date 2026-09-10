# Automated MCP Discovery and Global Productivity Tooling Specification

## Overview
Automated Model Context Protocol (MCP) server discovery, installation, and invocation layer configured using Smithery CLI, alongside official Anthropic MCP server modules and low-level reverse engineering utilities installed globally.

## Globally Installed Tooling Suite

### 1. Model Context Protocol Tools (Global NPM)
- **`@smithery/cli@4.11.1` (`smithery`)**:
  - Central registry manager for discovering, connecting, and calling 100k+ MCP tools.
  - Commands: `smithery mcp search <term>`, `smithery mcp add <id>`, `smithery tool list`, `smithery tool call`.
- **`@modelcontextprotocol/server-sequential-thinking@2026.8.31`**:
  - Official MCP server providing structured, iterative reasoning for complex system refactoring, protocol analysis, and architectural verification.
  - Executable: `mcp-server-sequential-thinking`
- **`@modelcontextprotocol/server-memory@2026.8.31`**:
  - Official Knowledge Graph memory server enabling structured entity-relation graph retention across sessions.
  - Executable: `mcp-server-memory`

### 2. Database & Protocol Inspection Utilities (Global Python)
- **`sqlite-utils@4.2.1`**:
  - High-performance SQLite inspection and manipulation tool.
  - Commands: `python -m sqlite_utils tables <db> --counts`, `python -m sqlite_utils schema <db>`, `python -m sqlite_utils query <db> "<sql>"`.
- **`dpkt@1.9.8`**:
  - Fast, lightweight binary packet dissecting library for PCAP/PCAPNG network captures.

## Core Commands & Parameters

### 1. Server Discovery via Smithery
```bash
smithery mcp search <term> [--json]
```
- **Parameters:**
  - `term` (string, required): Search query or keyword (e.g., `"sqlite"`, `"github"`, `"postgres"`).
  - `--json` (flag, optional): Emits structured JSON results containing `qualifiedName`, `description`, and `connectionUrl`.
- **Returns:** List of matching MCP servers registered in the Smithery index.

### 2. Tool Introspection & Execution
```bash
# List tools
smithery tool list <connection-id> [--flat]

# Tool schema inspection
smithery tool get <connection-id> <tool-name>

# Tool execution
smithery tool call <connection-id> <tool-name> '<json-args>'
```
- **Parameters:**
  - `connection-id` (string, required): Connected MCP identifier.
  - `tool-name` (string, required): Specific tool method.
  - `json-args` (JSON string, required): Serialized parameter payload adhering to tool schema.

### 3. Database Introspection
```bash
# Check tables and row counts
python -m sqlite_utils tables Database.sqlite --counts

# Execute query with tabular/JSON output
python -m sqlite_utils query server.db "SELECT * FROM sqlite_master;"
```

## Exceptions & Edge Cases

1. **Python 3.14 Compatibility Limitations:**
   - Alternative MCP package managers written in Python (such as `mcpm`) rely on `pydantic-core` / `pyo3-ffi`, which currently lack precompiled wheels for Python 3.14.
   - **Resolution:** Node.js-based `@smithery/cli` along with official Node-based `@modelcontextprotocol/*` packages are used.

2. **Authentication Flow (`auth_required`):**
   - Certain MCP servers require OAuth or API tokens. When `smithery mcp add` triggers an `auth_required` state, the user must approve the authentication URL before `smithery tool call` can succeed.

3. **Client Configuration Reload:**
   - Dynamically added local MCP servers to static client configurations require client reload. Direct CLI tool calls via `smithery tool call` bypass client reboots and execute immediately.
