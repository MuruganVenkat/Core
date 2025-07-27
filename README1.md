# Repository Migration Tool

[![Build Status](https://github.com/yourusername/repo-migration-tool/workflows/Build%20and%20Release/badge.svg)](https://github.com/yourusername/repo-migration-tool/actions)
[![NuGet Version](https://img.shields.io/nuget/v/RepoMigrationTool.svg)](https://www.nuget.org/packages/RepoMigrationTool/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A powerful command-line tool for migrating repositories from Azure DevOps to GitHub with support for batch processing, comprehensive authentication, and advanced configuration options.

## ✨ Features

- **Batch Migration**: Process multiple repositories in a single run
- **JSON Configuration**: Flexible configuration with support for multiple environments
- **Secure Authentication**: Personal Access Token (PAT) support for both ADO and GitHub
- **Comprehensive Logging**: Detailed progress tracking with colored output
- **Dry Run Mode**: Test migrations without making actual changes
- **Repository Filtering**: Selective migration with wildcard support
- **Connection Testing**: Validate repository access before migration
- **Cross-Platform**: Works on Windows, Linux, and macOS

## 🚀 Installation

### Option 1: .NET Global Tool (Recommended)

```bash
# Install from NuGet
dotnet tool install --global RepoMigrationTool

# Install from GitHub Packages
dotnet tool install --global RepoMigrationTool --add-source https://nuget.pkg.github.com/yourusername/index.json
```

### Option 2: Download Standalone Executable

Download the latest release for your platform from the [Releases page](https://github.com/yourusername/repo-migration-tool/releases):

- **Windows**: `repo-migration-tool-win-x64.zip`
- **Linux**: `repo-migration-tool-linux-x64.tar.gz`
- **macOS Intel**: `repo-migration-tool-osx-x64.tar.gz`
- **macOS Apple Silicon**: `repo-migration-tool-osx-arm64.tar.gz`

### Option 3: Build from Source

```bash
git clone https://github.com/yourusername/repo-migration-tool.git
cd repo-migration-tool
dotnet build --configuration Release
dotnet pack --configuration Release
dotnet tool install --global --add-source ./nupkg RepoMigrationTool
```

## 📋 Prerequisites

- **Git**: Must be installed and accessible from command line
- **.NET 6.0+**: Required for running the tool
- **Personal Access Tokens**: 
  - Azure DevOps PAT with `Code (Read)` permission
  - GitHub PAT with `repo` scope

## 🔧 Quick Start

### 1. Initialize Configuration

```bash
# Create a sample configuration file
repo-migration-tool init

# Or specify a custom path
repo-migration-tool init --config my-migration.json
```

### 2. Edit Configuration

Edit the generated `migration-config.json` file with your actual values:

```json
{
  "settings": {
    "workingDirectory": "./temp-migration",
    "stopOnFirstError": false,
    "cleanupTempDirectory": true
  },
  "authentication": {
    "adoPat": "your_ado_personal_access_token",
    "gitHubPat": "your_github_personal_access_token"
  },
  "repositories": [
    {
      "name": "My Application",
      "sourceUrl": "https://dev.azure.com/org/project/_git/myapp",
      "destinationUrl": "https://github.com/username/myapp.git",
      "commitMessage": "Migration from ADO to GitHub",
      "enabled": true,
      "cleanupAfterMigration": true
    }
  ]
}
```

### 3. Validate Configuration

```bash
# Validate configuration syntax and structure
repo-migration-tool validate

# Test connections to repositories
repo-migration-tool validate --test-connections
```

### 4. Run Migration

```bash
# Migrate all enabled repositories
repo-migration-tool migrate

# Dry run to test without making changes
repo-migration-tool migrate --dry-run

# Migrate specific repositories
repo-migration-tool migrate --repositories "MyApp*" "TestRepo"

# Use custom configuration file
repo-migration-tool migrate --config production-config.json
```

## 📖 Commands

### `init`
Initialize a new configuration file with sample data.

```bash
repo-migration-tool init [options]

Options:
  -c, --config <path>    Configuration file path (default: migration-config.json)
  -f, --force           Overwrite existing configuration file
```

### `validate`
Validate configuration file and optionally test connections.

```bash
repo-migration-tool validate [options]

Options:
  -c, --config <path>           Configuration file path
  -t, --test-connections        Test repository connections
```

### `migrate`
Perform repository migration based on configuration.

```bash
repo-migration-tool migrate [options]

Options:
  -c, --config <path>           Configuration file path
  -v, --verbose                 Enable verbose logging
  -d, --dry-run                 Perform dry run without actual changes
  -r, --repositories <names>    Filter repositories by name (supports wildcards)
```

## ⚙️ Configuration Reference

### Settings Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `workingDirectory` | string | `"./temp-migration"` | Temporary directory for cloning repositories |
| `stopOnFirstError` | boolean | `false` | Stop migration if any repository fails |
| `cleanupTempDirectory` | boolean | `true` | Clean up working directory after migration |

### Authentication Section

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `adoPat` | string | ✅ Yes | Azure DevOps Personal Access Token |
| `gitHubPat` | string | ✅ Yes | GitHub Personal Access Token |

### Repository Section

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `name` | string | ✅ Yes | - | Display name for the repository |
| `sourceUrl` | string | ✅ Yes | - | Azure DevOps repository URL |
| `destinationUrl` | string | ✅ Yes | - | GitHub repository URL |
| `commitMessage` | string | ✅ Yes | - | Commit message for merge operations |
| `enabled` | boolean | No | `true` | Whether to process this repository |
| `cleanupAfterMigration` | boolean | No | `true` | Delete local clone after migration |

## 🔐 Security Best Practices

### Token Management

1. **Never commit tokens to version control**
2. **Use environment variables for CI/CD**:
   ```bash
   export ADO_PAT="your_ado_token"
   export GITHUB_PAT="your_github_token"
   ```
3. **Set appropriate token permissions**:
   - **ADO**: `Code (Read)` minimum
   - **GitHub**: `repo` scope for private repositories
4. **Rotate tokens regularly**
5. **Use separate tokens for different environments**

### Token Permissions

#### Azure DevOps PAT Requirements
- **Code (Read)**: Clone repositories
- **Code (Read & Write)**: If accessing multiple projects

#### GitHub PAT Requirements
- **repo**: Full control of private repositories
- **workflow**: If repositories contain GitHub Actions

## 🔧 Advanced Usage

### Environment-Specific Configurations

Create separate configuration files for different environments:

```bash
# Development
repo-migration-tool migrate --config dev-config.json

# Staging  
repo-migration-tool migrate --config staging-config.json

# Production
repo-migration-tool migrate --config prod-config.json
```

### Batch Processing Script

```bash
#!/bin/bash
# migrate-all.sh

configs=("dev-config.json" "staging-config.json" "prod-config.json")

for config in "${configs[@]}"; do
    echo "Processing: $config"
    repo-migration-tool migrate --config "$config"
done
```

### CI/CD Integration

#### GitHub Actions

```yaml
- name: Install Migration Tool
  run: dotnet tool install --global RepoMigrationTool

- name: Run Migration
  run: repo-migration-tool migrate --config migration-config.json
  env:
    ADO_PAT: ${{ secrets.ADO_PAT }}
    GITHUB_PAT: ${{ secrets.GITHUB_PAT }}
```

#### Azure DevOps Pipeline

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Install Migration Tool'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'install --global RepoMigrationTool'

- task: DotNetCoreCLI@2
  displayName: 'Run Migration'
  inputs:
    command: 'custom'
    custom: 'repo-migration-tool'
    arguments: 'migrate --config migration-config.json'
```

## 🐛 Troubleshooting

### Common Issues

1. **Authentication Failures**
   - Verify PAT tokens are valid and not expired
   - Check token permissions
   - Ensure repositories are accessible

2. **Git Command Failures**
   - Verify Git is installed and in PATH
   - Check network connectivity
   - Validate repository URLs

3. **Configuration Errors**
   - Use `validate` command to check syntax
   - Verify all required fields are present
   - Check JSON formatting

### Debug Mode

Enable verbose logging for detailed troubleshooting:

```bash
repo-migration-tool migrate --verbose
```

### Dry Run Testing

Test your configuration without making changes:

```bash
repo-migration-tool migrate --dry-run
```

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

### Development Setup

```bash
git clone https://github.com/yourusername/repo-migration-tool.git
cd repo-migration-tool
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [System.CommandLine](https://github.com/dotnet/command-line-api)
- Inspired by the need for efficient repository migrations
- Thanks to all contributors and users

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/repo-migration-tool/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/repo-migration-tool/discussions)
- **Documentation**: [Wiki](https://github.com/yourusername/repo-migration-tool/wiki)

---

**Made with ❤️ for the DevOps community**
