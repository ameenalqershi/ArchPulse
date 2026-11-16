# T011 MCP symbol-context neighbor limit (2026-11-16)

Stacked on #94 and #91. The MCP SymbolContext tool previously propagated the
new 10k full-list neighbor limit exception without a structured MCP failure.
Now it catches the bounded failure and returns a generic user-level error.
The integration-style test persists 10,001 relations and checks error semantics,
lack of structured data, and no source file disclosure.

This does not implement direct MCP pagination, bound snapshot load cost or prove
end-to-end response latency. Human review and hosted CI required.
