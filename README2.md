# Repository Migration Tool - Deployment and Usage Guide

## 🚀 Complete GitHub Package Setup

This guide walks you through converting your C# repository migration tool into a professional GitHub package with NuGet distribution.

## 📁 Project Structure Overview

After setup, your project will have this structure:

```
repo-migration-tool/
├── .github/workflows/
│   └── release.yml                 # GitHub Actions CI/CD
├── src/
│   ├── Program.cs                  # Your main C# code
│   └── RepoMigrationTool.csproj    # Project file
├── tests/                          # Unit tests (optional)
├── docs/                           # Documentation
├── samples/
│   └── sample-config.json         # Sample configuration
├── scripts/
│   ├── build.ps1                  # PowerShell build script
│   └── build.sh                   # Bash build script
├── nupkg/                         # NuGet packages output
├── publish/                       # Standalone executables
├── README.md                      # Main documentation
├── CHANGELOG.md                   # Version history
├── CONTRIBUTING.md                # Contribution guidelines
├── LICENSE                        # MIT license
├── .gitignore                     # Git ignore rules
└── nuget.config                   # NuGet configuration
```

## 🛠️ Setup Instructions

### Step 1: Run the Setup Script

Use the provided PowerShell setup script to create the project structure:

```powershell
# Download and run the setup script
.\setup-package.ps1 -GitHubUsername "yourusername" -AuthorName "Your Name" -CompanyName "Your Company"

# Or with Git initialization
.\setup-package.ps1 -GitHubUsername "yourusername" -AuthorName "Your Name" -InitializeGit
```

### Step 2: Copy Your C# Code

1. Copy your updated C# migration code into `src/Program.cs`
2. Ensure all necessary using statements are included
3. Update the namespace to match the project structure

### Step 3: Create GitHub Repository

1. Go to [GitHub](https://github.com) and create a new repository
2. Name it `repo-migration-tool` (or your chosen project name)
3. Don't initialize with README (we already have one)
4. Make it public if you want to distribute via NuGet.org

### Step 4: Connect to GitHub

```bash
cd repo-migration-tool
git remote add origin https://github.com/yourusername/repo-migration-tool.git
git branch -M main
git push -u origin main
```

### Step 5: Configure GitHub Secrets

In your GitHub repository, go to **Settings → Secrets and variables → Actions** and add:

| Secret Name | Description | How to Get |
|-------------|-------------|------------|
| `NUGET_API_KEY` | NuGet.org API key for publishing | [NuGet.org Account → API Keys](https://www.nuget.org/account/apikeys) |

**Note:** `GITHUB_TOKEN` is automatically provided by GitHub Actions.

### Step 6: Test Local Build

```powershell
# PowerShell
.\scripts\build.ps1 -Pack -Test

# Or Bash
chmod +x scripts/build.sh
./scripts/build.sh --pack --test
```

## 📦 Publishing Process

### Automatic Publishing (Recommended)

1. **Create a Release Tag:**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **GitHub Actions will automatically:**
   - Build the project
   - Run tests
   - Create NuGet packages
   - Publish to NuGet.org
   - Publish to GitHub Packages
   - Create GitHub Release with binaries

### Manual Publishing

If you prefer manual control:

```bash
# Build and pack
dotnet pack src/ --configuration Release --output ./nupkg

# Publish to NuGet.org
dotnet nuget push ./nupkg/*.nupkg --api-key YOUR_NUGET_API_KEY --source https://api.nuget.org/v3/index.json

# Publish to GitHub Packages
dotnet nuget add source --username YOUR_USERNAME --password YOUR_GITHUB_TOKEN --store-password-in-clear-text --name github "https://nuget.pkg.github.com/YOUR_USERNAME/index.json"
dotnet nuget push ./nupkg/*.nupkg --api-key YOUR_GITHUB_TOKEN --source github
```

## 🎯 Usage Examples

### Installation

Once published, users can install your tool:

```bash
# Install from NuGet.org
dotnet tool install --global RepoMigrationTool

# Install from GitHub Packages
dotnet tool install --global RepoMigrationTool --add-source https://nuget.pkg.github.com/yourusername/index.json
```

### Command Usage

```bash
# Initialize configuration
repo-migration-tool init

# Validate configuration
repo-migration-tool validate --test-connections

# Run migration
repo-migration-tool migrate --config my-config.json

# Dry run
repo-migration-tool migrate --dry-run

# Filter repositories
repo-migration-tool migrate --repositories "Frontend*" "API*"

# Verbose logging
repo-migration-tool migrate --verbose
```

### JSON Configuration Usage

Users can create and use configuration files:

```json
{
  "settings": {
    "workingDirectory": "./temp-migration",
    "stopOnFirstError": false,
    "cleanupTempDirectory": true
  },
  "authentication": {
    "adoPat": "your_ado_token",
    "gitHubPat": "your_github_token"
  },
  "repositories": [
    {
      "name": "My Application",
      "sourceUrl": "https://dev.azure.com/org/project/_git/myapp",
      "destinationUrl": "https://github.com/user/myapp.git",
      "commitMessage": "Migration from ADO to GitHub",
      "enabled": true,
      "cleanupAfterMigration": true
    }
  ]
}
```

## 🔧 Advanced Configuration

### Multiple Environment Support

Create environment-specific configurations:

```bash
# Development
repo-migration-tool migrate --config configs/dev-config.json

# Staging
repo-migration-tool migrate --config configs/staging-config.json

# Production
repo-migration-tool migrate --config configs/prod-config.json
```

### Batch Processing Scripts

Create automation scripts for multiple environments:

**PowerShell:**
```powershell
# batch-migrate.ps1
$configs = @("dev-config.json", "staging-config.json", "prod-config.json")

foreach ($config in $configs) {
    Write-Host "Processing: $config" -ForegroundColor Green
    repo-migration-tool migrate --config $config
}
```

**Bash:**
```bash
#!/bin/bash
# batch-migrate.sh
configs=("dev-config.json" "staging-config.json" "prod-config.json")

for config in "${configs[@]}"; do
    echo "Processing: $config"
    repo-migration-tool migrate --config "$config"
done
```

## 🔐 Security Considerations

### Token Management

1. **Environment Variables:**
   ```bash
   export ADO_PAT="your_ado_token"
   export GITHUB_PAT="your_github_token"
   ```

2. **Configuration File Security:**
   - Never commit configuration files with real tokens
   - Use `.gitignore` to exclude sensitive configs
   - Consider encrypting configuration files for production

3. **CI/CD Security:**
   - Use GitHub Secrets for tokens
   - Rotate tokens regularly
   - Use minimum required permissions

### Token Permissions

**Azure DevOps PAT:**
- `Code (Read)` - Minimum for cloning
- `Code (Read & Write)` - For multiple projects

**GitHub PAT:**
- `repo` - Full repository access
- `workflow` - If repositories contain GitHub Actions

## 📊 Monitoring and Logging

The tool provides comprehensive logging:

- ✅ Success operations (green)
- ❌ Error messages (red)
- ⚠️ Warning messages (yellow)
- ℹ️ Information messages (cyan)
- 🔍 Debug messages (gray, verbose mode only)

### Log Levels

```bash
# Standard logging
repo-migration-tool migrate

# Verbose logging (includes debug info)
repo-migration-tool migrate --verbose

# Dry run (safe testing)
repo-migration-tool migrate --dry-run
```

## 🚨 Troubleshooting

### Common Issues

1. **Tool Not Found After Installation:**
   ```bash
   # Refresh PATH or restart terminal
   # Verify installation
   dotnet tool list --global
   ```

2. **Authentication Failures:**
   ```bash
   # Test configuration
   repo-migration-tool validate --test-connections
   
   # Check token permissions and expiration
   ```

3. **Build Failures:**
   ```bash
   # Clean and rebuild
   dotnet clean src/
   dotnet build src/
   ```

4. **Package Publishing Issues:**
   - Verify NuGet API key is correct
   - Check package name isn't already taken
   - Ensure GitHub token has package permissions

### Getting Help

1. **GitHub Issues:** Report bugs and feature requests
2. **GitHub Discussions:** Ask questions and share experiences
3. **Documentation:** Check the README and wiki

## 🎉 Success Metrics

After successful deployment, you'll have:

- ✅ Professional NuGet package
- ✅ Cross-platform support (Windows, Linux, macOS)
- ✅ Automated CI/CD pipeline
- ✅ Comprehensive documentation
- ✅ Secure authentication handling
- ✅ Batch processing capabilities
- ✅ JSON configuration support
- ✅ Community-friendly open source project

## 🔄 Maintenance and Updates

### Version Management

1. **Update Version Number:**
   - Edit `src/RepoMigrationTool.csproj`
   - Update `<Version>` property
   - Update CHANGELOG.md

2. **Create Release:**
   ```bash
   git tag v1.1.0
   git push origin v1.1.0
   ```

3. **Monitor Release:**
   - Check GitHub Actions workflow
   - Verify NuGet.org publication
   - Test installation from NuGet

### Community Management

- Respond to GitHub issues promptly
- Review and merge pull requests
- Update documentation as needed
- Maintain compatibility with latest .NET versions
- Keep dependencies updated

## 🏆 Best Practices

1. **Semantic Versioning:** Use MAJOR.MINOR.PATCH format
2. **Comprehensive Testing:** Include unit tests and integration tests
3. **Clear Documentation:** Keep README and examples up to date
4. **Security First:** Regular token rotation and permission audits
5. **Community Focus:** Welcome contributions and feedback
6. **Performance:** Monitor and optimize for large repositories
7. **Compatibility:** Support multiple .NET versions when possible

This comprehensive setup transforms your C# migration tool into a professional, distributable package that can be easily installed and used by the DevOps community worldwide. 🌍
