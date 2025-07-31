using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace GitMigration
{
    // Main result class
    public class MigrationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> ExecutedSteps { get; set; } = new List<string>();

        public static MigrationResult Success(List<string> steps = null)
        {
            return new MigrationResult 
            { 
                IsSuccess = true, 
                ExecutedSteps = steps ?? new List<string>() 
            };
        }

        public static MigrationResult Failure(string errorMessage, List<string> steps = null)
        {
            return new MigrationResult 
            { 
                IsSuccess = false, 
                ErrorMessage = errorMessage,
                ExecutedSteps = steps ?? new List<string>()
            };
        }
    }

    // Git Service Interface
    public interface IGitService
    {
        Task<bool> CloneRepositoryAsync(string sourceUrl, string directory);
        Task<bool> PullAllBranchesAsync();
        Task<bool> RenameBranchAsync(string oldName, string newName);
        Task<bool> SetRemoteUrlAsync(string remoteName, string url);
        Task<bool> PullWithUnrelatedHistoriesAsync(string remote, string branch);
        Task<bool> CommitMergeAsync(string message);
        Task<bool> PushAllBranchesAsync(string remote);
        Task<bool> PushAllTagsAsync(string remote);
    }

    // File System Service Interface
    public interface IFileSystemService
    {
        bool ChangeDirectory(string path);
        bool DirectoryExists(string path);
        string GetCurrentDirectory();
    }

    // Git Service Implementation
    public class GitService : IGitService
    {
        public async Task<bool> CloneRepositoryAsync(string sourceUrl, string directory)
        {
            try
            {
                var result = await ExecuteGitCommandAsync($"clone {sourceUrl} {directory}");
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cloning repository: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PullAllBranchesAsync()
        {
            try
            {
                // Fetch all remote branches
                var fetchResult = await ExecuteGitCommandAsync("fetch --all");
                if (!fetchResult.IsSuccess)
                    return false;

                // Get all remote branches
                var branchResult = await ExecuteGitCommandAsync("branch -r");
                if (!branchResult.IsSuccess)
                    return false;

                var remoteBranches = branchResult.Output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(b => b.Trim())
                    .Where(b => !b.Contains("HEAD") && b.Contains("origin/"))
                    .Select(b => b.Replace("origin/", ""))
                    .ToList();

                // Create local tracking branches for each remote branch
                foreach (var branch in remoteBranches)
                {
                    if (branch != "main" && branch != "master")
                    {
                        await ExecuteGitCommandAsync($"checkout -b {branch} origin/{branch}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pulling all branches: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RenameBranchAsync(string oldName, string newName)
        {
            try
            {
                var result = await ExecuteGitCommandAsync($"branch -m {oldName} {newName}");
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming branch: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetRemoteUrlAsync(string remoteName, string url)
        {
            try
            {
                var result = await ExecuteGitCommandAsync($"remote set-url {remoteName} {url}");
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting remote URL: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PullWithUnrelatedHistoriesAsync(string remote, string branch)
        {
            try
            {
                var result = await ExecuteGitCommandAsync($"pull --no-rebase {remote} {branch} --allow-unrelated-histories");
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pulling with unrelated histories: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CommitMergeAsync(string message)
        {
            try
            {
                // Check if there are any conflicts or uncommitted changes
                var statusResult = await ExecuteGitCommandAsync("status --porcelain");
                
                if (!string.IsNullOrEmpty(statusResult.Output))
                {
                    // If there are changes, commit them
                    var addResult = await ExecuteGitCommandAsync("add .");
                    if (!addResult.IsSuccess)
                        return false;

                    var commitResult = await ExecuteGitCommandAsync($"commit -m \"{message}\"");
                    return commitResult.IsSuccess;
                }

                return true; // No changes to commit
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error committing merge: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PushAllBranchesAsync(string remote)
        {
            try
            {
                var result = await ExecuteGitCommandAsync($"push --all {remote}");
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pushing all branches: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PushAllTagsAsync(string remote)
        {
            try
            {
                var result = await ExecuteGitCommandAsync($"push --tags {remote}");
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pushing all tags: {ex.Message}");
                return false;
            }
        }

        private async Task<CommandResult> ExecuteGitCommandAsync(string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "git";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                var result = new CommandResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    IsSuccess = process.ExitCode == 0
                };

                Console.WriteLine($"Git command: git {arguments}");
                Console.WriteLine($"Exit code: {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Output))
                    Console.WriteLine($"Output: {result.Output}");
                if (!string.IsNullOrEmpty(result.Error))
                    Console.WriteLine($"Error: {result.Error}");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception executing git command: {ex.Message}");
                return new CommandResult
                {
                    ExitCode = -1,
                    Error = ex.Message,
                    IsSuccess = false
                };
            }
        }
    }

    // File System Service Implementation
    public class FileSystemService : IFileSystemService
    {
        public bool ChangeDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Console.WriteLine($"Directory does not exist: {path}");
                    return false;
                }

                Directory.SetCurrentDirectory(path);
                Console.WriteLine($"Changed directory to: {Directory.GetCurrentDirectory()}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error changing directory: {ex.Message}");
                return false;
            }
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }
    }

    // Main Git Repository Migrator
    public class GitRepositoryMigrator
    {
        private readonly IGitService _gitService;
        private readonly IFileSystemService _fileSystemService;

        public GitRepositoryMigrator(IGitService gitService, IFileSystemService fileSystemService)
        {
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        }

        public async Task<MigrationResult> MigrateRepositoryAsync(
            string sourceUrl, 
            string destinationUrl, 
            string repositoryDirectory, 
            string commitMessage)
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(sourceUrl))
                throw new ArgumentException("Source URL cannot be null or empty", nameof(sourceUrl));
            if (string.IsNullOrWhiteSpace(destinationUrl))
                throw new ArgumentException("Destination URL cannot be null or empty", nameof(destinationUrl));
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                throw new ArgumentException("Repository directory cannot be null or empty", nameof(repositoryDirectory));
            if (string.IsNullOrWhiteSpace(commitMessage))
                throw new ArgumentException("Commit message cannot be null or empty", nameof(commitMessage));

            var executedSteps = new List<string>();
            var originalDirectory = _fileSystemService.GetCurrentDirectory();

            try
            {
                // Step 1: Git clone
                Console.WriteLine($"Step 1: Cloning repository from {sourceUrl}");
                if (!await _gitService.CloneRepositoryAsync(sourceUrl, repositoryDirectory))
                {
                    return MigrationResult.Failure("Failed to clone repository from source URL", executedSteps);
                }
                executedSteps.Add("Repository cloned successfully");

                // Step 2: Change directory
                Console.WriteLine($"Step 2: Changing directory to {repositoryDirectory}");
                if (!_fileSystemService.ChangeDirectory(repositoryDirectory))
                {
                    return MigrationResult.Failure("Failed to change directory to repository", executedSteps);
                }
                executedSteps.Add("Changed to repository directory");

                // Step 3: Pull all branches
                Console.WriteLine("Step 3: Pulling all branches");
                if (!await _gitService.PullAllBranchesAsync())
                {
                    return MigrationResult.Failure("Failed to pull all branches", executedSteps);
                }
                executedSteps.Add("All branches pulled");

                // Step 4: Rename default branch
                Console.WriteLine("Step 4: Renaming main branch to old-main");
                if (!await _gitService.RenameBranchAsync("main", "old-main"))
                {
                    // This might fail if there's no main branch, which could be acceptable
                    Console.WriteLine("Warning: Could not rename main branch (might not exist)");
                }
                executedSteps.Add("Default branch renamed");

                // Step 5: Set new remote repository
                Console.WriteLine($"Step 5: Setting remote origin to {destinationUrl}");
                if (!await _gitService.SetRemoteUrlAsync("origin", destinationUrl))
                {
                    return MigrationResult.Failure("Failed to set new remote URL", executedSteps);
                }
                executedSteps.Add("Remote URL updated");

                // Step 6: Merge remote main to local
                Console.WriteLine("Step 6: Pulling remote main with unrelated histories");
                if (!await _gitService.PullWithUnrelatedHistoriesAsync("origin", "main"))
                {
                    return MigrationResult.Failure("Failed to pull remote main with unrelated histories", executedSteps);
                }
                executedSteps.Add("Remote main merged");

                // Step 7: Commit merge
                Console.WriteLine($"Step 7: Committing merge with message: {commitMessage}");
                if (!await _gitService.CommitMergeAsync(commitMessage))
                {
                    return MigrationResult.Failure("Failed to commit merge", executedSteps);
                }
                executedSteps.Add("Merge committed");

                // Step 8: Push all branches
                Console.WriteLine("Step 8: Pushing all branches to remote");
                if (!await _gitService.PushAllBranchesAsync("origin"))
                {
                    return MigrationResult.Failure("Failed to push all branches", executedSteps);
                }
                executedSteps.Add("All branches pushed");

                // Step 9: Push all tags
                Console.WriteLine("Step 9: Pushing all tags to remote");
                if (!await _gitService.PushAllTagsAsync("origin"))
                {
                    return MigrationResult.Failure("Failed to push all tags", executedSteps);
                }
                executedSteps.Add("All tags pushed");

                Console.WriteLine("Migration completed successfully!");
                return MigrationResult.Success(executedSteps);
            }
            catch (Exception ex)
            {
                return MigrationResult.Failure($"Unexpected error during migration: {ex.Message}", executedSteps);
            }
            finally
            {
                // Return to original directory
                try
                {
                    _fileSystemService.ChangeDirectory(originalDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not return to original directory: {ex.Message}");
                }
            }
        }
    }

    // Helper class for command execution results
    public class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
    }

    // Console application example
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: GitMigration <sourceUrl> <destinationUrl> <repositoryDirectory> <commitMessage>");
                Console.WriteLine("Example: GitMigration https://github.com/source/repo.git https://ghes.company.com/dest/repo.git my-repo \"Merge commit message\"");
                return;
            }

            var sourceUrl = args[0];
            var destinationUrl = args[1];
            var repositoryDirectory = args[2];
            var commitMessage = args[3];

            var gitService = new GitService();
            var fileSystemService = new FileSystemService();
            var migrator = new GitRepositoryMigrator(gitService, fileSystemService);

            try
            {
                Console.WriteLine("Starting Git repository migration...");
                Console.WriteLine($"Source: {sourceUrl}");
                Console.WriteLine($"Destination: {destinationUrl}");
                Console.WriteLine($"Directory: {repositoryDirectory}");
                Console.WriteLine($"Commit Message: {commitMessage}");
                Console.WriteLine();

                var result = await migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

                if (result.IsSuccess)
                {
                    Console.WriteLine("\n✅ Migration completed successfully!");
                    Console.WriteLine("Executed steps:");
                    foreach (var step in result.ExecutedSteps)
                    {
                        Console.WriteLine($"  - {step}");
                    }
                }
                else
                {
                    Console.WriteLine($"\n❌ Migration failed: {result.ErrorMessage}");
                    if (result.ExecutedSteps.Any())
                    {
                        Console.WriteLine("Completed steps before failure:");
                        foreach (var step in result.ExecutedSteps)
                        {
                            Console.WriteLine($"  - {step}");
                        }
                    }
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n💥 Unexpected error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
