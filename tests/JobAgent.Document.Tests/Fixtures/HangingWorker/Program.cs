var markerIndex = Array.IndexOf(args, "--pid-file");
if (markerIndex >= 0 && markerIndex + 1 < args.Length)
    await File.WriteAllTextAsync(args[markerIndex + 1], Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

var outputIndex = Array.IndexOf(args, "--stdout-bytes");
if (outputIndex >= 0 && outputIndex + 1 < args.Length)
{
    var count = int.Parse(args[outputIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
    var chunk = new string('x', Math.Min(count, 8192));
    while (count > 0)
    {
        var write = Math.Min(count, chunk.Length);
        await Console.Out.WriteAsync(chunk.AsMemory(0, write));
        await Console.Out.FlushAsync();
        count -= write;
    }
}

await Task.Delay(Timeout.InfiniteTimeSpan);
