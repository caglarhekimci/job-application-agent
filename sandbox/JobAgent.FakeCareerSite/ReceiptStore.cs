namespace JobAgent.FakeCareerSite;

public sealed record SyntheticReceipt(string Id, string ApplicationKey, string Salary,
    string ProfessionalYears, string FileName, string ResumeHash);

public sealed class ReceiptStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SyntheticReceipt> records = new();
    public IReadOnlyCollection<SyntheticReceipt> Receipts => records.Values.ToArray();
    public SyntheticReceipt Add(SyntheticReceipt receipt) => records.GetOrAdd(receipt.ApplicationKey, receipt);
    public SyntheticReceipt? Find(string applicationKey) => records.GetValueOrDefault(applicationKey);
}
