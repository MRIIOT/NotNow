using NotNow.Core.Commands.Execution;
using NotNow.Core.Commands.Framework;

namespace NotNow.Core.Services;

public interface ICommandPostingService
{
    Task<bool> PostCommandToGitHubAsync(int issueNumber, string commandText, ExecutionResult result);
}