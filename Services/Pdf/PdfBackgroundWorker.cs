using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SocialCalc.Web.Services.Pdf;

public class PdfBackgroundWorker : BackgroundService
{
    private readonly IPdfJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PdfBackgroundWorker> _logger;

    public PdfBackgroundWorker(IPdfJobQueue queue, IServiceProvider serviceProvider, ILogger<PdfBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PDF Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                
                _queue.UpdateJobState(job.JobId, state => state.Status = PdfJobStatus.Processing);
                
                _logger.LogInformation("Processing PDF Job: {JobId}", job.JobId);

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        var pdfEngine = scope.ServiceProvider.GetRequiredService<IPdfRenderEngine>();

                        var tempDir = Path.Combine(Path.GetTempPath(), "SocialCalcPdf");
                        Directory.CreateDirectory(tempDir);
                        var tempFilePath = Path.Combine(tempDir, $"export_{job.JobId}.pdf");

                        await pdfEngine.RenderHtmlToPdfFileAsync(job.HtmlContent, tempFilePath, job.PrintSettings);

                        _queue.UpdateJobState(job.JobId, state => 
                        {
                            state.Status = PdfJobStatus.Completed;
                            state.OutputFilePath = tempFilePath;
                        });
                        
                        _logger.LogInformation("Completed PDF Job: {JobId}", job.JobId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing PDF Job: {JobId}", job.JobId);
                        _queue.UpdateJobState(job.JobId, state => 
                        {
                            state.Status = PdfJobStatus.Failed;
                            state.ErrorMessage = ex.Message;
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dequeueing PDF job.");
            }
        }
    }
}
