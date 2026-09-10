---
name: smithery-mcp-manager
description: Automatically discover, search, connect, and invoke MCP tools using the Smithery CLI. Use when a task requires an MCP server, database connector, API integration, or external tool that is not yet loaded.
---

# Smithery MCP Manager

Use `@smithery/cli` (`smithery`) to search for and utilize MCP servers on demand.

## Search for Tools / Servers
```bash
smithery mcp search "<query>" --json
```

## Connect to Server
```bash
smithery mcp add "<qualified-name-or-url>" --id <alias>
```

## List & Call Tools
```bash
# List tools under connection
smithery tool list <alias>

# Call tool
smithery tool call <alias> <tool-name> '<json-payload>'
```
