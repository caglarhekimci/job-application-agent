# W07 typed synthetic form controls

**Implemented and focused-test date:** 2026-09-20
**Scope:** fixed loopback synthetic fixture and managed browser session only. No live career site, personal data, employer submission, model-selected selector, arbitrary URL/path, or new network origin was used.

## Implemented contract

`IBrowserSession` preserves the existing high-level preparation, readiness, submission, and disposal calls used by the workflow. Inside the managed implementation, a closed typed action set covers navigation, visible-control inspection, text fill, select, checkbox, radio, approved-resume upload, and step advancement. Its fields and steps are enums; callers cannot provide a selector, URL, file path, approval token, or free-form browser action.

The original two-step fixture remains supported. An opt-in extended fixture adds:

- `preference.work.mode`: exactly `remote` or `hybrid`;
- `preference.travel`: exactly `true` or `false`;
- `preference.contact.method`: exactly `email` or `phone`;
- `preference.contact.window`: required only for `phone`, nonblank, maximum 80 characters.

These values can come only from the already payload-bound `ApplicationDraft.Answers`. The phone choice is a non-sensitive fixture preference; it does not create, infer, or transmit a telephone-number field.

The extended fixture has three steps: contact details, questions/preferences, then resume/final review. The call-window input is hidden and disabled for email and becomes visible, enabled, and required for phone.

## Fail-closed checks

Before launching Playwright, the session rejects unknown answer keys and incomplete or invalid preference groups. A missing conditional phone answer returns `NeedsInput`.

At each form step, the session validates the complete fixed control contract: recognized names, exact types and counts, required state, disabled state, visibility, select option values, checkbox value, radio values/order, and the conditional text limit. A new visible required control returns `NeedsInput`; unknown hidden/optional controls, duplicate radio options, changed select options, unexpected disabled controls, and an unbounded conditional input return `FormChanged`.

The outbound interceptor still permits only the fixed loopback recipient. It parses the multipart request before forwarding with zero redirects and zero retries, requires exactly the approved field set and resume bytes, and treats an unchecked checkbox as field absence. Receipt verification compares every applicable preference, including an explicit false travel value, before returning verified submission evidence.

## TDD and verification evidence

The RED run failed at compilation because the typed session contract and synthetic control fixture did not yet exist:

```text
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~FormControlTests"

Failed: SyntheticFormMutation and the new session/fixture contract were missing.
```

After implementation, the focused suite passed **12/12**:

```text
Passed: 12, Failed: 0, Skipped: 0, Total: 12
artifacts/verification/w07-controls-green/form-controls-green-3.trx
```

The cases cover both allowed select values, checked and unchecked checkbox serialization, both radio choices, the conditional third-step text value, missing conditional input, unknown required input, changed option, hidden field, disabled control, duplicate radio value, missing maximum length, unknown draft key, and invalid draft values.

The existing browser policy and adversarial suites then passed **18/18**:

```text
Passed: 18, Failed: 0, Skipped: 0, Total: 18
artifacts/verification/w07-controls-green/form-controls-compat.trx
```

This compatibility run covers the pre-existing approval, recipient/origin, redirect/retry, exact-body, auto-submit, tampering, socket, malformed-receipt, and changed-form regressions selected by `BrowserPolicyTests|AdversarialFormTests`.

## Limits

- This adapter recognizes one versioned synthetic form contract. It does not claim generic coverage of employer forms or live-site compatibility.
- Unknown-question review and answer collection in the local UI are outside this change. The browser stops with `NeedsInput`; it does not invent an answer.
- The action types are trusted in-process implementation contracts. They are not exposed as model, MCP, HTTP, shell, or arbitrary browser inputs.
- Release-wide verification belongs to the coordinated repository verification run; this evidence records only the focused suites above.
