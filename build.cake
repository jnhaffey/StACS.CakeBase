// Install Addins.
#addin "MagicChunks"
#addin nuget:?package=Cake.Git

// Install Tools.

// Load Other Scripts.
#load "./build/parameters.cake";

//////////////////////////////////////////////////////////////////////
// PARAMETERS
//////////////////////////////////////////////////////////////////////

Parameters parameters = Parameters.GetParameters(Context);
bool publishingError = false;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
	var solutionChanges = GitDiff(context.Environment.WorkingDirectory.FullPath);

	parameters.Initialize(context, solutionChanges);

	Information("Target: {0}", parameters.Target);
	Information("Configuration: {0}", parameters.Configuration);
	Information("Build Bump: {0}", parameters.BumpVersion);
	Information("IsReleaseBuild: {0}", parameters.IsReleaseBuild);
	Information("SolutionDirectory: {0}", parameters.SolutionDirectory);

	Information("\n\r--Project States--\n\r");
	
	foreach(var project in parameters.Projects)
	{
		Information("Project: {0}", project.Name);
		if(project.IsTestProject == false)
		{
			Information("Version: {0}", project.Version.GetSemVersion);
			Information("Is PreRelease: {0}", project.Version.IsPre);
			Information("Is AlphaRelease: {0}", project.Version.IsAlpha);
		}		
		Information("File Name: {0}", project.ProjectFile.GetFilename());
		Information("Project Path: {0}", project.ProjectPath.FullPath);
		Information("Build Folder: {0}", project.BuildFolder.FullPath);

		Information("Is Pending New Version: {0}", project.IsPendingNewVersion);
		Information("Is Pending Reference Updates: {0}", project.IsPendingReferenceUpdate);
		Information("Is Pending New Build: {0}", project.IsPendingNewBuild);
		Information("Is Pending New Package: {0}", project.IsPendingNewPackage);
		Information("Is Test Project: {0}", project.IsTestProject);
		Information("Build Order: {0}", project.BuildOrder);
		Information("Reference:");
		if(project.ProjectReferences != null && project.ProjectReferences.Any())
		{
			foreach(var reference in project.ProjectReferences)
			{
				Information("\t{0} (v{1})", reference.Name, reference.Version);
			}
		}
		else
		{
			Information("\tProject has no Package References");
		}
		Information("\n\r");
	}
});

Teardown(context =>
{
	Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean-All-Build-Folders")
    .Does(() =>
	{
		foreach(var project in parameters.Projects)
		{
			CleanDirectory(project.BuildFolder.FullPath);
		}
		CleanDirectory(parameters.SolutionDirectory.FullPath + "/artifacts");
	});

Task("Update-Project-Versions")
	.IsDependentOn("Clean-All-Build-Folders")
	.Does(() => 
	{
		var projectsWithReferences = parameters.Projects.Where(p => p.HasChildReferences());

		while(parameters.Projects.Any(p => p.IsPendingNewVersion))
		{
			var project = parameters.Projects.FirstOrDefault(p => p.IsPendingNewVersion);
			var oldVersion = project.Version.GetSemVersion;
			project.UpdateProjectVersion(VersionBump.Build);
			Information("Project: `{0}` \n\r\t Updating Version v{1} => v{2}",
				project.Name, oldVersion, project.Version.GetSemVersion);
			Information("\n\r--Reference Projects--");

			//TODO: Fix this
			foreach(var childProject in projectsWithReferences)
			{
				if(childProject.ProjectReferences.Any(pr => pr.Name == project.Name))
				{
					childProject.FlagForReferenceUpdates();
					Information("\t{0}", childProject.Name);
				}
				else
				{
					Information("\tProject has no Package References");
					break;
				}
			}
			Information("\n\r");
		}
	});

Task("Update-Project-Reference-Versions")
	.IsDependentOn("Update-Project-Versions")
	.Does(context => 
	{
		while(parameters.Projects.Any(p => p.IsPendingReferenceUpdate))
		{
			var project = parameters.Projects.FirstOrDefault(p => p.IsPendingReferenceUpdate);
			Information("Updating References in {0}", project.Name);
			project.UpdateProjectReference(context, parameters.Projects);
			Information("\n\r");
		}
	});

Task("Restore-Build-Package-Projects")
	.IsDependentOn("Update-Project-Reference-Versions")
	.Does(() =>
	{
		foreach(var project in parameters.Projects.OrderBy(p => p.BuildOrder))
		{
			// Restore NuGet Packages for Current Project
			Information("Starting NuGet Restore for {0}", project.Name);
			DotNetCoreRestore(project.ProjectFile.FullPath, 
				new DotNetCoreRestoreSettings
				{
					ConfigFile = "./nuget.config"
				});
			Information("Finished NuGet Restore for {0}", project.Name);

			// Build Current Project
			Information("Starting Build for {0}", project.Name);
			DotNetCoreBuild(project.ProjectFile.FullPath, new DotNetCoreBuildSettings()
			{
				Configuration = parameters.Configuration
			});
			project.MarkNewBuildComplete();
			Information("Finished Build for {0}", project.Name);

			// Package Current Project
			Information("Starting NuGet Packaging for {0}", project.Name);
			DotNetCorePack(project.ProjectFile.FullPath, 
				new DotNetCorePackSettings {
					Configuration = parameters.Configuration,
					OutputDirectory = parameters.SolutionDirectory + "/artifacts",
					NoBuild = true
				});
			project.MarkNewPackageComplete();
			Information("Finished NuGet Packaging for {0}", project.Name);
		}
	});

Task("Update-Test-Reference-Versions")
	.IsDependentOn("Restore-Build-Package-Projects")
	.Does(context =>
	{
		foreach(var testProject in parameters.Tests)
		{
			Information("Updating References in {0}", testProject.Name);
			testProject.UpdateProjectReference(context, parameters.Projects);
			Information("\n\r");
		}
	});

Task("Restore-Build-Package-Tests")
	.IsDependentOn("Update-Test-Reference-Versions")
	.Does(() =>
	{
		foreach(var testProject in parameters.Tests)
		{
			Information("Starting NuGet Restore for {0}", testProject.Name);
			DotNetCoreRestore(testProject.ProjectFile.FullPath, 
				new DotNetCoreRestoreSettings
				{
					ConfigFile = "./nuget.config"
				});
			Information("Finished NuGet Restore for {0}", testProject.Name);

			Information("Starting Build for {0}", testProject.Name);
			DotNetCoreBuild(testProject.ProjectFile.FullPath, new DotNetCoreBuildSettings()
			{
				Configuration = parameters.Configuration
			});
			testProject.MarkNewBuildComplete();
			Information("Finished Build for {0}", testProject.Name);
		}
	});
	
Task("Run-Tests")
	.IsDependentOn("Restore-Build-Package-Tests")
	.Does(() =>
	{
		foreach(var testProject in parameters.Tests)
		{
			Information("Starting Tests for {0}", testProject.Name);
			DotNetCoreTest(testProject.ProjectFile.FullPath,
				new DotNetCoreTestSettings()
				{
					Configuration = parameters.Configuration,
					NoBuild = true
				});
			Information("Finished Tests for {0}", testProject.Name);
		}
	});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
	.IsDependentOn("Run-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(parameters.Target);