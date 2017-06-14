# StACS.CakeBase
This is a Cake Script that was designed to work with .NET Core or .NET Standard Projects in a solution that each project is its own package.

How this script works:

**Build.ps1**
- Options of concern, * indicates default value:
  - Configuration => Valid Values: Release* | Debug
  - Bump => Valid Values: Change* | Build | Revision | Minor | Major

**Setup**
- Get Parmeters pulls in flags from calling build.ps1
- Gets a list of all Git Changes from last Commit
- Parameter Initialize:
  - Scans for all csproj files in solution (Sets a flag if the project names contains the word `test`)
  - Evaluates projects for the following:
    - It Exams if it the project has Package References to projects in this solution (via NuGet Only)
    - It Compares project path against paths in Git Change List
    - If Test Project Flag is set, adds project details to Test List
    - If Test Project Flag is not set, Gets Project Version Details from csproj
    - Last, it loops through all projects in list and scores them based on references to other projects in the solution.  This creates the 'build order'

**Clean All Build Folders**
- It cleans all the Build Folders for each of the projects
- It also cleans the artifacts folder used to restore local Package References to nuget packages

**Update Project Versions**
- It loops through all projects (not test projects) that have the flag `IsPendingNewVersion`
- It Updates the build number for each project flagged (currently ignores ps1 bump flag if changed)
- It flags projects with the `HasChildReferences` flag and flags them with `IsPendingReferenceUpdate` if the project has a Package Reference to the project that just had a version updated

**Update Project Reference Versions**
- This loops through all projects (not test projects) that have the flag `IsPendingReferenceUpdate`
- It sets the Package Reference Version to match the new version of the project in the solution

**Restore Build Package Projects**
- It loops through all the projects (not test projects), ordered by the Build Order set during Setup
  - 1) It Restores All Package References for the project, this includes local Package References if needed
  - 2) It Builds the project
  - 3) It Creates a new Package of the project andsaves it to the artifacts folder 

**Update Test Reference Versions**
- This loops through all test projects
  - It sets the Package Reference Versions to match the new version of the project in the solution, if they have changed

**Restore Build Package Tests**
- This loops through all test projects
  - 1) It Restores All Package References for the project, this includes local Package References if needed
  - 2) It Builds the project

**Run Tests**
- This loops through all test projects
  - It Executes all Tests in each of the test project