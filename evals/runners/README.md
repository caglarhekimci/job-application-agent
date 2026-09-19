# Deterministic fixture runner

The runner is in `JobAgent.Core.Evaluation` so the CLI can call it without a second
implementation:

```csharp
var options = new EvaluationRunOptions(codeRevision, scenarioAsOf, runAt);
var report = FixtureEvaluationRunner.RunFile(datasetPath, options);
Console.WriteLine(EvaluationJson.Serialize(report));
```

The caller supplies the code revision, fixed scenario time, and actual run timestamp. Defaults identify the execution
as mode `fixture`, model `deterministic-rules-v1`, and prompt/rule contract
`fixture-rules-v1`. The serialized report contains no answer values. A real-model run
is outside this runner and is reported as `NotRun`.
