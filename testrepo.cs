using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace RepoMigrationTool
{
    /// <summary>
    /// Main program entry point for the Repository Migration Tool
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point with command line argument parsing
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code</returns>
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = CreateRootCommand();
            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Creates the root command with all options and subcommands
        /// </summary>
        /// <returns>Root command</returns>
        private static RootCommand CreateRootCommand()
        {
            var rootCommand = new RootCommand("Repository Migration Tool - Migrate repositories from Azure DevOps to GitHub")
            {
                Name = "repo-migration-tool"
            };

            // Add global options
            var configOption = new Option<string>(
                aliases: new[] { "--config", "-c" },
                description: "Path to the JSON configuration file",
                getDefaultValue: () => "migration-config.json"
            );

            var verboseOption = new Option<bool>(
                aliases: new[] { "--verbose", "-v" },
                description: "Enable verbose logging"
            );

            var dryRunOption = new Option<bool>(
                aliases: new[] { "--dry-run", "-d" },
                description: "Perform a dry run without actual migration"
            );

            rootCommand.AddOption(configOption);
            rootCommand.AddOption(verboseOption);
            rootCommand.AddOption(dryRunOption);

            // Add migrate command
            var migrateCommand = CreateMigrateCommand();
            rootCommand.AddCommand(migrateCommand);

            // Add validate command
            var validateCommand = CreateValidateCommand();
            rootCommand.AddCommand(validateCommand);

            // Add init command
            var initCommand = CreateInitCommand();
            rootCommand.AddCommand(initCommand);

            // Set default handler for root command
            rootCommand.SetHandler(async (string config, bool verbose, bool dryRun) =>
            {
                await HandleMigrateCommand(config, verbose, dryRun);
            }, configOption, verboseOption, dryRunOption);

            return rootCommand;
        }

        /// <summary>
        /// Creates the migrate command
        /// </summary>
        /// <returns>Migrate command</returns>
        private static Command CreateMigrateCommand()
        {
            var migrateCommand = new Command("migrate", "Migrate repositories using the specified configuration");

            var configOption = new Option<string>(
                aliases: new[] { "--config", "-c" },
                description: "Path to the JSON configuration file",
                getDefaultValue: () => "migration-config.json"
            );

            var verboseOption = new Option<bool>(
                aliases: new[] { "--verbose", "-v" },
                description: "Enable verbose logging"
            );

            var dryRunOption = new Option<bool>(
                aliases: new[] { "--dry-run", "-d" },
                description: "Perform a dry run without actual migration"
            );

            var repositoryFilter = new Option<string[]>(
                aliases: new[] { "--repositories", "-r" },
                description: "Filter repositories by name (supports wildcards)"
            );

            migrateCommand.AddOption(configOption);
            migrateCommand.AddOption(verboseOption);
            migrateCommand.AddOption(dryRunOption);
            migrateCommand.AddOption(repositoryFilter);

            migrateCommand.SetHandler(async (string config, bool verbose, bool dryRun, string[] repoFilter) =>
            {
                await HandleMigrateCommand(config, verbose, dryRun, repoFilter);
            }, configOption, verboseOption, dryRunOption, repositoryFilter);

            return migrateCommand;
        }

        /// <summary>
        /// Creates the validate command
        /// </summary>
        /// <returns>Validate command</returns>
        private static Command CreateValidateCommand()
        {
            var validateCommand = new Command("validate", "Validate the configuration file");

            var configOption = new Option<string>(
                aliases: new[] { "--config", "-c" },
                description: "Path to the JSON configuration file",
                getDefaultValue: () => "migration-config.json"
            );

            var testConnectionsOption = new Option<bool>(
                aliases: new[] { "--test-connections", "-t" },
                description: "Test connections to repositories"
            );

            validateCommand.AddOption(configOption);
            validateCommand.AddOption(testConnectionsOption);

            validateCommand.SetHandler(async (string config, bool testConnections) =>
            {
                await HandleValidateCommand(config, testConnections);
            }, configOption, testConnectionsOption);

            return validateCommand;
        }

        /// <summary>
        /// Creates the init command
        /// </summary>
        /// <returns>Init command</returns>
        private static Command CreateInitCommand()
        {
            var initCommand = new Command("init", "Initialize a new configuration file");

            var configOption = new Option<string>(
                aliases: new[] { "--config", "-c" },
                description: "Path for the new configuration file",
                getDefaultValue: () => "migration-config.json"
            );

            var forceOption = new Option<bool>(
                aliases: new[] { "--force", "-f" },
                description: "Overwrite existing configuration file"
            );

            initCommand.AddOption(configOption);
            initCommand.AddOption(forceOption);

            initCommand.SetHandler(async (string config, bool force) =>
            {
                await HandleInitCommand(config, force);
            }, configOption, forceOption);

            return initCommand;
        }

        /// <summary>
        /// Handles the migrate command execution
        /// </summary>
        private static async Task HandleMigrateCommand(string configPath, bool verbose, bool dryRun, string[] repositoryFilter = null)
        {
            try
            {
                var logger = new ConsoleLogger(verbose);
                logger.LogInfo("Repository Migration Tool v1.0.0");
                logger.LogInfo($"Configuration file: {configPath}");

                if (dryRun)
                {
                    logger.LogWarning("DRY RUN MODE - No actual changes will be made");
                }

                // Check if config file exists
                if (!File.Exists(configPath))
                {
                    logger.LogError($"Configuration file not found: {configPath}");
                    logger.LogInfo("Use 'repo-migration-tool init' to create a sample configuration file");
                    Environment.Exit(1);
                }

                // Load and validate configuration
                var config = await ConfigurationManager.LoadConfiguration(configPath, logger);
                ConfigurationManager.ValidateConfiguration(config, logger);

                var migrator = new RepositoryMigrator(config.Settings.WorkingDirectory, logger, dryRun);

                logger.LogInfo($"Starting migration of {config.Repositories.Count} repositories...");

                int successCount = 0;
                int failureCount = 0
                int skippedCount = 0;

                // Apply repository filter if specified
                var repositoriesToProcess = FilterRepositories(config.Repositories, repositoryFilter, logger);

                // Process each repository
                for (int i = 0; i < repositoriesToProcess.Count; i++)
                {
                    var repo = repositoriesToProcess[i];
                    logger.LogSeparator();
                    logger.LogInfo($"Processing Repository {i + 1}/{repositoriesToProcess.Count}: {repo.Name}");
                    logger.LogSeparator();

                    try
                    {
                        if (!repo.Enabled)
                        {
                            logger.LogWarning($"Skipping disabled repository: {repo.Name}");
                            skippedCount++;
                            continue;
                        }

                        await migrator.MigrateRepository(repo, config.Authentication);
                        logger.LogSuccess($"Successfully migrated: {repo.Name}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to migrate {repo.Name}: {ex.Message}");
                        if (verbose)
                        {
                            logger.LogError($"Stack trace: {ex.StackTrace}");
                        }
                        failureCount++;

                        if (config.Settings.StopOnFirstError)
                        {
                            logger.LogError("Stopping migration due to StopOnFirstError setting.");
                            break;
                        }
                    }
                }

                // Summary
                logger.LogSeparator();
                logger.LogInfo("MIGRATION SUMMARY");
                logger.LogSeparator();
                logger.LogSuccess($"Successful migrations: {successCount}");
                logger.LogError($"Failed migrations: {failureCount}");
                logger.LogWarning($"Skipped migrations: {skippedCount}");
                logger.LogInfo($"Total repositories processed: {repositoriesToProcess.Count}");

                if (failureCount > 0)
                {
                    Environment.Exit(1);
                }

                logger.LogSuccess("🎉 All enabled repositories migrated successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Migration process failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Handles the validate command execution
        /// </summary>
        private static async Task HandleValidateCommand(string configPath, bool testConnections)
        {
            var logger = new ConsoleLogger(true);
            
            try
            {
                logger.LogInfo("Repository Migration Configuration Validator");
                logger.LogSeparator();

                if (!File.Exists(configPath))
                {
                    logger.LogError($"Configuration file not found: {configPath}");
                    Environment.Exit(1);
                }

                logger.LogInfo($"Validating configuration file: {configPath}");

                var config = await ConfigurationManager.LoadConfiguration(configPath, logger);
                ConfigurationManager.ValidateConfiguration(config, logger);

                ConfigurationManager.ShowConfigurationSummary(config, logger);

                if (testConnections)
                {
                    logger.LogSeparator();
                    logger.LogInfo("CONNECTION TESTING");
                    logger.LogSeparator();

                    var validator = new ConnectionValidator(logger);
                    var connectionsOk = await validator.TestRepositoryConnections(config);
                    
                    if (!connectionsOk)
                    {
                        logger.LogWarning("Some repository connections failed. Please check your PAT tokens and repository URLs.");
                        Environment.Exit(1);
                    }
                }

                logger.LogSeparator();
                logger.LogSuccess("Configuration validation completed successfully!");
                logger.LogSeparator();
            }
            catch (Exception ex)
            {
                logger.LogError($"Validation failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Handles the init command execution
        /// </summary>
        private static async Task HandleInitCommand(string configPath, bool force)
        {
            var logger = new ConsoleLogger(true);
            
            try
            {
                if (File.Exists(configPath) && !force)
                {
                    logger.LogError($"Configuration file already exists: {configPath}");
                    logger.LogInfo("Use --force to overwrite the existing file");
                    Environment.Exit(1);
                }

                var success = await ConfigurationManager.CreateSampleConfiguration(configPath, logger);
                
                if (success)
                {
                    logger.LogSuccess($"Sample configuration created: {configPath}");
                    logger.LogInfo("Please edit the configuration file with your actual values before running the migration.");
                }
                else
                {
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Initialization failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Filters repositories based on the provided filter criteria
        /// </summary>
        private static List<RepositoryConfiguration> FilterRepositories(
            List<RepositoryConfiguration> repositories, 
            string[] filters, 
            ILogger logger)
        {
            if (filters == null || filters.Length == 0)
            {
                return repositories;
            }

            var filteredRepos = new List<RepositoryConfiguration>();
            
            foreach (var repo in repositories)
            {
                foreach (var filter in filters)
                {
                    if (IsMatch(repo.Name, filter))
                    {
                        filteredRepos.Add(repo);
                        break;
                    }
                }
            }

            logger.LogInfo($"Repository filter applied: {filteredRepos.Count}/{repositories.Count} repositories selected");
            return filteredRepos;
        }

        /// <summary>
        /// Simple wildcard matching for repository names
        /// </summary>
        private static bool IsMatch(string text, string pattern)
        {
            if (pattern == "*") return true;
            if (!pattern.Contains("*")) return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            
            // Simple wildcard implementation
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }

    /// <summary>
    /// Repository migration orchestrator
    /// </summary>
    public class RepositoryMigrator
    {
        private readonly string _workingDirectory;
        private readonly ILogger _logger;
        private readonly bool _dryRun;

        public RepositoryMigrator(string workingDirectory, ILogger logger, bool dryRun = false)
        {
            _workingDirectory = workingDirectory ?? "./temp-migration";
            _logger = logger;
            _dryRun = dryRun;

            // Create working directory if it doesn't exist
            if (!Directory.Exists(_workingDirectory))
            {
                Directory.CreateDirectory(_workingDirectory);
            }
        }

        public async Task MigrateRepository(RepositoryConfiguration repoConfig, AuthenticationConfiguration authConfig)
        {
            if (!repoConfig.Enabled)
            {
                _logger.LogWarning($"Skipping disabled repository: {repoConfig.Name}");
                return;
            }

            _logger.LogInfo($"🚀 Starting migration for: {repoConfig.Name}");

            if (_dryRun)
            {
                _logger.LogWarning($"DRY RUN: Would migrate {repoConfig.Name} from {repoConfig.SourceUrl} to {repoConfig.DestinationUrl}");
                return;
            }

            // Extract repository name from URL
            var repositoryName = ExtractRepositoryName(repoConfig.SourceUrl);
            var repositoryPath = Path.Combine(_workingDirectory, repositoryName);

            // Create authenticated URLs
            var authenticatedSourceUrl = InjectPATIntoUrl(repoConfig.SourceUrl, authConfig.AdoPat, "ADO");
            var authenticatedDestinationUrl = InjectPATIntoUrl(repoConfig.DestinationUrl, authConfig.GitHubPat, "GitHub");

            try
            {
                // Migration steps
                await CloneRepository(authenticatedSourceUrl, repositoryName, repositoryPath);
                await PullAllBranches(repositoryPath);
                await RenameDefaultBranch(repositoryPath);
                await SetNewRemote(authenticatedDestinationUrl, repositoryPath);
                await MergeRemoteMain(repoConfig.CommitMessage, repositoryPath);
                await PushAllBranches(repositoryPath);
                await PushAllTags(repositoryPath);

                _logger.LogSuccess($"Successfully completed migration for: {repoConfig.Name}");
            }
            finally
            {
                // Cleanup temporary directory if requested
                if (repoConfig.CleanupAfterMigration && Directory.Exists(repositoryPath))
                {
                    try
                    {
                        Directory.Delete(repositoryPath, true);
                        _logger.LogInfo($"🧹 Cleaned up temporary directory: {repositoryPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not cleanup directory {repositoryPath}: {ex.Message}");
                    }
                }
            }
        }

        private string InjectPATIntoUrl(string url, string pat, string provider)
        {
            try
            {
                var uri = new Uri(url);

                if (provider == "ADO")
                {
                    return $"{uri.Scheme}://pat:{pat}@{uri.Host}{uri.PathAndQuery}";
                }
                else
                {
                    return $"{uri.Scheme}://{pat}@{uri.Host}{uri.PathAndQuery}";
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid URL format: {ex.Message}");
            }
        }

        private async Task CloneRepository(string sourceUrl, string repositoryName, string repositoryPath)
        {
            _logger.LogInfo($"📥 Cloning repository...");

            if (Directory.Exists(repositoryPath))
            {
                Directory.Delete(repositoryPath, true);
            }

            await RunGitCommand($"clone --mirror \"{sourceUrl}\" \"{repositoryName}\"", _workingDirectory);
            await RunGitCommand("config --bool core.bare false", repositoryPath);
            await RunGitCommand("reset --hard", repositoryPath);
        }

        private async Task PullAllBranches(string repositoryPath)
        {
            _logger.LogInfo("🌿 Fetching all branches...");

            await RunGitCommand("fetch --all", repositoryPath);

            var result = await RunGitCommand("branch -r", repositoryPath, captureOutput: true);
            var branches = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var branch in branches)
            {
                var branchName = branch.Trim().Replace("origin/", "");
                if (!branchName.StartsWith("HEAD") && branchName != "main" && branchName != "master")
                {
                    try
                    {
                        await RunGitCommand($"checkout -b \"{branchName}\" \"origin/{branchName}\"", repositoryPath);
                    }
                    catch
                    {
                        _logger.LogWarning($"Branch {branchName} already exists or cannot be created");
                    }
                }
            }
        }

        private async Task RenameDefaultBranch(string repositoryPath)
        {
            _logger.LogInfo("🔄 Renaming default branch...");

            try
            {
                try
                {
                    await RunGitCommand("checkout main", repositoryPath);
                    await RunGitCommand("branch -m main old-main", repositoryPath);
                }
                catch
                {
                    try
                    {
                        await RunGitCommand("checkout master", repositoryPath);
                        await RunGitCommand("branch -m master old-main", repositoryPath);
                    }
                    catch
                    {
                        _logger.LogInfo("No main or master branch found to rename");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not rename default branch: {ex.Message}");
            }
        }

        private async Task SetNewRemote(string destinationUrl, string repositoryPath)
        {
            _logger.LogInfo($"🔗 Setting new remote...");
            await RunGitCommand($"remote set-url origin \"{destinationUrl}\"", repositoryPath);
        }

        private async Task MergeRemoteMain(string commitMessage, string repositoryPath)
        {
            _logger.LogInfo("🔀 Merging remote main branch...");

            try
            {
                await RunGitCommand("checkout -b main", repositoryPath);
                await RunGitCommand("pull --no-rebase origin main --allow-unrelated-histories", repositoryPath);

                var status = await RunGitCommand("status --porcelain", repositoryPath, captureOutput: true);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    _logger.LogInfo("🔧 Resolving conflicts and creating merge commit...");
                    await RunGitCommand("add .", repositoryPath);
                    await RunGitCommand($"commit -m \"{commitMessage}\"", repositoryPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInfo($"Merge operation result: {ex.Message}");
            }
        }

        private async Task PushAllBranches(string repositoryPath)
        {
            _logger.LogInfo("📤 Pushing all branches...");
            await RunGitCommand("push --all origin", repositoryPath);
        }

        private async Task PushAllTags(string repositoryPath)
        {
            _logger.LogInfo("🏷️ Pushing all tags...");
            await RunGitCommand("push --tags origin", repositoryPath);
        }

        private async Task<string> RunGitCommand(string arguments, string workingDirectory, bool captureOutput = false, bool allowFailure = false)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.EnvironmentVariables["GIT_ASKPASS"] = "echo";
            startInfo.EnvironmentVariables["SSH_ASKPASS"] = "echo";

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = "";
            if (captureOutput)
            {
                output = await process.StandardOutput.ReadToEndAsync();
            }

            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && !allowFailure)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    throw new Exception($"Git command failed: {error}");
                }
                throw new Exception($"Git command failed with exit code: {process.ExitCode}");
            }

            if (!arguments.Contains("://") || arguments.Contains("config") || arguments.Contains("status"))
            {
                _logger.LogDebug($"  → git {arguments}");
            }
            else
            {
                _logger.LogDebug($"  → git [authenticated command]");
            }

            return output;
        }

        private string ExtractRepositoryName(string url)
        {
            try
            {
                if (url.Contains("@"))
                {
                    var parts = url.Split('@');
                    if (parts.Length > 1)
                    {
                        url = "https://" + parts[1];
                    }
                }

                var uri = new Uri(url);
                var segments = uri.Segments;
                var lastSegment = segments[segments.Length - 1];

                if (lastSegment.EndsWith(".git"))
                {
                    lastSegment = lastSegment.Substring(0, lastSegment.Length - 4);
                }

                return lastSegment;
            }
            catch
            {
                var lastSlash = url.LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    var name = url.Substring(lastSlash + 1);
                    return name.EndsWith(".git") ? name.Substring(0, name.Length - 4) : name;
                }
                return "repository";
            }
        }
    }

    // Configuration Management Classes
    public static class ConfigurationManager
    {
        public static async Task<MigrationConfiguration> LoadConfiguration(string configPath, ILogger logger)
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };

                var config = JsonSerializer.Deserialize<MigrationConfiguration>(json, options);

                if (config == null)
                {
                    throw new Exception("Failed to deserialize configuration file");
                }

                return config;
            }
            catch (JsonException ex)
            {
                throw new Exception($"Invalid JSON in configuration file: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading configuration file: {ex.Message}");
            }
        }

        public static void ValidateConfiguration(MigrationConfiguration config, ILogger logger)
        {
            if (config.Authentication == null)
                throw new Exception("Authentication configuration is required");

            if (string.IsNullOrWhiteSpace(config.Authentication.AdoPat))
                throw new Exception("ADO PAT token is required in authentication configuration");

            if (string.IsNullOrWhiteSpace(config.Authentication.GitHubPat))
                throw new Exception("GitHub PAT token is required in authentication configuration");

            if (config.Repositories == null || config.Repositories.Count == 0)
                throw new Exception("At least one repository configuration is required");

            for (int i = 0; i < config.Repositories.Count; i++)
            {
                var repo = config.Repositories[i];
                if (string.IsNullOrWhiteSpace(repo.SourceUrl))
                    throw new Exception($"Repository {i + 1}: Source URL is required");

                if (string.IsNullOrWhiteSpace(repo.DestinationUrl))
                    throw new Exception($"Repository {i + 1}: Destination URL is required");

                if (string.IsNullOrWhiteSpace(repo.CommitMessage))
                    throw new Exception($"Repository {i + 1}: Commit message is required");
            }

            logger.LogSuccess("Configuration validation passed");
        }

        public static async Task<bool> CreateSampleConfiguration(string configPath, ILogger logger)
        {
            var sampleConfig = new MigrationConfiguration
            {
                Settings = new MigrationSettings
                {
                    WorkingDirectory = "./temp-migration",
                    StopOnFirstError = false,
                    CleanupTempDirectory = true
                },
                Authentication = new AuthenticationConfiguration
                {
                    AdoPat = "your_ado_personal_access_token_here",
                    GitHubPat = "your_github_personal_access_token_here"
                },
                Repositories = new List<RepositoryConfiguration>
                {
                    new RepositoryConfiguration
                    {
                        Name = "Sample Repository 1",
                        SourceUrl = "https://dev.azure.com/your-org/your-project/_git/repo1",
                        DestinationUrl = "https://github.com/your-username/repo1.git",
                        CommitMessage = "Migration from ADO to GitHub - Repo 1",
                        Enabled = true,
                        CleanupAfterMigration = true
                    },
                    new RepositoryConfiguration
                    {
                        Name = "Sample Repository 2",
                        SourceUrl = "https://dev.azure.com/your-org/your-project/_git/repo2",
                        DestinationUrl = "https://github.com/your-username/repo2.git",
                        CommitMessage = "Migration from ADO to GitHub - Repo 2",
                        Enabled = true,
                        CleanupAfterMigration = true
                    }
                }
            };

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(sampleConfig, options);
                await File.WriteAllTextAsync(configPath, json);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to create sample configuration: {ex.Message}");
                return false;
            }
        }

        public static void ShowConfigurationSummary(MigrationConfiguration config, ILogger logger)
        {
            logger.LogSeparator();
            logger.LogInfo("CONFIGURATION SUMMARY");
            logger.LogSeparator();

            logger.LogInfo("Settings:");
            logger.LogInfo($"  Working Directory: {config.Settings.WorkingDirectory}");
            logger.LogInfo($"  Stop on First Error: {config.Settings.StopOnFirstError}");
            logger.LogInfo($"  Cleanup Temp Directory: {config.Settings.CleanupTempDirectory}");

            logger.LogInfo("Authentication:");
            var adoMasked = MaskToken(config.Authentication.AdoPat);
            var githubMasked = MaskToken(config.Authentication.GitHubPat);
            logger.LogInfo($"  ADO PAT: {adoMasked}");
            logger.LogInfo($"  GitHub PAT: {githubMasked}");

            var enabledCount = config.Repositories.Count(r => r.Enabled);
            logger.LogInfo("Repositories:");
            logger.LogInfo($"  Total: {config.Repositories.Count}");
            logger.LogInfo($"  Enabled: {enabledCount}");
            logger.LogInfo($"  Disabled: {config.Repositories.Count - enabledCount}");

            logger.LogInfo("Repository Details:");
            for (int i = 0; i < config.Repositories.Count; i++)
            {
                var repo = config.Repositories[i];
                var status = repo.Enabled ? "ENABLED" : "DISABLED";
                logger.LogInfo($"  {i + 1}. {repo.Name} [{status}]");
                logger.LogInfo($"     Source: {repo.SourceUrl}");
                logger.LogInfo($"     Destination: {repo.DestinationUrl}");
                logger.LogInfo($"     Commit Message: {repo.CommitMessage}");
            }
        }

        private static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "Not Set";

            if (token.Length <= 8)
                return new string('*', token.Length);

            return new string('*', token.Length - 4) + token.Substring(token.Length - 4);
        }
    }

    public class ConnectionValidator
    {
        private readonly ILogger _logger;

        public ConnectionValidator(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> TestRepositoryConnections(MigrationConfiguration config)
        {
            _logger.LogInfo("Testing repository connections...");

            var enabledRepos = config.Repositories.Where(r => r.Enabled).ToList();
            var successCount = 0;
            var totalCount = enabledRepos.Count;

            foreach (var repo in enabledRepos)
            {
                _logger.LogInfo($"Testing connection to: {repo.Name}");

                var sourceSuccess = await TestGitConnection(repo.SourceUrl, config.Authentication.AdoPat, "ADO");
                var destSuccess = await TestGitConnection(repo.DestinationUrl, config.Authentication.GitHubPat, "GitHub");

                if (sourceSuccess && destSuccess)
                {
                    successCount++;
                }
            }

            _logger.LogInfo($"Connection test summary: {successCount}/{totalCount} repositories fully accessible");
            return successCount == totalCount;
        }

        private async Task<bool> TestGitConnection(string url, string pat, string provider)
        {
            try
            {
                var authenticatedUrl = provider == "ADO" 
                    ? url.Replace("https://", $"https://pat:{pat}@")
                    : url.Replace("https://", $"https://{pat}@");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"ls-remote --heads \"{authenticatedUrl}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogSuccess($"    {provider} connection successful");
                    return true;
                }
                else
                {
                    _logger.LogError($"    {provider} connection failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"    {provider} connection error: {ex.Message}");
                return false;
            }
        }
    }

    // Logging Infrastructure
    public interface ILogger
    {
        void LogInfo(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogDebug(string message);
        void LogSeparator();
    }

    public class ConsoleLogger : ILogger
    {
        private readonly bool _verbose;

        public ConsoleLogger(bool verbose = false)
        {
            _verbose = verbose;
        }

        public void LogInfo(string message)
        {
            Console.WriteLine($"ℹ️  {message}");
        }

        public void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ {message}");
            Console.ResetColor();
        }

        public void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  {message}");
            Console.ResetColor();
        }

        public void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {message}");
            Console.ResetColor();
        }

        public void LogDebug(string message)
        {
            if (_verbose)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"🔍 {message}");
                Console.ResetColor();
            }
        }

        public void LogSeparator()
        {
            Console.WriteLine(new string('=', 80));
        }
    }

    // Configuration Model Classes
    public class MigrationConfiguration
    {
        public MigrationSettings Settings { get; set; } = new MigrationSettings();
        public AuthenticationConfiguration Authentication { get; set; } = new AuthenticationConfiguration();
        public List<RepositoryConfiguration> Repositories { get; set; } = new List<RepositoryConfiguration>();
    }

    public class MigrationSettings
    {
        public string WorkingDirectory { get; set; } = "./temp-migration";
        public bool StopOnFirstError { get; set; } = false;
        public bool CleanupTempDirectory { get; set; } = true;
    }

    public class AuthenticationConfiguration
    {
        public string AdoPat { get; set; } = "";
        public string GitHubPat { get; set; } = "";
    }

    public class RepositoryConfiguration
    {
        public string Name { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public string DestinationUrl { get; set; } = "";
        public string CommitMessage { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public bool CleanupAfterMigration { get; set; } = true;
    }
}
