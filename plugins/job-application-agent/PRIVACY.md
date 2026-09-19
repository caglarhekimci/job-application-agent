# Local Inspector data disclosure

This package launches an MCP process on the local Windows computer. It supplies
no hosted service, account system, analytics endpoint, or paid model client.

The configured local runtime can contain a confirmed profile and synthetic
application records. Profile payloads are protected for the current Windows user.
The companion controls retention, export and deletion; removing the plugin does
not erase the companion's data. The plugin does not delete or edit those records.

Tool responses are supplied to the model host that invoked them. This is a data
transfer even though the database stays local. The host can retain prompts,
responses and logs under its own account settings and policies.

- Capability responses include mode, feature restrictions and supported tool names.
- Profile responses include the caller-provided profile reference, profile version
  and currently verified professional skill names.
- Application responses include the caller-provided application reference, state,
  job reference, and receipt/application identifiers and verification time when
  receipt evidence exists.

The reviewed tool responses do not include names, email addresses, salary targets,
private salary minimums, raw CV text, CV bytes, browser sessions or credentials.
Do not place personal data in record identifiers or job references. Record
references are still contextual data and should not be published unnecessarily.

The runtime directory is trusted process configuration, not a model argument.
Only enable this plugin in a host and Windows account you trust. Revoking the
plugin connection stops future tool use; it cannot recall responses already sent
to the host. This local disclosure is not a completed public-store privacy policy
for a future hosted service.
