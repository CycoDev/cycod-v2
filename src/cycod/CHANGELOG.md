# Changelog

## [Unreleased]
### Added
- MultiSessionDebugTools: New debugging capabilities (AttachDebugSession, EvaluateExpression, GetStackFrames, GetCurrentFrameSourceSnippet) built on DAP (netcoredbg).
- Protocol constants: Added EvaluateCommand to DapProtocol.

### Changed
- Debugging internals: Expanded MockDapClient to support attach, evaluate, multi-frame stack traces.

### Removed
- DebugSessionTools: Deprecated single-session stub removed in favor of multi-session implementation.

### Notes
- Attach and evaluation require a stopped state and netcoredbg availability. Mock client can be injected for deterministic tests.
- Error responses standardized: {"error":"code", "message":"detail", ...}; success responses use {"status":"ok", ...}.
