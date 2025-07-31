using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace GitMigration.Tests
{
    public class GitRepositoryMigrationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IGitService> _mockGitService;
        private readonly Mock<IFileSystemService> _mockFileSystemService;
        private readonly GitRepositoryMigrator _migrator;

        public GitRepositoryMigrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockGitService = new Mock<IGitService>();
            _mockFileSystemService = new Mock<IFileSystemService>();
            _migrator = new GitRepositoryMigrator(_mockGitService.Object, _mockFileSystemService.Object);
        }

        [Fact]
        public async Task MigrateRepository_ShouldExecuteAllStepsInCorrectOrder_WhenValidParametersProvided()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Merge remote main branch";

            SetupMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            var result = await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            Assert.True(result.IsSuccess);
            VerifyAllStepsExecuted(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);
        }

        [Fact]
        public async Task MigrateRepository_Step1_GitClone_ShouldCloneFromSourceUrl()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.CloneRepositoryAsync(sourceUrl, repositoryDirectory))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.CloneRepositoryAsync(sourceUrl, repositoryDirectory), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step2_ChangeDirectory_ShouldNavigateToRepositoryDirectory()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockFileSystemService.Setup(x => x.ChangeDirectory(repositoryDirectory))
                                  .Returns(true)
                                  .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockFileSystemService.Verify(x => x.ChangeDirectory(repositoryDirectory), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step3_PullAllBranches_ShouldFetchAllRemoteBranches()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.PullAllBranchesAsync())
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.PullAllBranchesAsync(), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step4_RenameDefaultBranch_ShouldRenameMainToOldMain()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.RenameBranchAsync("main", "old-main"))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.RenameBranchAsync("main", "old-main"), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step5_SetNewRemoteRepository_ShouldUpdateOriginToDestinationUrl()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.SetRemoteUrlAsync("origin", destinationUrl))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.SetRemoteUrlAsync("origin", destinationUrl), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step6_MergeRemoteMain_ShouldPullWithAllowUnrelatedHistories()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.PullWithUnrelatedHistoriesAsync("origin", "main"))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.PullWithUnrelatedHistoriesAsync("origin", "main"), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step7_CommitMerge_ShouldCommitWithProvidedMessage()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Custom merge commit message";

            _mockGitService.Setup(x => x.CommitMergeAsync(commitMessage))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.CommitMergeAsync(commitMessage), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step8_PushAllBranches_ShouldPushAllBranchesToRemote()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.PushAllBranchesAsync("origin"))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.PushAllBranchesAsync("origin"), Times.Once);
        }

        [Fact]
        public async Task MigrateRepository_Step9_PushAllTags_ShouldPushAllTagsToRemote()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.PushAllTagsAsync("origin"))
                          .ReturnsAsync(true)
                          .Verifiable();

            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Act
            await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            _mockGitService.Verify(x => x.PushAllTagsAsync("origin"), Times.Once);
        }

        [Theory]
        [InlineData("", "valid-destination", "valid-directory", "valid-message")]
        [InlineData("valid-source", "", "valid-directory", "valid-message")]
        [InlineData("valid-source", "valid-destination", "", "valid-message")]
        [InlineData("valid-source", "valid-destination", "valid-directory", "")]
        public async Task MigrateRepository_ShouldThrowArgumentException_WhenRequiredParametersAreNullOrEmpty(
            string sourceUrl, string destinationUrl, string repositoryDirectory, string commitMessage)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage));
        }

        [Fact]
        public async Task MigrateRepository_ShouldReturnFailure_WhenGitCloneFails()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.CloneRepositoryAsync(sourceUrl, repositoryDirectory))
                          .ReturnsAsync(false);

            // Act
            var result = await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to clone repository", result.ErrorMessage);
        }

        [Fact]
        public async Task MigrateRepository_ShouldReturnFailure_WhenDirectoryChangeFails()
        {
            // Arrange
            var sourceUrl = "https://github.com/source/repo.git";
            var destinationUrl = "https://ghes.company.com/destination/repo.git";
            var repositoryDirectory = "test-repo";
            var commitMessage = "Test commit";

            _mockGitService.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>()))
                          .ReturnsAsync(true);
            _mockFileSystemService.Setup(x => x.ChangeDirectory(repositoryDirectory))
                                  .Returns(false);

            // Act
            var result = await _migrator.MigrateRepositoryAsync(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to change directory", result.ErrorMessage);
        }

        private void SetupMockExpectations(string sourceUrl, string destinationUrl, string repositoryDirectory, string commitMessage)
        {
            _mockGitService.Setup(x => x.CloneRepositoryAsync(sourceUrl, repositoryDirectory)).ReturnsAsync(true);
            _mockFileSystemService.Setup(x => x.ChangeDirectory(repositoryDirectory)).Returns(true);
            SetupRemainingMockExpectations(sourceUrl, destinationUrl, repositoryDirectory, commitMessage);
        }

        private void SetupRemainingMockExpectations(string sourceUrl, string destinationUrl, string repositoryDirectory, string commitMessage)
        {
            _mockGitService.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _mockFileSystemService.Setup(x => x.ChangeDirectory(It.IsAny<string>())).Returns(true);
            _mockGitService.Setup(x => x.PullAllBranchesAsync()).ReturnsAsync(true);
            _mockGitService.Setup(x => x.RenameBranchAsync("main", "old-main")).ReturnsAsync(true);
            _mockGitService.Setup(x => x.SetRemoteUrlAsync("origin", destinationUrl)).ReturnsAsync(true);
            _mockGitService.Setup(x => x.PullWithUnrelatedHistoriesAsync("origin", "main")).ReturnsAsync(true);
            _mockGitService.Setup(x => x.CommitMergeAsync(commitMessage)).ReturnsAsync(true);
            _mockGitService.Setup(x => x.PushAllBranchesAsync("origin")).ReturnsAsync(true);
            _mockGitService.Setup(x => x.PushAllTagsAsync("origin")).ReturnsAsync(true);
        }

        private void VerifyAllStepsExecuted(string sourceUrl, string destinationUrl, string repositoryDirectory, string commitMessage)
        {
            _mockGitService.Verify(x => x.CloneRepositoryAsync(sourceUrl, repositoryDirectory), Times.Once);
            _mockFileSystemService.Verify(x => x.ChangeDirectory(repositoryDirectory), Times.Once);
            _mockGitService.Verify(x => x.PullAllBranchesAsync(), Times.Once);
            _mockGitService.Verify(x => x.RenameBranchAsync("main", "old-main"), Times.Once);
            _mockGitService.Verify(x => x.SetRemoteUrlAsync("origin", destinationUrl), Times.Once);
            _mockGitService.Verify(x => x.PullWithUnrelatedHistoriesAsync("origin", "main"), Times.Once);
            _mockGitService.Verify(x => x.CommitMergeAsync(commitMessage), Times.Once);
            _mockGitService.Verify(x => x.PushAllBranchesAsync("origin"), Times.Once);
            _mockGitService.Verify(x => x.PushAllTagsAsync("origin"), Times.Once);
        }
    }

    // Supporting interfaces and classes that would be part of your implementation
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

    public interface IFileSystemService
    {
        bool ChangeDirectory(string path);
    }

    public class MigrationResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class GitRepositoryMigrator
    {
        private readonly IGitService _gitService;
        private readonly IFileSystemService _fileSystemService;

        public GitRepositoryMigrator(IGitService gitService, IFileSystemService fileSystemService)
        {
            _gitService = gitService;
            _fileSystemService = fileSystemService;
        }

        public async Task<MigrationResult> MigrateRepositoryAsync(string sourceUrl, string destinationUrl, 
            string repositoryDirectory, string commitMessage)
        {
            // Validation logic would be implemented here
            if (string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(destinationUrl) || 
                string.IsNullOrEmpty(repositoryDirectory) || string.IsNullOrEmpty(commitMessage))
            {
                throw new ArgumentException("All parameters are required");
            }

            // Implementation would execute all the steps here
            // This is just a skeleton for the test structure
            
            return new MigrationResult { IsSuccess = true };
        }
    }
}


/*<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
<PackageReference Include="Moq" Version="4.18.4" />*/
