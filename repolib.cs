// File: Models/MigrationConfig.cs
using System.Collections.Generic;

namespace RepoMigrationTool.Models
{
    public class MigrationConfig
    {
        public string SourceUrl { get; set; }
        public string DestinationUrl { get; set; }
        public string LocalPath { get; set; }
        public string CommitMessage { get; set; }
        public CredentialsConfig Source { get; set; }
        public CredentialsConfig Destination { get; set; }
        public MigrationOptions Options { get; set; } = new MigrationOptions();
    }

    public class CredentialsConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string PersonalAccessToken { get; set; }
    }

    public class MigrationOptions
    {
        public bool CleanupLocalRepo { get; set; } = true;
        public bool RenameMainBranch { get; set; } = true;
        public string OldMainBranchName { get; set; } = "old-main";
        public bool AllowUnrelatedHistories { get; set; } = true;
        public List<string> ExcludeBranches { get; set; } = new List<string>();
        public List<string> ExcludeTags { get; set; } = new List<string>();
    }
}

// File: Interfaces/IConfigurationService.cs
using RepoMigrationTool.Models;
using System.Threading.Tasks;

namespace RepoMigrationTool.Interfaces
{
    public interface IConfigurationService
    {
        Task<MigrationConfig> LoadConfigAsync(string configPath);
    }
}

// File: Interfaces/IGitRepository.cs
using LibGit2Sharp;
using System.Collections.Generic;

namespace RepoMigrationTool.Interfaces
{
    public interface IGitRepository : System.IDisposable
    {
        IEnumerable<Branch> Branches { get; }
        IEnumerable<Tag> Tags { get; }
        Branch Head { get; }
        RepositoryStatus RetrieveStatus();
        Branch CreateBranch(string name, Commit target = null);
        void Checkout(Branch branch);
        Commit Commit(string message, Signature author, Signature committer);
        void Stage(string pathspec);
        MergeResult Merge(Branch branch, Signature merger, MergeOptions options);
        void UpdateRemote(string remoteName, string url);
        void Fetch(string remoteName, FetchOptions options, string logMessage);
        void Push(string remoteName, IEnumerable<string> refSpecs, PushOptions options);
    }
}

// File: Interfaces/IGitOperations.cs
using LibGit2Sharp;
using RepoMigrationTool.Models;
using System.Threading.Tasks;

namespace RepoMigrationTool.Interfaces
{
    public interface IGitOperations
    {
        Task<IGitRepository> CloneRepositoryAsync(string sourceUrl, string localPath, CredentialsConfig credentials);
        Task FetchAllBranchesAsync(IGitRepository repository, CredentialsConfig credentials);
        Task RenameDefaultBranchAsync(IGitRepository repository, MigrationOptions options);
        Task SetRemoteUrlAsync(IGitRepository repository, string newUrl);
        Task MergeRemoteMainAsync(IGitRepository repository, CredentialsConfig credentials, MigrationOptions options);
        Task CreateCommitIfNeededAsync(IGitRepository repository, string commitMessage);
        Task PushAllBranchesAsync(IGitRepository repository, CredentialsConfig credentials, MigrationOptions options);
        Task PushAllTagsAsync(IGitRepository repository, CredentialsConfig credentials, MigrationOptions options);
    }
}

// File: Interfaces/IMigrationService.cs
using RepoMigrationTool.Models;
using System.Threading.Tasks;

namespace RepoMigrationTool.Interfaces
{
    public interface IMigrationService
    {
        Task MigrateRepositoryAsync(MigrationConfig config);
    }
}

// File: Interfaces/ILogger.cs
using System;

namespace RepoMigrationTool.Interfaces
{
    public interface ILogger
    {
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
    }
}

// File: Services/ConfigurationService.cs
using RepoMigrationTool.Interfaces;
using RepoMigrationTool.Models;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RepoMigrationTool.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly ILogger _logger;

        public ConfigurationService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<MigrationConfig> LoadConfigAsync(string configPath)
        {
            try
            {
                _logger.LogInformation($"Loading configuration from: {configPath}");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Configuration file not found: {configPath}");
                }

                var yamlContent = await File.ReadAllTextAsync(configPath);
                
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var config = deserializer.Deserialize<MigrationConfig>(yamlContent);
                
                ValidateConfig(config);
                _logger.LogInformation("Configuration loaded successfully");
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load configuration", ex);
                throw;
            }
        }

        private void ValidateConfig(MigrationConfig config)
        {
            if (string.IsNullOrEmpty(config.SourceUrl))
                throw new ArgumentException("SourceUrl is required");
            
            if (string.IsNullOrEmpty(config.DestinationUrl))
                throw new ArgumentException("DestinationUrl is required");
            
            if (string.IsNullOrEmpty(config.LocalPath))
                throw new ArgumentException("LocalPath is required");
            
            if (string.IsNullOrEmpty(config.CommitMessage))
                throw new ArgumentException("CommitMessage is required");
        }
    }
}

// File: Services/ConsoleLogger.cs
using RepoMigrationTool.Interfaces;
using System;

namespace RepoMigrationTool.Services
{
    public class ConsoleLogger : ILogger
    {
        public void LogInformation(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public void LogError(string message, Exception exception = null)
        {
            Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            if (exception != null)
            {
                Console.WriteLine($"Exception: {exception}");
            }
        }
    }
}

// File: Wrappers/GitRepositoryWrapper.cs
using LibGit2Sharp;
using RepoMigrationTool.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace RepoMigrationTool.Wrappers
{
    public class GitRepositoryWrapper : IGitRepository
    {
        private readonly Repository _repository;

        public GitRepositoryWrapper(Repository repository)
        {
            _repository = repository;
        }

        public IEnumerable<Branch> Branches => _repository.Branches;
        public IEnumerable<Tag> Tags => _repository.Tags;
        public Branch Head => _repository.Head;

        public RepositoryStatus RetrieveStatus() => _repository.RetrieveStatus();

        public Branch CreateBranch(string name, Commit target = null)
        {
            return target == null ? _repository.CreateBranch(name) : _repository.CreateBranch(name, target);
        }

        public void Checkout(Branch branch) => Commands.Checkout(_repository, branch);

        public Commit Commit(string message, Signature author, Signature committer)
        {
            return _repository.Commit(message, author, committer);
        }

        public void Stage(string pathspec) => Commands.Stage(_repository, pathspec);

        public MergeResult Merge(Branch branch, Signature merger, MergeOptions options)
        {
            return _repository.Merge(branch, merger, options);
        }

        public void UpdateRemote(string remoteName, string url)
        {
            _repository.Network.Remotes.Update(remoteName, r => r.Url = url);
        }

        public void Fetch(string remoteName, FetchOptions options, string logMessage)
        {
            var remote = _repository.Network.Remotes[remoteName];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(_repository, remoteName, refSpecs, options, logMessage);
        }

        public void Push(string remoteName, IEnumerable<string> refSpecs, PushOptions options)
        {
            var remote = _repository.Network.Remotes[remoteName];
            _repository.Network.Push(remote, refSpecs, options);
        }

        public void Dispose() => _repository?.Dispose();
    }
}

// File: Services/GitOperations.cs
using LibGit2Sharp;
using RepoMigrationTool.Interfaces;
using RepoMigrationTool.Models;
using RepoMigrationTool.Wrappers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RepoMigrationTool.Services
{
    public class GitOperations : IGitOperations
    {
        private readonly ILogger _logger;

        public GitOperations(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<IGitRepository> CloneRepositoryAsync(string sourceUrl, string localPath, CredentialsConfig credentials)
        {
            return await Task.Run(() =>
            {
                _logger.LogInformation($"Cloning repository from {sourceUrl} to {localPath}");

                if (Directory.Exists(localPath))
                {
                    Directory.Delete(localPath, true);
                }

                var cloneOptions = new CloneOptions
                {
                    IsBare = false,
                    Checkout = true,
                    RecurseSubmodules = true
                };

                if (credentials != null)
                {
                    cloneOptions.CredentialsProvider = CreateCredentialsProvider(credentials);
                }

                var repository = Repository.Clone(sourceUrl, localPath, cloneOptions);
                _logger.LogInformation("Repository cloned successfully");

                return new GitRepositoryWrapper(new Repository(localPath));
            });
        }

        public async Task FetchAllBranchesAsync(IGitRepository repository, CredentialsConfig credentials)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation("Fetching all branches");

                var fetchOptions = new FetchOptions();
                if (credentials != null)
                {
                    fetchOptions.CredentialsProvider = CreateCredentialsProvider(credentials);
                }

                repository.Fetch("origin", fetchOptions, "Fetching all branches");

                // Create local tracking branches
                foreach (var remoteBranch in repository.Branches.Where(b => b.IsRemote && !b.IsTracking))
                {
                    var localBranchName = remoteBranch.FriendlyName.Replace("origin/", "");
                    if (!repository.Branches.Any(b => b.FriendlyName == localBranchName))
                    {
                        repository.CreateBranch(localBranchName, remoteBranch.Tip);
                    }
                }

                _logger.LogInformation("All branches fetched successfully");
            });
        }

        public async Task RenameDefaultBranchAsync(IGitRepository repository, MigrationOptions options)
        {
            await Task.Run(() =>
            {
                if (!options.RenameMainBranch) return;

                _logger.LogInformation("Renaming default branch");

                var currentBranch = repository.Head;
                if (currentBranch.FriendlyName == "main")
                {
                    var oldMainBranch = repository.Branches.FirstOrDefault(b => b.FriendlyName == options.OldMainBranchName);
                    if (oldMainBranch == null)
                    {
                        // Note: LibGit2Sharp doesn't have direct rename, would need to use Repository class
                        _logger.LogWarning("Branch renaming requires direct Repository access - implement if needed");
                    }
                    else
                    {
                        _logger.LogInformation($"Branch '{options.OldMainBranchName}' already exists. Skipping rename.");
                    }
                }
            });
        }

        public async Task SetRemoteUrlAsync(IGitRepository repository, string newUrl)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation($"Setting remote URL to: {newUrl}");
                repository.UpdateRemote("origin", newUrl);
                _logger.LogInformation("Remote URL updated successfully");
            });
        }

        public async Task MergeRemoteMainAsync(IGitRepository repository, CredentialsConfig credentials, MigrationOptions options)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation("Merging remote main branch");

                try
                {
                    var fetchOptions = new FetchOptions();
                    if (credentials != null)
                    {
                        fetchOptions.CredentialsProvider = CreateCredentialsProvider(credentials);
                    }

                    repository.Fetch("origin", fetchOptions, "Fetching from new remote");

                    var remoteMainBranch = repository.Branches.FirstOrDefault(b => b.FriendlyName == "origin/main");
                    if (remoteMainBranch != null)
                    {
                        var mainBranch = repository.Branches.FirstOrDefault(b => b.FriendlyName == "main");
                        if (mainBranch == null)
                        {
                            mainBranch = repository.CreateBranch("main");
                        }

                        repository.Checkout(mainBranch);

                        var mergeOptions = new MergeOptions
                        {
                            AllowUnrelatedHistories = options.AllowUnrelatedHistories,
                            CommitOnSuccess = false
                        };

                        var signature = new Signature("Migration Tool", "migration@tool.com", DateTimeOffset.Now);
                        var mergeResult = repository.Merge(remoteMainBranch, signature, mergeOptions);

                        _logger.LogInformation($"Merge result: {mergeResult.Status}");
                    }
                    else
                    {
                        _logger.LogInformation("No remote main branch found in destination");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Warning during merge: {ex.Message}");
                }
            });
        }

        public async Task CreateCommitIfNeededAsync(IGitRepository repository, string commitMessage)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation("Creating commit if needed");

                var status = repository.RetrieveStatus();
                if (status.IsDirty)
                {
                    repository.Stage("*");
                    var signature = new Signature("Migration Tool", "migration@tool.com", DateTimeOffset.Now);

                    try
                    {
                        var commit = repository.Commit(commitMessage, signature, signature);
                        _logger.LogInformation($"Commit created: {commit.Sha}");
                    }
                    catch (EmptyCommitException)
                    {
                        _logger.LogInformation("No changes to commit");
                    }
                }
                else
                {
                    _logger.LogInformation("No changes detected, skipping commit");
                }
            });
        }

        public async Task PushAllBranchesAsync(IGitRepository repository, CredentialsConfig credentials, MigrationOptions options)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation("Pushing all branches");

                var pushOptions = new PushOptions();
                if (credentials != null)
                {
                    pushOptions.CredentialsProvider = CreateCredentialsProvider(credentials);
                }

                var localBranches = repository.Branches
                    .Where(b => !b.IsRemote && !options.ExcludeBranches.Contains(b.FriendlyName))
                    .ToList();

                var refSpecs = localBranches.Select(branch => 
                    $"refs/heads/{branch.FriendlyName}:refs/heads/{branch.FriendlyName}").ToList();

                if (refSpecs.Any())
                {
                    repository.Push("origin", refSpecs, pushOptions);
                    _logger.LogInformation($"Pushed {refSpecs.Count} branches");
                }
            });
        }

        public async Task PushAllTagsAsync(IGitRepository repository, CredentialsConfig credentials, MigrationOptions options)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation("Pushing all tags");

                var pushOptions = new PushOptions();
                if (credentials != null)
                {
                    pushOptions.CredentialsProvider = CreateCredentialsProvider(credentials);
                }

                var tags = repository.Tags
                    .Where(tag => !options.ExcludeTags.Contains(tag.FriendlyName))
                    .ToList();

                var tagRefs = tags.Select(tag => 
                    $"refs/tags/{tag.FriendlyName}:refs/tags/{tag.FriendlyName}").ToList();

                if (tagRefs.Any())
                {
                    repository.Push("origin", tagRefs, pushOptions);
                    _logger.LogInformation($"Pushed {tagRefs.Count} tags");
                }
                else
                {
                    _logger.LogInformation("No tags found to push");
                }
            });
        }

        private CredentialsHandler CreateCredentialsProvider(CredentialsConfig credentials)
        {
            return (_url, _user, _cred) =>
            {
                if (!string.IsNullOrEmpty(credentials.PersonalAccessToken))
                {
                    return new UsernamePasswordCredentials 
                    { 
                        Username = credentials.Username ?? "token", 
                        Password = credentials.PersonalAccessToken 
                    };
                }
                
                if (!string.IsNullOrEmpty(credentials.Username) && !string.IsNullOrEmpty(credentials.Password))
                {
                    return new UsernamePasswordCredentials 
                    { 
                        Username = credentials.Username, 
                        Password = credentials.Password 
                    };
                }

                return null;
            };
        }
    }
}

// File: Services/MigrationService.cs
using RepoMigrationTool.Interfaces;
using RepoMigrationTool.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RepoMigrationTool.Services
{
    public class MigrationService : IMigrationService
    {
        private readonly IGitOperations _gitOperations;
        private readonly ILogger _logger;

        public MigrationService(IGitOperations gitOperations, ILogger logger)
        {
            _gitOperations = gitOperations;
            _logger = logger;
        }

        public async Task MigrateRepositoryAsync(MigrationConfig config)
        {
            IGitRepository repository = null;
            
            try
            {
                _logger.LogInformation("Starting repository migration...");

                // Step 1: Clone the source repository
                repository = await _gitOperations.CloneRepositoryAsync(
                    config.SourceUrl, 
                    config.LocalPath, 
                    config.Source);

                // Steps 2-3: Fetch all branches
                await _gitOperations.FetchAllBranchesAsync(repository, config.Source);

                // Step 4: Rename default branch
                await _gitOperations.RenameDefaultBranchAsync(repository, config.Options);

                // Step 5: Set new remote URL
                await _gitOperations.SetRemoteUrlAsync(repository, config.DestinationUrl);

                // Step 6: Merge remote main
                await _gitOperations.MergeRemoteMainAsync(repository, config.Destination, config.Options);

                // Step 7: Create commit with message
                await _gitOperations.CreateCommitIfNeededAsync(repository, config.CommitMessage);

                // Step 8: Push all branches
                await _gitOperations.PushAllBranchesAsync(repository, config.Destination, config.Options);

                // Step 9: Push all tags
                await _gitOperations.PushAllTagsAsync(repository, config.Destination, config.Options);

                _logger.LogInformation("Repository migration completed successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError("Migration failed", ex);
                throw;
            }
            finally
            {
                repository?.Dispose();
                
                if (config.Options.CleanupLocalRepo && Directory.Exists(config.LocalPath))
                {
                    try
                    {
                        Directory.Delete(config.LocalPath, true);
                        _logger.LogInformation("Local repository cleaned up");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to cleanup local repository: {ex.Message}");
                    }
                }
            }
        }
    }
}

// File: Program.cs
using Microsoft.Extensions.DependencyInjection;
using RepoMigrationTool.Interfaces;
using RepoMigrationTool.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RepoMigrationTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();

            try
            {
                var configPath = args.Length > 0 ? args[0] : "migration-config.yaml";
                
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Configuration file not found: {configPath}");
                    Console.WriteLine("Usage: RepoMigrationTool [config-file-path]");
                    return;
                }

                var configService = serviceProvider.GetRequiredService<IConfigurationService>();
                var migrationService = serviceProvider.GetRequiredService<IMigrationService>();

                var config = await configService.LoadConfigAsync(configPath);
                await migrationService.MigrateRepositoryAsync(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();
            
            services.AddSingleton<ILogger, ConsoleLogger>();
            services.AddScoped<IConfigurationService, ConfigurationService>();
            services.AddScoped<IGitOperations, GitOperations>();
            services.AddScoped<IMigrationService, MigrationService>();
            
            return services;
        }
    }
}
