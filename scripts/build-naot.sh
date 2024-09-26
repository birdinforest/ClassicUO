#!/bin/bash

set -e

# Define paths and project details
bootstrap_project="../src/ClassicUO.Bootstrap/src/ClassicUO.Bootstrap.csproj"
client_project="../src/ClassicUO.Client"
output_directory="../bin/dist"
target=""

# Determine the platform
platform=$(uname -s)

# Build for the appropriate platform
case $platform in
  Linux)
    # Add Linux-specific build commands here
    target="linux-x64"
    ;;
  Darwin)
    # Add macOS-specific build commands here
   target="osx-x64"
    ;;
  MINGW* | CYGWIN*)
    # Add Windows-specific build commands here
    target="win-x64"
    ;;
  *)
    echo "Unsupported platform: $platform"
    exit 1
    ;;
esac


dotnet publish "$bootstrap_project" -c Release -o "$output_directory"
dotnet publish "$client_project" -c Release -p:NativeLib=Shared -p:OutputType=Library -r $target -o "$output_directory"

# dotnet publish "$client_project" -c Release -f net8.0 -p:TargetFrameworks=net8.0 -p:NativeLib=Shared -p:OutputType=Library -r $target -o "$output_directory"

# Post processing for the ClassicUO executable
case $platform in
  Darwin)
  # Convert ClassicUO executable and set permissions
  cd "$output_directory"
  tr -d '\r' < ClassicUO > ClassicUO_fixed && mv ClassicUO_fixed ClassicUO
  chmod +x ClassicUO
esac