# Expanded synthetic B0 benchmark

`datasets/expanded-synthetic-v1.json` is an MIT-licensed, synthetic-only dataset for the deterministic B0 rule runner. It contains 12 distinct profile archetypes. Each archetype is evaluated against 20 split-owned answer scenarios and five split-owned job scenarios, materializing 240 question cases and 60 job cases. The dataset has 40 question families and 10 job families in total: half belong only to development and half only to test.

The compact file stores each family rationale once and an explicit expected status/value for every profile archetype in that family's split. The loader materializes each compatible profile/family combination as an individually identified case. Validation requires all six split archetypes in every family, unique scenario and template families, exact target counts, `synthetic: true`, and disjoint development/test profile and template-family identifiers.

Profile archetypes cover confirmed and unconfirmed contact data, one through five years of professional experience, personal-project-only and internship-only experience, expired evidence, monthly-net and annual-gross salary semantics, absent salary confirmation, Turkish locale, and scoped answer memory.

Development question families cover contact, salary unit distinctions, current versus expected salary, professional experience, unknown keys, sensitive and attested questions, company/application scope, Turkish/English/unsupported languages, expired and evidence-backed memories, and field-length limits. Test families use separate semantic keys and scenarios for strict field limits, certification, health/legal manual entry, notice period, travel, application cover text, location, future expiry, missing/unverified evidence, and exact/over-length fields. Development and test job families use different thresholds and requirement types.

The development/test split prevents profile-family and actual reusable template-family leakage. Both partitions still exercise the same Core implementation and related rule categories, so this is a regression partition rather than evidence of independent holdout generalization.

`ExpandedFixtureEvaluationRunner` makes no network or model call. B0 is the only executed baseline. B1 and B2 remain `NotRun`; fixture results must not be presented as language-model quality.
