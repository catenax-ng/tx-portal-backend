#!/bin/bash

# Check if the correct number of arguments are provided
if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <name> <version>"
  exit 1
fi

# Assign the arguments to variables
name="$1"
version="$2"

# Initialize a global array to store updated directories
updated_directories=()

# Initialize a global array to store projects that need to be updated
projects_to_update=()

# Initialize a global array to store projects that have been already updated
already_updated_projects=()

# Define the version update functions
update_major() {
  local version="$1"
  local updated_version="$(echo "$version" | awk -F. '{$1+=1; $2=0; $3=0; print}' | tr ' ' '.')"
  echo "$updated_version"
}

update_minor() {
  local version="$1"
  local updated_version="$(echo "$version" | awk -F. '{$2+=1; $3=0; print}' | tr ' ' '.')"
  echo "$updated_version"
}

update_patch() {
  local version="$1"
  local updated_version="$(echo "$version" | awk -F. '{$3+=1; print}' | tr ' ' '.')"
  echo "$updated_version"
}

update_pre() {
  local version="$1"
  local current_suffix=$(grep '<VersionSuffix>' "$props_file" | sed -n 's/.*<VersionSuffix>\(.*\)<\/VersionSuffix>.*/\1/p' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | tr -d '\n')
  local current_suffix_version="${current_suffix%%"."*}"
  if [ "$current_suffix_version" != "$version" ]; then
    updated_suffix="$version"
  else
    if [[ "$current_suffix" == "alpha" || "$current_suffix" == "beta" ]]; then
      updated_suffix="${current_suffix}.1"
    else
      numeric_part=$(echo "$current_suffix" | sed 's/[^0-9]//g')
      new_numeric_part=$((numeric_part + 1))
      updated_suffix="${version}.${new_numeric_part}"
    fi
  fi
  echo "$updated_suffix"
}

# Function to search and update .csproj files recursively
update_csproj_files_recursive() {
  local updated_name="$1"
  local updated_version="$2"

  # Iterate over directories in the Framework directory
  for dir in ./src/Framework/*/; do
    # Check if the directory exists
    if [ -d "$dir" ]; then
      # Check if the directory is already in updated_directories
      if [[ " ${updated_directories[*]} " != *"$dir"* ]]; then
        # Search for .csproj files in the current directory
        csproj_files=("$dir"*.csproj)
        for project_file in "${csproj_files[@]}"; do
          if grep -q "$updated_name" "$project_file"; then
            directory_name=$(basename "$dir")
            # Only update the project if it has not been updated before
            if [[ ! " ${already_updated_projects[*]} " == *"$directory_name"* ]]; then
              update_version "$dir" "$directory_name" "$updated_version"
              projects_to_update+=("$directory_name")
              already_updated_projects+=("$directory_name")
            fi
          fi
        done
      fi
    fi
  done

  # Recursively update projects that depend on the updated projects
  for project_name in "${projects_to_update[@]}"; do
    # Only update projects if they haven't been updated before
    if [[ ! " ${already_updated_projects[*]} " == *"$project_name"* ]]; then
      update_csproj_files_recursive "$project_name" "$updated_version"
    fi
  done
}

update_version(){
  local directory="$1"
  local updated_name="$2"
  
  local props_file=$directory"Directory.Build.props"
  # Check if the Directory.Builds.props file exists
  if [ -f "$props_file" ]; then
    # Extract the current version from the XML file
    current_version=$(awk -F'[<>]' '/<VersionPrefix>/{print $3}' "$props_file")
    current_suffix=$(awk -F'[<>]' '/<VersionSuffix>/{print $3}' "$props_file")

    case "$version" in
      major)
        updated_version=$(update_major "$current_version")
        updated_suffix="$current_suffix"
        ;;
      minor)
        updated_version=$(update_minor "$current_version")
        updated_suffix="$current_suffix"
        ;;
      patch)
        updated_version=$(update_patch "$current_version")
        updated_suffix="$current_suffix"
        ;;
      alpha|beta)
        updated_version="$current_version"
        updated_suffix=$(update_pre "$version")
        ;;
      *)
        echo "Invalid version argument. Valid options: major, minor, patch, alpha, beta"
        exit 1
        ;;
    esac

    # Update the VersionPrefix and VersionSuffix in the file using awk
    awk -v new_version="$updated_version" -v new_suffix="$updated_suffix" '/<VersionPrefix>/{gsub(/<VersionPrefix>[^<]+<\/VersionPrefix>/, "<VersionPrefix>" new_version "</VersionPrefix>")}/<VersionSuffix>/{gsub(/<VersionSuffix>[^<]+<\/VersionSuffix>/, "<VersionSuffix>" new_suffix "</VersionSuffix>")}1' "$props_file" > temp && mv temp "$props_file"
    echo "Updated version in $props_file to $updated_version $updated_suffix"

    updated_directories+=($directory)
    # Update the depending solutions
    update_csproj_files_recursive "$updated_name"
  else
    echo "Directory.Builds.props file not found in $directory$updated_name"
  fi
}

# Function to iterate over directories in the Framework directory
iterate_directories() {
  local updated_name="$1"
  
  # Iterate over directories in the Framework directory
  for dir in ./src/Framework/*/; do
    # Check if the directory exists
    if [ -d "$dir" ]; then
      # Check if a directory with the specified name exists
      if [[ $dir == "./src/Framework/Framework.$updated_name/" ]]; then
        update_version "$dir" "$updated_name"
        projects_to_update+=("$updated_name")
        already_updated_projects+=("$updated_name") # Mark as updated
      fi
    fi
  done

  # Update all projects that depend on the updated projects recursively
  for project_name in "${projects_to_update[@]}"; do
    # Only update projects if they haven't been updated before
    if [[ ! " ${already_updated_projects[*]} " == *"$project_name"* ]]; then
      update_csproj_files_recursive "$project_name" "$updated_version"
    fi
  done
}

# Call the iterate_directories function to start the script
iterate_directories "$name"
