# fixture-rules-v1

This identifier describes the deterministic rule fixture contract. It is not a model
prompt and is not sent to any provider.

- Resolve only values supported by the synthetic profile and required local confirmation.
- Abstain on unknown fields and require review for unverified values.
- Count professional experience only from verified professional evidence.
- Classify only a verified submission as success.
- Keep unverified and unknown submission outcomes as `NotSuccess`.
