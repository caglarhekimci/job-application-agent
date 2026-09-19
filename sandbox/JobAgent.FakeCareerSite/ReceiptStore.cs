namespace JobAgent.FakeCareerSite;

public sealed record SyntheticReceipt(string Id, string ApplicationKey, string Salary,
    string ProfessionalYears, string FileName, string ResumeHash,
    string? WorkMode = null, bool? Travel = null, string? ContactMethod = null,
    string? ContactWindow = null);

public sealed class ReceiptStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SyntheticReceipt> records = new();
    private int requests;
    private int submissionPosts;
    public int Requests => Volatile.Read(ref requests);
    public int SubmissionPosts => Volatile.Read(ref submissionPosts);
    public void RecordRequest() => Interlocked.Increment(ref requests);
    public void RecordSubmission() => Interlocked.Increment(ref submissionPosts);
    public IReadOnlyCollection<SyntheticReceipt> Receipts => records.Values.ToArray();
    public SyntheticReceipt Add(SyntheticReceipt receipt) => records.GetOrAdd(receipt.ApplicationKey, receipt);
    public SyntheticReceipt? Find(string applicationKey) => records.GetValueOrDefault(applicationKey);
}
