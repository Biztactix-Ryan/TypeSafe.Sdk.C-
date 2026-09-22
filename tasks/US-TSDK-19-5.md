---
archived: false
assignee: claude
claimed_at: null
claimed_by_run: null
created: '2026-09-18'
depends_on: []
id: US-TSDK-19-5
points: 3
status: done
story_id: US-TSDK-19
tags: []
title: Route all transport logging through [LoggerMessage] partial methods with EventIds
updated: '2026-09-18'
---

Create src/TypeSafe.Sdk/Internal/TransportLog.cs: `internal static partial class TransportLog` with [LoggerMessage(EventId = n, Level = ..., Message = "...")] partial methods (the generator ships with Microsoft.Extensions.Logging.Abstractions; it is AOT-safe) for every event Transport emits today: request sending (Method, Endpoint, Attempt), response received (Method, Endpoint, StatusCode, ElapsedMs, RequestId, Attempt), retry scheduled (Attempt, Delay, Reason), connection/timeout error, unknown-answer warning, and the Debug-level redacted headers and bodies. Give each a stable EventId and name (document the table in the file header and in README's Configuration/logging text if it lists log output). Replace every ILogger call in Internal/Transport.cs (and Responses.cs's warn callback path) with these methods; keep message text as close to today's as possible and keep header redaction (`Bearer ***last4`) and Debug body logging behaviour identical. Reduce Internal/Logging.cs (SdkLog) to the minimum-level parsing/filter or remove it if the ILogger's own level filtering suffices; the TYPESAFE_LOG_LEVEL option must keep working. Reconcile LoggingTests expectations only where message wording had to change; never drop an assertion. Acceptance: grep shows no `_logger.Log(`/`LogInformation(`-style calls in Transport outside TransportLog; LoggingTests pass; zero-warning build with the analyzers on.