# StACS.CakeBase
This uses [Cake Build](https://github.com/cake-build/cake) as the backing to run.
Currently, I do not have a way to package this so that you can install it into your solution.
If you have a way to do this, please contact me.
This script was designed to build solutions such as _frameworks_ that are modular.

##### Current Restrictions:
* This script will only work with C# based projects currently.  
* This script will only work with the new project file format (.NET Core or .NET Standard)
* Tests currently do not support solution package reference auto-updating.
* Generate NuGet Packaging on Build must be enabled on the projects.
* nuget.config file must have reference to artifacts folder in solution  
Example:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <solution>
    <add key="disableSourceControlIntegration" value="true" />
  </solution>
  <packageSources>
    <clear />
    <add key="NuGet official package source v3" value="https://api.nuget.org/v3/index.json" />
    <add key="Artifacts Folder" value="artifacts" />
  </packageSources>
  <activePackageSource>
    <add key="All" value="(Aggregate source)" />
  </activePackageSource>
</configuration>
```


#### Description of Script
build.ps1:  
This PowerShell script will check for the required folders and files needed to run the process.
- Options (Defaults marked with *)
  - target: *Default
  - configuration: *Debug | Release
  - segementBump: *Build | Minor | Major
  - nextSegmentThreshold: *50
  - verbosity: Quiet | Minimal | *Normal | Verbose | Diagnostic

build.cake:  
This is the primary script file and does the following steps  
**SETUP**
1. Sets up parameters (Parameters.cake) which stores options set from executing build.ps1
2. Sets up solutionData (SolutionData.cake) which stores data regarding the solution in order to run process
3. Populates Project Data (Flagging as Test Project if `test` is found in the name) into solutionData.Projects.  This is done by finding all csproj files and recording basic information about them.
4. Adds Project Version data to solutionData.Projects.  This is done by reading the Version Element within the CSProj File.
5. Adds Project Files Name/Hash to solutionData.Projects.  This is done by scanning for all files, excluding an ignore list in Constants.cake.
6. Adds Project References to solutionData.Projects.  This only looks at references to projects in the same solution.
7. Adds Project Build Order to solutionData.Projects.  This is done by examining each project's references.
8. Checks for the existence of the `projectData.json` file. (Created upon successful build process)
8.1. If found, compares the previous file hashes with the new hashes to see which projects have files that have changed and attempts to determine the percentage of files changes.
8.2. If not found, assumes all files are new and forces a full rebuild.
9. Displays all data gathered and evaluated.

**CLEAN ALL BUILD FOLDERS**
1. Loops through each project and deletes the contents of the build folder (for the given configuration).
2. Examines the Artifacts folder and removes any packages that are more than two (2) versions old.

**UPDATE PROJECT VERSIONS**
1. Loops through each project and updates the project's version, if it is flagged to do so.
2. Loops through any solution projects that reference the given project and flags it to update it reference version.

**UPDATE PROJECT REFERENCES**
1. Loops through each project that is flagged to update is reference version (only solution project references).
2. Updates the reference version number to the new version number of the project.

**RESTORE BUILD PACKAGE PROJECTS**
1. Loops through each project (order by Build Order [1, 2, 3, 4...])
2. Restores all Packages (both solution and external) for the given project (requires nuget.config file be correctly configured).
3. Builds the project.
4. Packages the project (Package is created in artifacts folder)5. 

**RESTORE BUILD TESTS**
1. Loops through each project marked as test (no ordering).
2. Restores all Packages (both solution and external) for the given project (requires nuget.config file be correctly configured).
3. Builds the project.

**RUN TESTS**
1. Runs all tests found in solution.

##### To Install:
Create a folder at the solution level named 'build'.
Copy the cake files from this repository's build folder into your build folder.
Copy the build.cake and build.ps1 files to your solution directory.

##### To run this, execute build.ps1 (with defaults or custom options)
