using System.Threading.Channels;
using System.Threading.Tasks;

namespace SocialCalc.Web.Services.Pdf;

public enum PdfJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class PdfJobRequest
{
    public string JobId { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public PdfRenderOptions? PrintSettings { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
}

public class PdfJobState
{
    public string JobId { get; set; } = string.Empty;
    public PdfJobStatus Status { get; set; } = PdfJobStatus.Pending;
    public string OutputFilePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
}

public interface IPdfJobQueue
{
    ValueTask QueueJobAsync(PdfJobRequest job);
    ValueTask<PdfJobRequest> DequeueAsync(CancellationToken cancellationToken);
    void UpdateJobState(string jobId, Action<PdfJobState> updateAction);
    PdfJobState? GetJobState(string jobId);
}

public class PdfJobQueue : IPdfJobQueue
{
    private readonly Channel<PdfJobRequest> _queue;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PdfJobState> _jobStates;

    public PdfJobQueue()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<PdfJobRequest>(options);
        _jobStates = new System.Collections.Concurrent.ConcurrentDictionary<string, PdfJobState>();
    }

    public async ValueTask QueueJobAsync(PdfJobRequest job)
    {
        _jobStates[job.JobId] = new PdfJobState { JobId = job.JobId, Status = PdfJobStatus.Pending, OriginalFileName = job.OriginalFileName };
        await _queue.Writer.WriteAsync(job);
    }

    public async ValueTask<PdfJobRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    public void UpdateJobState(string jobId, Action<PdfJobState> updateAction)
    {
        if (_jobStates.TryGetValue(jobId, out var state))
        {
            updateAction(state);
        }
    }

    public PdfJobState? GetJobState(string jobId)
    {
        _jobStates.TryGetValue(jobId, out var state);
        return state;
    }
}
