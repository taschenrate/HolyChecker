using HolyChecker.Models;

namespace HolyChecker.Services;

public interface IExternalToolService
{
    Task LaunchAsync(ExternalTool tool, CancellationToken token);
}