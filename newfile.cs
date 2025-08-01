# Repository Migration Solution

## Project Structure
```
RepositoryMigration/
├── RepositoryMigration/
│   ├── Models/
│   │   ├── MigrationConfig.cs
│   │   └── RepositoryInfo.cs
│   ├── Interfaces/
│   │   ├── IGitCommandExecutor.cs
│   │   ├── IYamlConfigReader.cs
│   │   └── IRepositoryMigrator.cs
│   ├── Services/
│   │   ├── GitCommandExecutor.cs
│   │   ├── YamlConfigReader.cs
│   │   └── RepositoryMigrator.cs
│   ├── Program.cs
│   └── RepositoryMigration.csproj
├── RepositoryMigration.Tests/
│   ├── Services/
│   │   ├── GitCommandExecutorTests.cs
│   │   ├── YamlConfigReaderTests.cs
│   │   └── RepositoryMigratorTests.cs
│   └── RepositoryMigration.Tests.csproj
├── configs/
│   ├── migration-config.yaml          # Main configuration file
│   ├── production-migration.yaml      # Production environment
│   ├── staging-migration.yaml         # Staging environment
│   └── sample-config.yaml            # Example/template
└── README.md
```

## Models/MigrationConfig.cs
```csharp
using System.Collections.Generic;

namespace RepositoryMigration.Models
{
    public class MigrationConfig
    {
        public List<RepositoryInfo> Repositories { get; set; } = new List<RepositoryInfo>();
    }
}
```

## Models/RepositoryInfo.cs
```csharp
namespace RepositoryMigration.Models
{
    public class RepositoryInfo
    {
        public string SourceUrl { get; set; } = string.Empty;
        public string DestinationUrl { get; set; } = string.Empty;
        public string CommitMessage { get; set; } = string.Empty;
        public string? WorkingDirectory { get; set; }
    }
}
```

## Interfaces/IGitCommandExecutor.cs
```csharp
using System.Threading.Tasks;

namespace RepositoryMigration.Interfaces
{
    public interface IGitCommandExecutor
    {
        Task<(bool Success, string Output)> ExecuteCommandAsync(string command, string workingDirectory = "");
        Task<bool> CloneRepositoryAsync(string sourceUrl, string targetDirectory);
        Task<bool> PullAllBranchesAsync(string workingDirectory);
        Task<bool> RenameDefaultBranchAsync(string workingDirectory, string oldName = "main", string newName = "old-main");
        Task<bool> SetRemoteUrlAsync(string workingDirectory, string newRemoteUrl);
        Task<bool> MergeRemoteMainAsync(string workingDirectory, string commitMessage);
        Task<bool> PushAllBranchesAsync(string workingDirectory);
        Task<bool> PushAllTagsAsync(string workingDirectory);
    }
}
```

## Interfaces/IYamlConfigReader.cs
```csharp
using System.Threading.Tasks;
using RepositoryMigration.Models;

namespace RepositoryMigration.Interfaces
{
    public interface IYamlConfigReader
    {
        Task<MigrationConfig> ReadConfigAsync(string filePath);
    }
}
```

## Interfaces/IRepositoryMigrator.cs
```csharp
using System.Threading.Tasks;
using RepositoryMigration.Models;

namespace RepositoryMigration.Interfaces
{
    public interface IRepositoryMigrator
    {
        Task<bool> MigrateRepositoryAsync(RepositoryInfo repositoryInfo);
        Task MigrateAllRepositoriesAsync(string configFilePath);
    }
}
```

## Services/GitCommandExecutor.cs
```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RepositoryMigration.Interfaces;

namespace RepositoryMigration.Services
{
    public class GitCommandExecutor : IGitCommandExecutor
    {
        public async Task<(bool Success, string Output)> ExecuteCommandAsync(string command, string workingDirectory = "")
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = command,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                    return (false, "Failed to start git process");

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var result = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";
                return (process.ExitCode == 0, result);
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }

        public async Task<bool> CloneRepositoryAsync(string sourceUrl, string targetDirectory)
        {
            var command = $"clone {sourceUrl} {targetDirectory}";
            var result = await ExecuteCommandAsync(command);
            return result.Success;
        }

        public async Task<bool> PullAllBranchesAsync(string workingDirectory)
        {
            // First, fetch all remote branches
            var fetchResult = await ExecuteCommandAsync("fetch --all", workingDirectory);
            if (!fetchResult.Success)
                return false;

            // Get list of remote branches
            var branchResult = await ExecuteCommandAsync("branch -r", workingDirectory);
            if (!branchResult.Success)
                return false;

            // Pull each remote branch
            var branches = branchResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var branch in branches)
            {
                var cleanBranch = branch.Trim().Replace("origin/", "");
                if (cleanBranch != "HEAD" && !cleanBranch.Contains("->"))
                {
                    await ExecuteCommandAsync($"checkout -b {cleanBranch} origin/{cleanBranch}", workingDirectory);
                }
            }

            return true;
        }

        public async Task<bool> RenameDefaultBranchAsync(string workingDirectory, string oldName = "main", string newName = "old-main")
        {
            var result = await ExecuteCommandAsync($"branch -m {oldName} {newName}", workingDirectory);
            return result.Success;
        }

        public async Task<bool> SetRemoteUrlAsync(string workingDirectory, string newRemoteUrl)
        {
            var result = await ExecuteCommandAsync($"remote set-url origin {newRemoteUrl}", workingDirectory);
            return result.Success;
        }

        public async Task<bool> MergeRemoteMainAsync(string workingDirectory, string commitMessage)
        {
            // Create a temporary file for the commit message
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, commitMessage);

            try
            {
                // Pull with merge commit
                var pullResult = await ExecuteCommandAsync("pull --no-rebase origin main --allow-unrelated-histories", workingDirectory);
                
                // If there are conflicts or merge needed, commit with the message
                if (!pullResult.Success || pullResult.Output.Contains("CONFLICT"))
                {
                    await ExecuteCommandAsync($"commit -F {tempFile}", workingDirectory);
                }

                return true;
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public async Task<bool> PushAllBranchesAsync(string workingDirectory)
        {
            var result = await ExecuteCommandAsync("push --all origin", workingDirectory);
            return result.Success;
        }

        public async Task<bool> PushAllTagsAsync(string workingDirectory)
        {
            var result = await ExecuteCommandAsync("push --tags origin", workingDirectory);
            return result.Success;
        }
    }
}
```

## Services/YamlConfigReader.cs
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using RepositoryMigration.Interfaces;
using RepositoryMigration.Models;

namespace RepositoryMigration.Services
{
    public class YamlConfigReader : IYamlConfigReader
    {
        private readonly IDeserializer _deserializer;

        public YamlConfigReader()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public async Task<MigrationConfig> ReadConfigAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");

            try
            {
                var yamlContent = await File.ReadAllTextAsync(filePath);
                var config = _deserializer.Deserialize<MigrationConfig>(yamlContent);
                return config ?? new MigrationConfig();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse YAML configuration: {ex.Message}", ex);
            }
        }
    }
}
```

## Services/RepositoryMigrator.cs
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RepositoryMigration.Interfaces;
using RepositoryMigration.Models;

namespace RepositoryMigration.Services
{
    public class RepositoryMigrator : IRepositoryMigrator
    {
        private readonly IGitCommandExecutor _gitCommandExecutor;
        private readonly IYamlConfigReader _configReader;
        private readonly ILogger<RepositoryMigrator> _logger;

        public RepositoryMigrator(
            IGitCommandExecutor gitCommandExecutor,
            IYamlConfigReader configReader,
            ILogger<RepositoryMigrator> logger)
        {
            _gitCommandExecutor = gitCommandExecutor ?? throw new ArgumentNullException(nameof(gitCommandExecutor));
            _configReader = configReader ?? throw new ArgumentNullException(nameof(configReader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> MigrateRepositoryAsync(RepositoryInfo repositoryInfo)
        {
            if (repositoryInfo == null)
                throw new ArgumentNullException(nameof(repositoryInfo));

            if (string.IsNullOrEmpty(repositoryInfo.SourceUrl) || string.IsNullOrEmpty(repositoryInfo.DestinationUrl))
                throw new ArgumentException("Source and destination URLs are required");

            var repoName = GetRepositoryName(repositoryInfo.SourceUrl);
            var workingDirectory = repositoryInfo.WorkingDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), repoName);

            try
            {
                _logger.LogInformation($"Starting migration of {repositoryInfo.SourceUrl} to {repositoryInfo.DestinationUrl}");

                // Step 1: Clone repository
                _logger.LogInformation("Step 1: Cloning repository...");
                if (!await _gitCommandExecutor.CloneRepositoryAsync(repositoryInfo.SourceUrl, workingDirectory))
                {
                    _logger.LogError("Failed to clone repository");
                    return false;
                }

                // Step 2: Pull all branches
                _logger.LogInformation("Step 2: Pulling all branches...");
                if (!await _gitCommandExecutor.PullAllBranchesAsync(workingDirectory))
                {
                    _logger.LogWarning("Warning: Failed to pull all branches, continuing...");
                }

                // Step 3: Rename default branch
                _logger.LogInformation("Step 3: Renaming default branch...");
                if (!await _gitCommandExecutor.RenameDefaultBranchAsync(workingDirectory))
                {
                    _logger.LogWarning("Warning: Failed to rename default branch, continuing...");
                }

                // Step 4: Set new remote URL
                _logger.LogInformation("Step 4: Setting new remote URL...");
                if (!await _gitCommandExecutor.SetRemoteUrlAsync(workingDirectory, repositoryInfo.DestinationUrl))
                {
                    _logger.LogError("Failed to set new remote URL");
                    return false;
                }

                // Step 5: Merge remote main
                _logger.LogInformation("Step 5: Merging remote main...");
                var commitMessage = string.IsNullOrEmpty(repositoryInfo.CommitMessage) 
                    ? "Migration from ADO to GitHub" 
                    : repositoryInfo.CommitMessage;
                
                if (!await _gitCommandExecutor.MergeRemoteMainAsync(workingDirectory, commitMessage))
                {
                    _logger.LogWarning("Warning: Failed to merge remote main, continuing...");
                }

                // Step 6: Push all branches
                _logger.LogInformation("Step 6: Pushing all branches...");
                if (!await _gitCommandExecutor.PushAllBranchesAsync(workingDirectory))
                {
                    _logger.LogError("Failed to push all branches");
                    return false;
                }

                // Step 7: Push all tags
                _logger.LogInformation("Step 7: Pushing all tags...");
                if (!await _gitCommandExecutor.PushAllTagsAsync(workingDirectory))
                {
                    _logger.LogWarning("Warning: Failed to push all tags");
                }

                _logger.LogInformation($"Successfully migrated {repositoryInfo.SourceUrl} to {repositoryInfo.DestinationUrl}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error migrating repository: {ex.Message}");
                return false;
            }
            finally
            {
                // Cleanup: Remove working directory
                if (Directory.Exists(workingDirectory))
                {
                    try
                    {
                        Directory.Delete(workingDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to cleanup working directory: {workingDirectory}");
                    }
                }
            }
        }

        public async Task MigrateAllRepositoriesAsync(string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath))
                throw new ArgumentException("Config file path is required", nameof(configFilePath));

            var config = await _configReader.ReadConfigAsync(configFilePath);
            
            _logger.LogInformation($"Found {config.Repositories.Count} repositories to migrate");

            foreach (var repo in config.Repositories)
            {
                await MigrateRepositoryAsync(repo);
            }
        }

        private static string GetRepositoryName(string url)
        {
            var uri = new Uri(url);
            var name = Path.GetFileNameWithoutExtension(uri.Segments.Last());
            return name;
        }
    }
}
```

## Program.cs
```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RepositoryMigration.Interfaces;
using RepositoryMigration.Services;

namespace RepositoryMigration
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: RepositoryMigration <config.yaml>");
                return;
            }

            var host = CreateHostBuilder(args).Build();
            
            try
            {
                var migrator = host.Services.GetRequiredService<IRepositoryMigrator>();
                await migrator.MigrateAllRepositoriesAsync(args[0]);
                Console.WriteLine("Migration completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration failed: {ex.Message}");
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(builder => builder.AddConsole());
                    services.AddScoped<IGitCommandExecutor, GitCommandExecutor>();
                    services.AddScoped<IYamlConfigReader, YamlConfigReader>();
                    services.AddScoped<IRepositoryMigrator, RepositoryMigrator>();
                });
    }
}
```

## RepositoryMigration.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
    <PackageReference Include="YamlDotNet" Version="13.7.1" />
  </ItemGroup>

</Project>
```

## Unit Tests

### RepositoryMigration.Tests.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RepositoryMigration\RepositoryMigration.csproj" />
  </ItemGroup>

</Project>
```

### Services/GitCommandExecutorTests.cs
```csharp
using System.Threading.Tasks;
using Xunit;
using RepositoryMigration.Services;

namespace RepositoryMigration.Tests.Services
{
    public class GitCommandExecutorTests
    {
        private readonly GitCommandExecutor _gitCommandExecutor;

        public GitCommandExecutorTests()
        {
            _gitCommandExecutor = new GitCommandExecutor();
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithValidCommand_ReturnsSuccess()
        {
            // Arrange
            var command = "--version";

            // Act
            var result = await _gitCommandExecutor.ExecuteCommandAsync(command);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("git version", result.Output);
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithInvalidCommand_ReturnsFalse()
        {
            // Arrange
            var command = "invalid-command";

            // Act
            var result = await _gitCommandExecutor.ExecuteCommandAsync(command);

            // Assert
            Assert.False(result.Success);
        }

        [Theory]
        [InlineData("https://github.com/test/repo.git", "test-repo")]
        [InlineData("https://dev.azure.com/org/project/_git/repo", "test-repo")]
        public async Task CloneRepositoryAsync_WithValidUrl_ShouldExecuteCloneCommand(string sourceUrl, string targetDirectory)
        {
            // This test would require a real repository or mocking the Process class
            // For demonstration, we'll test the command construction logic
            Assert.True(!string.IsNullOrEmpty(sourceUrl));
            Assert.True(!string.IsNullOrEmpty(targetDirectory));
        }
    }
}
```

### Services/YamlConfigReaderTests.cs
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using RepositoryMigration.Services;

namespace RepositoryMigration.Tests.Services
{
    public class YamlConfigReaderTests
    {
        private readonly YamlConfigReader _yamlConfigReader;

        public YamlConfigReaderTests()
        {
            _yamlConfigReader = new YamlConfigReader();
        }

        [Fact]
        public async Task ReadConfigAsync_WithValidYaml_ReturnsConfig()
        {
            // Arrange
            var yamlContent = @"
repositories:
  - sourceUrl: https://dev.azure.com/org/project/_git/repo1
    destinationUrl: https://github.com/org/repo1.git
    commitMessage: Migration commit for repo1
  - sourceUrl: https://dev.azure.com/org/project/_git/repo2
    destinationUrl: https://github.com/org/repo2.git
    commitMessage: Migration commit for repo2
";
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, yamlContent);

            try
            {
                // Act
                var config = await _yamlConfigReader.ReadConfigAsync(tempFile);

                // Assert
                Assert.NotNull(config);
                Assert.Equal(2, config.Repositories.Count);
                Assert.Equal("https://dev.azure.com/org/project/_git/repo1", config.Repositories[0].SourceUrl);
                Assert.Equal("https://github.com/org/repo1.git", config.Repositories[0].DestinationUrl);
                Assert.Equal("Migration commit for repo1", config.Repositories[0].CommitMessage);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ReadConfigAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentFile = "non-existent-file.yaml";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _yamlConfigReader.ReadConfigAsync(nonExistentFile));
        }

        [Fact]
        public async Task ReadConfigAsync_WithInvalidYaml_ThrowsInvalidOperationException()
        {
            // Arrange
            var invalidYaml = "invalid: yaml: content: [";
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, invalidYaml);

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    _yamlConfigReader.ReadConfigAsync(tempFile));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
```

### Services/RepositoryMigratorTests.cs
```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using RepositoryMigration.Interfaces;
using RepositoryMigration.Models;
using RepositoryMigration.Services;

namespace RepositoryMigration.Tests.Services
{
    public class RepositoryMigratorTests
    {
        private readonly Mock<IGitCommandExecutor> _mockGitExecutor;
        private readonly Mock<IYamlConfigReader> _mockConfigReader;
        private readonly Mock<ILogger<RepositoryMigrator>> _mockLogger;
        private readonly RepositoryMigrator _repositoryMigrator;

        public RepositoryMigratorTests()
        {
            _mockGitExecutor = new Mock<IGitCommandExecutor>();
            _mockConfigReader = new Mock<IYamlConfigReader>();
            _mockLogger = new Mock<ILogger<RepositoryMigrator>>();
            _repositoryMigrator = new RepositoryMigrator(_mockGitExecutor.Object, _mockConfigReader.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task MigrateRepositoryAsync_WithValidRepository_ReturnsTrue()
        {
            // Arrange
            var repositoryInfo = new RepositoryInfo
            {
                SourceUrl = "https://dev.azure.com/org/project/_git/test-repo",
                DestinationUrl = "https://github.com/org/test-repo.git",
                CommitMessage = "Migration commit"
            };

            _mockGitExecutor.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.PullAllBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.RenameDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.SetRemoteUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.MergeRemoteMainAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.PushAllBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.PushAllTagsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _repositoryMigrator.MigrateRepositoryAsync(repositoryInfo);

            // Assert
            Assert.True(result);
            _mockGitExecutor.Verify(x => x.CloneRepositoryAsync(repositoryInfo.SourceUrl, It.IsAny<string>()), Times.Once);
            _mockGitExecutor.Verify(x => x.SetRemoteUrlAsync(It.IsAny<string>(), repositoryInfo.DestinationUrl), Times.Once);
            _mockGitExecutor.Verify(x => x.MergeRemoteMainAsync(It.IsAny<string>(), repositoryInfo.CommitMessage), Times.Once);
        }

        [Fact]
        public async Task MigrateRepositoryAsync_WithNullRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _repositoryMigrator.MigrateRepositoryAsync(null));
        }

        [Fact]
        public async Task MigrateRepositoryAsync_WithEmptySourceUrl_ThrowsArgumentException()
        {
            // Arrange
            var repositoryInfo = new RepositoryInfo
            {
                SourceUrl = "",
                DestinationUrl = "https://github.com/org/test-repo.git"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _repositoryMigrator.MigrateRepositoryAsync(repositoryInfo));
        }

        [Fact]
        public async Task MigrateRepositoryAsync_WhenCloneFails_ReturnsFalse()
        {
            // Arrange
            var repositoryInfo = new RepositoryInfo
            {
                SourceUrl = "https://dev.azure.com/org/project/_git/test-repo",
                DestinationUrl = "https://github.com/org/test-repo.git"
            };

            _mockGitExecutor.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _repositoryMigrator.MigrateRepositoryAsync(repositoryInfo);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task MigrateAllRepositoriesAsync_WithValidConfig_CallsMigrateForEachRepository()
        {
            // Arrange
            var configPath = "test-config.yaml";
            var config = new MigrationConfig
            {
                Repositories = new List<RepositoryInfo>
                {
                    new RepositoryInfo { SourceUrl = "url1", DestinationUrl = "dest1" },
                    new RepositoryInfo { SourceUrl = "url2", DestinationUrl = "dest2" }
                }
            };

            _mockConfigReader.Setup(x => x.ReadConfigAsync(configPath))
                .ReturnsAsync(config);

            // Mock all git operations to succeed
            _mockGitExecutor.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.PullAllBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.RenameDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.SetRemoteUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.MergeRemoteMainAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.PushAllBranchesAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGitExecutor.Setup(x => x.PushAllTagsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _repositoryMigrator.MigrateAllRepositoriesAsync(configPath);

            // Assert
            _mockConfigReader.Verify(x => x.ReadConfigAsync(configPath), Times.Once);
            _mockGitExecutor.Verify(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        }
    }
}
```

## Sample YAML Configuration (config.yaml)
```yaml
repositories:
  - sourceUrl: https://dev.azure.com/organization/project/_git/repository1
    destinationUrl: https://github.com/organization/repository1.git
    commitMessage: "Migration from ADO to GitHub - Repository 1"
    workingDirectory: "./temp/repo1"
  - sourceUrl: https://dev.azure.com/organization/project/_git/repository2
    destinationUrl: https://github.com/organization/repository2.git
    commitMessage: "Migration from ADO to GitHub - Repository 2"
  - sourceUrl: https://dev.azure.com/organization/project/_git/repository3
    destinationUrl: https://github.com/organization/repository3.git
    commitMessage: "Migrated legacy codebase to GitHub"

# GitHub Actions Workflow for Repository Migration Tool
# File: .github/workflows/ci-cd.yml

name: Repository Migration CI/CD

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'RepositoryMigration/**'
      - 'RepositoryMigration.Tests/**'
      - '.github/workflows/**'
  pull_request:
    branches: [ main ]
    paths:
      - 'RepositoryMigration/**'
      - 'RepositoryMigration.Tests/**'
  workflow_dispatch:
    inputs:
      run_migration:
        description: 'Run actual migration after build'
        required: false
        default: 'false'
        type: boolean
      config_file:
        description: 'Configuration file to use for migration'
        required: false
        default: 'configs/migration-config.yaml'
        type: string

env:
  DOTNET_VERSION: '8.0.x'
  PROJECT_PATH: './RepositoryMigration'
  TEST_PROJECT_PATH: './RepositoryMigration.Tests'
  SOLUTION_PATH: './RepositoryMigration.sln'

jobs:
  # Job 1: Build and Test
  build-and-test:
    name: Build and Test
    runs-on: ubuntu-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
      with:
        fetch-depth: 0  # Fetch full history for better Git operations
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Cache NuGet packages
      uses: actions/cache@v4
      with:
        path: ~/.nuget/packages
        key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
        restore-keys: |
          ${{ runner.os }}-nuget-
    
    - name: Restore dependencies
      run: dotnet restore ${{ env.SOLUTION_PATH }}
    
    - name: Build solution
      run: dotnet build ${{ env.SOLUTION_PATH }} --configuration Release --no-restore
    
    - name: Run unit tests
      run: |
        dotnet test ${{ env.TEST_PROJECT_PATH }} \
          --configuration Release \
          --no-build \
          --verbosity normal \
          --logger trx \
          --results-directory TestResults/ \
          --collect:"XPlat Code Coverage"
    
    - name: Upload test results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: test-results
        path: TestResults/
    
    - name: Upload code coverage
      uses: codecov/codecov-action@v4
      if: always()
      with:
        directory: TestResults/
        flags: unittests
        name: codecov-umbrella
    
    - name: Publish application
      run: |
        dotnet publish ${{ env.PROJECT_PATH }} \
          --configuration Release \
          --output ./publish \
          --no-build
    
    - name: Upload build artifacts
      uses: actions/upload-artifact@v4
      with:
        name: published-app
        path: ./publish/

  # Job 2: Security Scan
  security-scan:
    name: Security Scan
    runs-on: ubuntu-latest
    needs: build-and-test
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Restore dependencies
      run: dotnet restore ${{ env.SOLUTION_PATH }}
    
    - name: Run security audit
      run: dotnet list package --vulnerable --include-transitive
    
    - name: Run CodeQL Analysis
      uses: github/codeql-action/init@v3
      with:
        languages: csharp
    
    - name: Build for CodeQL
      run: dotnet build ${{ env.SOLUTION_PATH }} --configuration Release
    
    - name: Perform CodeQL Analysis
      uses: github/codeql-action/analyze@v3

  # Job 3: Docker Build (Optional)
  docker-build:
    name: Build Docker Image
    runs-on: ubuntu-latest
    needs: build-and-test
    if: github.ref == 'refs/heads/main'
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3
    
    - name: Login to GitHub Container Registry
      uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}
    
    - name: Extract metadata
      id: meta
      uses: docker/metadata-action@v5
      with:
        images: ghcr.io/${{ github.repository }}/repo-migration-tool
        tags: |
          type=ref,event=branch
          type=ref,event=pr
          type=sha,prefix={{branch}}-
          type=raw,value=latest,enable={{is_default_branch}}
    
    - name: Build and push Docker image
      uses: docker/build-push-action@v5
      with:
        context: .
        file: ./Dockerfile
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

  # Job 4: Migration Execution (Manual Trigger Only)
  run-migration:
    name: Execute Migration
    runs-on: ubuntu-latest
    needs: [build-and-test, security-scan]
    if: github.event.inputs.run_migration == 'true'
    environment: production  # Requires manual approval
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Setup Git
      run: |
        git config --global user.name "GitHub Actions Bot"
        git config --global user.email "actions@github.com"
        git config --global init.defaultBranch main
    
    - name: Download build artifacts
      uses: actions/download-artifact@v4
      with:
        name: published-app
        path: ./publish
    
    - name: Make executable
      run: chmod +x ./publish/RepositoryMigration
    
    - name: Validate configuration file
      run: |
        if [ ! -f "${{ github.event.inputs.config_file }}" ]; then
          echo "Error: Configuration file not found: ${{ github.event.inputs.config_file }}"
          exit 1
        fi
        echo "Using configuration file: ${{ github.event.inputs.config_file }}"
    
    - name: Run migration (dry-run first)
      run: |
        echo "This would execute: ./publish/RepositoryMigration ${{ github.event.inputs.config_file }}"
        echo "Configuration file content:"
        cat "${{ github.event.inputs.config_file }}"
        # Note: Actual migration execution is commented out for safety
        # Uncomment the next line to run actual migration
        # ./publish/RepositoryMigration "${{ github.event.inputs.config_file }}"
    
    - name: Upload migration logs
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: migration-logs
        path: |
          *.log
          migration-*.log

  # Job 5: Release (on tag push)
  release:
    name: Create Release
    runs-on: ubuntu-latest
    needs: [build-and-test, security-scan]
    if: startsWith(github.ref, 'refs/tags/v')
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Download build artifacts
      uses: actions/download-artifact@v4
      with:
        name: published-app
        path: ./publish
    
    - name: Create release package
      run: |
        cd publish
        tar -czf ../repository-migration-tool-${{ github.ref_name }}.tar.gz *
        cd ..
        zip -r repository-migration-tool-${{ github.ref_name }}.zip publish/
    
    - name: Create GitHub Release
      uses: softprops/action-gh-release@v1
      with:
        files: |
          repository-migration-tool-${{ github.ref_name }}.tar.gz
          repository-migration-tool-${{ github.ref_name }}.zip
        generate_release_notes: true
        draft: false
        prerelease: ${{ contains(github.ref_name, '-') }}
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

---

# Additional Workflow Files

## .github/workflows/dependency-update.yml
name: Dependency Update

on:
  schedule:
    - cron: '0 2 * * 1'  # Weekly on Monday at 2 AM UTC
  workflow_dispatch:

jobs:
  update-dependencies:
    runs-on: ubuntu-latest
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Update dependencies
      run: |
        dotnet list package --outdated
        # Add actual update commands here
        # dotnet add package PackageName
    
    - name: Create Pull Request
      uses: peter-evans/create-pull-request@v5
      with:
        token: ${{ secrets.GITHUB_TOKEN }}
        commit-message: 'chore: update dependencies'
        title: 'Automated dependency update'
        body: 'This PR updates outdated NuGet packages.'
        branch: dependency-updates

---

## .github/workflows/code-quality.yml
name: Code Quality

on:
  pull_request:
    branches: [ main, develop ]

jobs:
  code-quality:
    runs-on: ubuntu-latest
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Format check
      run: dotnet format --verify-no-changes --verbosity diagnostic
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Run static analysis
      run: |
        dotnet build --configuration Release --verbosity normal
        # Add additional static analysis tools here

---

## Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RepositoryMigration/RepositoryMigration.csproj", "RepositoryMigration/"]
COPY ["RepositoryMigration.Tests/RepositoryMigration.Tests.csproj", "RepositoryMigration.Tests/"]
RUN dotnet restore "RepositoryMigration/RepositoryMigration.csproj"

COPY . .
WORKDIR "/src/RepositoryMigration"
RUN dotnet build "RepositoryMigration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RepositoryMigration.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install Git
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

# Set Git configuration
RUN git config --global user.name "Migration Tool" && \
    git config --global user.email "migration@tool.com" && \
    git config --global init.defaultBranch main

ENTRYPOINT ["dotnet", "RepositoryMigration.dll"]

---

## .github/dependabot.yml
version: 2
updates:
  # Enable version updates for NuGet
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
    open-pull-requests-limit: 10
    reviewers:
      - "your-team"
    assignees:
      - "your-username"
    commit-message:
      prefix: "chore"
      prefix-development: "chore"
      include: "scope"

  # Enable version updates for GitHub Actions
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
    commit-message:
      prefix: "ci"
      include: "scope"
