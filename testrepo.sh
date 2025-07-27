#!/bin/bash

# Repository Migration Script from ADO to GitHub
# Usage: ./migrate_repo.sh <source-url> <destination-url> <commit-message> [working-directory]

set -e  # Exit on any error

# Function to print colored output
print_status() {
    echo -e "\033[1;34m[INFO]\033[0m $1"
}

print_success() {
    echo -e "\033[1;32m[SUCCESS]\033[0m $1"
}

print_error() {
    echo -e "\033[1;31m[ERROR]\033[0m $1"
}

print_warning() {
    echo -e "\033[1;33m[WARNING]\033[0m $1"
}

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to extract repository name from URL
extract_repo_name() {
    local url="$1"
    # Extract the last part of the URL and remove .git if present
    basename "$url" .git
}

# Function to run git command with error handling
run_git_command() {
    local cmd="$1"
    local description="$2"
    
    print_status "$description"
    echo "Executing: git $cmd"
    
    if git $cmd; then
        print_success "$description completed"
    else
        local exit_code=$?
        print_error "$description failed with exit code $exit_code"
        return $exit_code
    fi
}

# Function to pull all branches
pull_all_branches() {
    print_status "Fetching all branches..."
    
    # Fetch all branches
    git fetch --all
    
    # Get list of remote branches and create local tracking branches
    git branch -r | grep -v '\->' | while read remote; do
        local branch=$(echo $remote | sed 's/origin\///')
        if [[ "$branch" != "HEAD" && "$branch" != "main" && "$branch" != "master" ]]; then
            print_status "Creating local branch: $branch"
            git checkout -b "$branch" "$remote" 2>/dev/null || {
                print_warning "Branch $branch already exists or cannot be created"
            }
        fi
    done
}

# Function to rename default branch
rename_default_branch() {
    print_status "Renaming default branch..."
    
    # Try to checkout and rename main branch
    if git checkout main 2>/dev/null; then
        git branch -m main old-main
        print_success "Renamed main branch to old-main"
    elif git checkout master 2>/dev/null; then
        git branch -m master old-main
        print_success "Renamed master branch to old-main"
    else
        print_warning "No main or master branch found to rename"
    fi
}

# Function to merge remote main with conflict resolution
merge_remote_main() {
    local commit_message="$1"
    
    print_status "Merging remote main branch..."
    
    # Create a new main branch
    git checkout -b main 2>/dev/null || git checkout main
    
    # Pull from remote main with unrelated histories
    if git pull --no-rebase origin main --allow-unrelated-histories; then
        print_success "Successfully merged remote main"
    else
        print_status "Handling merge conflicts..."
        
        # Check if there are conflicts or uncommitted changes
        if ! git diff-index --quiet HEAD --; then
            print_status "Adding all changes and creating merge commit..."
            git add .
            
            # Create merge commit with provided message
            if git commit -m "$commit_message"; then
                print_success "Merge commit created with message: $commit_message"
            else
                print_error "Failed to create merge commit"
                return 1
            fi
        fi
    fi
}

# Main migration function
migrate_repository() {
    local source_url="$1"
    local destination_url="$2"
    local commit_message="$3"
    local working_dir="${4:-$(pwd)}"
    
    print_status "Starting repository migration..."
    print_status "Source: $source_url"
    print_status "Destination: $destination_url"
    print_status "Working Directory: $working_dir"
    
    # Extract repository name
    local repo_name=$(extract_repo_name "$source_url")
    local repo_path="$working_dir/$repo_name"
    
    print_status "Repository name: $repo_name"
    
    # Navigate to working directory
    cd "$working_dir"
    
    # Remove existing directory if it exists
    if [ -d "$repo_name" ]; then
        print_warning "Repository directory already exists. Removing..."
        rm -rf "$repo_name"
    fi
    
    # Step 1: Clone the source repository
    print_status "Cloning repository from $source_url..."
    if git clone "$source_url"; then
        print_success "Repository cloned successfully"
    else
        print_error "Failed to clone repository"
        return 1
    fi
    
    # Step 2: Navigate to repository directory
    cd "$repo_name"
    
    # Step 3: Pull all branches
    pull_all_branches
    
    # Step 4: Rename default branch
    rename_default_branch
    
    # Step 5: Set new remote repository
    run_git_command "remote set-url origin $destination_url" "Setting new remote URL"
    
    # Step 6: Merge remote main to local
    merge_remote_main "$commit_message"
    
    # Step 7: Push all branches to destination
    print_status "Pushing all branches to destination repository..."
    if git push --all origin; then
        print_success "All branches pushed successfully"
    else
        print_error "Failed to push branches"
        return 1
    fi
    
    # Step 8: Push all tags to destination
    print_status "Pushing all tags to destination repository..."
    if git push --tags origin; then
        print_success "All tags pushed successfully"
    else
        print_warning "Failed to push tags (this might be expected if no tags exist)"
    fi
    
    print_success "Repository migration completed successfully!"
}

# Main script execution
main() {
    # Check if git is installed
    if ! command_exists git; then
        print_error "Git is not installed or not in PATH"
        exit 1
    fi
    
    # Check arguments
    if [ $# -lt 3 ]; then
        echo "Usage: $0 <source-url> <destination-url> <commit-message> [working-directory]"
        echo ""
        echo "Example:"
        echo "  $0 https://dev.azure.com/org/project/_git/repo \\"
        echo "     https://github.com/user/repo.git \\"
        echo "     \"Migration from ADO to GitHub\" \\"
        echo "     /path/to/working/directory"
        exit 1
    fi
    
    local source_url="$1"
    local destination_url="$2"
    local commit_message="$3"
    local working_dir="${4:-$(pwd)}"
    
    # Validate URLs
    if [[ ! "$source_url" =~ ^https?:// ]]; then
        print_error "Source URL must be a valid HTTP/HTTPS URL"
        exit 1
    fi
    
    if [[ ! "$destination_url" =~ ^https?:// ]]; then
        print_error "Destination URL must be a valid HTTP/HTTPS URL"
        exit 1
    fi
    
    # Create working directory if it doesn't exist
    if [ ! -d "$working_dir" ]; then
        print_status "Creating working directory: $working_dir"
        mkdir -p "$working_dir"
    fi
    
    # Run migration
    if migrate_repository "$source_url" "$destination_url" "$commit_message" "$working_dir"; then
        print_success "Migration completed successfully!"
        exit 0
    else
        print_error "Migration failed!"
        exit 1
    fi
}

# Execute main function with all arguments
main "$@"
