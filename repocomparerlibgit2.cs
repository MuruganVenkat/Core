using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class GitRepoComparerLibGit2
{
    public class ComparisonResult
    {
        public bool AreIdentical { get; set; }
        public List<string> Differences { get; set; } = new List<string>();
        public Dictionary<string, int> CommitCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, List<string>> Branches { get; set; } = new Dictionary<string, List<string>>();
    }
    
    public ComparisonResult CompareRepositories(string adoRepoUrl, string githubRepoUrl, 
        string localBasePath, CloneOptions cloneOptions = null)
    {
        var result = new ComparisonResult();
        
        try
        {
            // Setup paths
            string adoPath = Path.Combine(localBasePath, "ado_repo");
            string githubPath = Path.Combine(localBasePath, "github_repo");
            
            // Clone or update repositories
            var adoRepo = CloneOrUpdateRepository(adoRepoUrl, adoPath, cloneOptions);
            var githubRepo = CloneOrUpdateRepository(githubRepoUrl, githubPath, cloneOptions);
            
            // Compare repositories
            CompareCommitHistories(adoRepo, githubRepo, result);
            CompareBranches(adoRepo, githubRepo, result);
            CompareFileStructure(adoRepo, githubRepo, result);
            CompareTags(adoRepo, githubRepo, result);
            
            // Cleanup
            adoRepo?.Dispose();
            githubRepo?.Dispose();
            
            result.AreIdentical = result.Differences.Count == 0;
        }
        catch (Exception ex)
        {
            result.Differences.Add($"Error during comparison: {ex.Message}");
        }
        
        return result;
    }
    
    private Repository CloneOrUpdateRepository(string repoUrl, string localPath, CloneOptions options = null)
    {
        if (Directory.Exists(localPath) && Directory.Exists(Path.Combine(localPath, ".git")))
        {
            Console.WriteLine($"Repository exists at {localPath}. Opening existing repository...");
            var repo = new Repository(localPath);
            
            // Pull latest changes
            try
            {
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, options?.FetchOptions, "Fetching updates");
                Console.WriteLine("Repository updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not update repository: {ex.Message}");
            }
            
            return repo;
        }
        else
        {
            Console.WriteLine($"Cloning repository from {repoUrl} to {localPath}...");
            
            if (Directory.Exists(localPath))
                Directory.Delete(localPath, true);
            
            return new Repository(Repository.Clone(repoUrl, localPath, options));
        }
    }
    
    private void CompareCommitHistories(Repository adoRepo, Repository githubRepo, ComparisonResult result)
    {
        Console.WriteLine("Comparing commit histories...");
        
        var adoCommits = adoRepo.Commits.Take(100).ToList();
        var githubCommits = githubRepo.Commits.Take(100).ToList();
        
        result.CommitCounts["ADO"] = adoCommits.Count;
        result.CommitCounts["GitHub"] = githubCommits.Count;
        
        Console.WriteLine($"ADO Repository: {adoCommits.Count} commits (showing last 100)");
        Console.WriteLine($"GitHub Repository: {githubCommits.Count} commits (showing last 100)");
        
        // Compare commit SHAs
        var adoShas = new HashSet<string>(adoCommits.Select(c => c.Sha));
        var githubShas = new HashSet<string>(githubCommits.Select(c => c.Sha));
        
        var uniqueToAdo = adoShas.Except(githubShas).ToList();
        var uniqueToGithub = githubShas.Except(adoShas).ToList();
        
        if (uniqueToAdo.Any())
        {
            result.Differences.Add($"Commits unique to ADO: {uniqueToAdo.Count}");
        }
        
        if (uniqueToGithub.Any())
        {
            result.Differences.Add($"Commits unique to GitHub: {uniqueToGithub.Count}");
        }
        
        // Compare latest commit
        if (adoCommits.Any() && githubCommits.Any())
        {
            var latestAdo = adoCommits.First();
            var latestGithub = githubCommits.First();
            
            if (latestAdo.Sha != latestGithub.Sha)
            {
                result.Differences.Add("Latest commits differ between repositories");
            }
        }
    }
    
    private void CompareBranches(Repository adoRepo, Repository githubRepo, ComparisonResult result)
    {
        Console.WriteLine("Comparing branches...");
        
        var adoBranches = adoRepo.Branches.Select(b => b.FriendlyName).OrderBy(x => x).ToList();
        var githubBranches = githubRepo.Branches.Select(b => b.FriendlyName).OrderBy(x => x).ToList();
        
        result.Branches["ADO"] = adoBranches;
        result.Branches["GitHub"] = githubBranches;
        
        Console.WriteLine($"ADO branches: {string.Join(", ", adoBranches)}");
        Console.WriteLine($"GitHub branches: {string.Join(", ", githubBranches)}");
        
        var uniqueToAdo = adoBranches.Except(githubBranches).ToList();
        var uniqueToGithub = githubBranches.Except(adoBranches).ToList();
        
        if (uniqueToAdo.Any())
        {
            result.Differences.Add($"Branches unique to ADO: {string.Join(", ", uniqueToAdo)}");
        }
        
        if (uniqueToGithub.Any())
        {
            result.Differences.Add($"Branches unique to GitHub: {string.Join(", ", uniqueToGithub)}");
        }
    }
    
    private void CompareFileStructure(Repository adoRepo, Repository githubRepo, ComparisonResult result)
    {
        Console.WriteLine("Comparing file structures...");
        
        // Get HEAD commits
        var adoHead = adoRepo.Head.Tip;
        var githubHead = githubRepo.Head.Tip;
        
        if (adoHead == null || githubHead == null)
        {
            result.Differences.Add("One or both repositories have no commits");
            return;
        }
        
        // Get file trees
        var adoFiles = GetFileList(adoHead.Tree);
        var githubFiles = GetFileList(githubHead.Tree);
        
        Console.WriteLine($"ADO Repository files: {adoFiles.Count}");
        Console.WriteLine($"GitHub Repository files: {githubFiles.Count}");
        
        var uniqueToAdo = adoFiles.Keys.Except(githubFiles.Keys).ToList();
        var uniqueToGithub = githubFiles.Keys.Except(adoFiles.Keys).ToList();
        
        if (uniqueToAdo.Any())
        {
            result.Differences.Add($"Files unique to ADO: {uniqueToAdo.Count} files");
        }
        
        if (uniqueToGithub.Any())
        {
            result.Differences.Add($"Files unique to GitHub: {uniqueToGithub.Count} files");
        }
        
        // Compare file contents for common files
        var commonFiles = adoFiles.Keys.Intersect(githubFiles.Keys).ToList();
        int differentFiles = 0;
        
        foreach (var fileName in commonFiles)
        {
            if (adoFiles[fileName] != githubFiles[fileName])
            {
                differentFiles++;
            }
        }
        
        if (differentFiles > 0)
        {
            result.Differences.Add($"Files with different content: {differentFiles} files");
        }
    }
    
    private Dictionary<string, string> GetFileList(Tree tree, string basePath = "")
    {
        var files = new Dictionary<string, string>();
        
        foreach (var entry in tree)
        {
            var fullPath = string.IsNullOrEmpty(basePath) ? entry.Name : $"{basePath}/{entry.Name}";
            
            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                var subTree = (Tree)entry.Target;
                var subFiles = GetFileList(subTree, fullPath);
                foreach (var subFile in subFiles)
                {
                    files[subFile.Key] = subFile.Value;
                }
            }
            else if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)entry.Target;
                files[fullPath] = blob.Sha;
            }
        }
        
        return files;
    }
    
    private void CompareTags(Repository adoRepo, Repository githubRepo, ComparisonResult result)
    {
        Console.WriteLine("Comparing tags...");
        
        var adoTags = adoRepo.Tags.Select(t => t.FriendlyName).OrderBy(x => x).ToList();
        var githubTags = githubRepo.Tags.Select(t => t.FriendlyName).OrderBy(x => x).ToList();
        
        Console.WriteLine($"ADO tags: {string.Join(", ", adoTags)}");
        Console.WriteLine($"GitHub tags: {string.Join(", ", githubTags)}");
        
        var uniqueToAdo = adoTags.Except(githubTags).ToList();
        var uniqueToGithub = githubTags.Except(adoTags).ToList();
        
        if (uniqueToAdo.Any())
        {
            result.Differences.Add($"Tags unique to ADO: {string.Join(", ", uniqueToAdo)}");
        }
        
        if (uniqueToGithub.Any())
        {
            result.Differences.Add($"Tags unique to GitHub: {string.Join(", ", uniqueToGithub)}");
        }
    }
}

// Usage example
class Program
{
    static void Main(string[] args)
    {
        var comparer = new GitRepoComparerLibGit2();
        
        // Configure clone options if needed (for authentication, etc.)
        var cloneOptions = new CloneOptions
        {
            // CredentialsProvider = (url, user, cred) => new UsernamePasswordCredentials
            // {
            //     Username = "your-username",
            //     Password = "your-token"
            // }
        };
        
        string adoRepoUrl = "https://dev.azure.com/yourorg/yourproject/_git/yourrepo";
        string githubRepoUrl = "https://github.com/username/repository.git";
        string localBasePath = @"C:\temp\repo_comparison";
        
        var result = comparer.CompareRepositories(adoRepoUrl, githubRepoUrl, localBasePath, cloneOptions);
        
        Console.WriteLine("\n=== Comparison Results ===");
        Console.WriteLine($"Repositories are identical: {result.AreIdentical}");
        
        if (result.Differences.Any())
        {
            Console.WriteLine("\nDifferences found:");
            foreach (var diff in result.Differences)
            {
                Console.WriteLine($"- {diff}");
            }
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}

// Don't forget to install LibGit2Sharp NuGet package:
// Install-Package LibGit2Sharp
