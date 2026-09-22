# TypeSafe.Sdk — Security

## Authentication

### Method

API key sent as `Authorization: Bearer <key>` on every request. Resolved from `TypeSafeClientOptions.ApiKey`, else `TYPESAFE_API_KEY`. The client throws `TypeSafeException` at construction if neither is set; blank env values are ignored.

### Token/Session Management

No sessions. The key is held in a private field on `Transport`, never exposed as a public property, never logged unredacted (redacted to `Bearer ***<last4>` at debug level).

### Multi-factor Authentication

Not applicable (machine-to-machine).

## Authorization

### Roles & Permissions

| Role | Permissions | Description |
|------|------------|-------------|
| API key holder | Whatever TypeSafe grants the key | The SDK has no authorisation model of its own |

### Enforcement

Server side. The SDK maps 401 to `TypeSafeAuthenticationException` and 403 to `TypeSafePermissionDeniedException`.

## Data Protection

### Encryption

HTTPS by default (`https://api.typesafe.ai`). The SDK does not enforce TLS if `TYPESAFE_BASE_URL` is set to `http://`; that is the consumer's call for local proxies.

### PII Handling

`state`, instructions, and answers are the consumer's data and pass straight through. At `debug` log level request and response **bodies are logged unredacted**, which may include PII; the README and XML docs say so. Default level is `warn`.

### Data Retention

None in the SDK. Buffered `HttpResponseMessage` is kept on the response object for the caller's lifetime of that object.

## API Security

- Protected headers (`Authorization`, `Accept`, `Content-Type`, `User-Agent`, `X-TypeSafe-*`) cannot be overridden by `DefaultHeaders` or per-call headers.
- Input validation: questions must be non-empty; score criteria non-empty; raw questions need a non-empty string `type`; retry policy and timeout values validated.
- Response validation: successful bodies are parsed strictly with field paths; unknown answer types skipped, unknown fields ignored.
- No browser guard equivalent of the JS SDK's `dangerouslyAllowBrowser` (irrelevant for .NET server/desktop; Blazor WebAssembly consumers would expose the key and should not use this directly).

## Secrets Management

Consumers supply the key via environment or their own secret store (Infisical, Azure Key Vault, user secrets). The repo contains no secrets; `.env*` files are not used. Tests use the literal fake key `sk-test-key-1234567890`.

## Known Risks & Mitigations

| Risk | Severity | Mitigation | Status |
|------|----------|------------|--------|
| Debug logging leaks PII in bodies | Medium | Off by default; documented; headers redacted | Accepted |
| Not yet validated against the live API (stub tests only) | Medium | `tests/TypeSafe.Sdk.IntegrationTests` — three `[SkippableFact]` live tests gated on `TYPESAFE_API_KEY`, skipped with "TYPESAFE_API_KEY is not set; live tests skipped" when it is absent. The bad-key test (401 + request id) has been observed passing against the live API from this machine using only a bogus key; the models-list and System One tests have not yet run with a real key | Mitigated pending first keyed run |
| `object?` inputs use reflection serialisation (trim/AOT unsafe, silent camelCase renaming) | Low | Replace with `Content` type (epic TSDK-2) | Planned |
| Supplied `HttpClient.Timeout` inherited as the SDK timeout (100 s default) | Low | Documented; consider explicit default | Open |

The live-validation risk closes — status `Mitigated` — once `Models_list_returns_at_least_one_model_with_a_name` and
`System_one_answers_a_noul_a_choice_and_a_score_question` have both passed against the live API with a real key. As of
2026-09-18 they have not: no key has been available, so those two tests have only ever been skipped. The suite runs in
CI, where no key is configured and all three tests skip by design; no secret is added to CI and the key is never logged.

## Incident Response

Contact: Ryan (Biztactix). For a leaked API key, rotate it in the TypeSafe AI console; the SDK has no cached credentials to purge.

---
*Last reviewed: 2026-09-18*
