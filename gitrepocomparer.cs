using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public class GitRepoComparer
{
    private readonly string _workingDirectory;
    
    public GitRepoComparer(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }
    
    public async Task<bool> CompareRepositories(string adoRepoUrl, string githubRepoUrl, string localPath)
    {
        try
        {
            // Create local directory if it doesn't exist
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);
            
            // Clone ADO repository
            string adoPath = Path.Combine(localPath, "ado_repo");
            await CloneRepository(adoRepoUrl, adoPath);
            
            // Clone GitHub repository
            string githubPath = Path.Combine(localPath, "github_repo");
            await CloneRepository(githubRepoUrl, githubPath);
            
            // Compare repositories
            await CompareDirectories(adoPath, githubPath);
            
            // Compare commit histories
            await CompareCommitHistories(adoPath, githubPath);
            
            // Compare branches
            await CompareBranches(adoPath, githubPath);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error comparing repositories: {ex.Message}");
            return false;
        }
    }
    
    private async Task CloneRepository(string repoUrl, string localPath)
    {
        if (Directory.Exists(localPath))
        {
            Console.WriteLine($"Directory {localPath} already exists. Pulling latest changes...");
            await ExecuteGitCommand($"pull origin", localPath);
        }
        else
        {
            Console.WriteLine($"Cloning repository from {repoUrl}...");
            await ExecuteGitCommand($"clone {repoUrl} \"{localPath}\"", _workingDirectory);
        }
    }
    
    private async Task CompareDirectories(string adoPath, string githubPath)
    {
        Console.WriteLine("\n=== Directory Structure Comparison ===");
        
        // Get file lists from both repositories
        var adoFiles = await ExecuteGitCommand("ls-files", adoPath);
        var githubFiles = await ExecuteGitCommand("ls-files", githubPath);
        
        Console.WriteLine($"ADO Repository files:\n{adoFiles}");
        Console.WriteLine($"\nGitHub Repository files:\n{githubFiles}");
        
        // You can add more sophisticated comparison logic here
        if (adoFiles.Trim() == githubFiles.Trim())
        {
            Console.WriteLine("✓ File structures are identical");
        }
        else
        {
            Console.WriteLine("✗ File structures differ");
        }
    }
    
    private async Task CompareCommitHistories(string adoPath, string githubPath)
    {
        Console.WriteLine("\n=== Commit History Comparison ===");
        
        // Get commit logs
        var adoCommits = await ExecuteGitCommand("log --oneline -10", adoPath);
        var githubCommits = await ExecuteGitCommand("log --oneline -10", githubPath);
        
        Console.WriteLine($"ADO Repository - Last 10 commits:\n{adoCommits}");
        Console.WriteLine($"\nGitHub Repository - Last 10 commits:\n{githubCommits}");
        
        // Get total commit count
        var adoCommitCount = await ExecuteGitCommand("rev-list --count HEAD", adoPath);
        var githubCommitCount = await ExecuteGitCommand("rev-list --count HEAD", githubPath);
        
        Console.WriteLine($"\nADO Repository total commits: {adoCommitCount.Trim()}");
        Console.WriteLine($"GitHub Repository total commits: {githubCommitCount.Trim()}");
    }
    
    private async Task CompareBranches(string adoPath, string githubPath)
    {
        Console.WriteLine("\n=== Branch Comparison ===");
        
        var adoBranches = await ExecuteGitCommand("branch -r", adoPath);
        var githubBranches = await ExecuteGitCommand("branch -r", githubPath);
        
        Console.WriteLine($"ADO Repository branches:\n{adoBranches}");
        Console.WriteLine($"\nGitHub Repository branches:\n{githubBranches}");
    }
    
    public async Task CompareSpecificBranches(string adoPath, string githubPath, string branchName)
    {
        Console.WriteLine($"\n=== Comparing branch '{branchName}' ===");
        
        // Checkout specific branch in both repos
        await ExecuteGitCommand($"checkout {branchName}", adoPath);
        await ExecuteGitCommand($"checkout {branchName}", githubPath);
        
        // Compare file differences
        var adoFiles = await GetFileHashes(adoPath);
        var githubFiles = await GetFileHashes(githubPath);
        
        Console.WriteLine($"Comparing file contents on branch '{branchName}'...");
        // Add comparison logic here
    }
    
    private async Task<string> GetFileHashes(string repoPath)
    {
        return await ExecuteGitCommand("ls-files -s", repoPath);
    }
    
    private async Task<string> ExecuteGitCommand(string arguments, string workingDirectory)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process();
        process.StartInfo = processInfo;
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            throw new Exception($"Git command failed: {error}");
        }
        
        return output;
    }
}

// Usage example
class Program
{
    static async Task Main(string[] args)
    {
        var comparer = new GitRepoComparer(@"C:\temp");
        
        string adoRepoUrl = "https://dev.azure.com/yourorg/yourproject/_git/yourrepo";
        string githubRepoUrl = "https://github.com/username/repository.git";
        string localPath = @"C:\temp\repo_comparison";
        
        await comparer.CompareRepositories(adoRepoUrl, githubRepoUrl, localPath);
        
        Console.WriteLine("Comparison completed. Press any key to exit...");
        Console.ReadKey();
    }
}
