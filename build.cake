// Install Add-ins.
#addin nuget:?package=Cake.Git
#addin nuget:?package=Newtonsoft.Json

// Install Tools.

// Load Other Scripts.
#load "build/Constants.cake";
#load "build/SegmentBump.cake";
#load "build/Parameters.cake";
#load "build/SolutionData.cake";
#load "build/Project.cake";
#load "build/BuildVersion.cake";
#load "build/ProjectFile.cake";
#load "build/ProjectReference.cake";

//////////////////////////////////////////////////////////////////////
// PARAMETERS
//////////////////////////////////////////////////////////////////////

var parameters = Parameters.CreateNew(Context);
var solutionData = SolutionData.CreateNew(Context, parameters);
var setupComplete = false;
var publishingError = false;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context => {
	Information("Building Solution Information...");
	solutionData.PopulateSolutionProjects(context, parameters);
	Information("Adding Project Version Data...");
	solutionData.AddProjectVersions();
	Information("Adding Project File Data...");
	solutionData.AddProjectFiles();
	Information("Adding Solution Project Reference Data...");
	solutionData.AddProjectReferences();
	Information("Calculating Build Order...");
	solutionData.AddProjectBuildOrder();

	// Load Previous Build Data (if any)
	if(FileExists(Constants.JsonFile))
	{
		Information("`projectData.json` Found{0}Loading previous build data...", Parameters.NewLine(1));
		var previousBuildDetails = Parameters.ReadJsonFile(Constants.JsonFile);
		solutionData.CompareProjectsForChanges(previousBuildDetails.Projects);
	}
	else
	{
		Warning("`projectData.json` not Found{0}All Projects will be require new build.", Parameters.NewLine(1));
		solutionData.SetAllProjectsToBuild();
	}

	// DISPLAY SETUP DETAILS
	// Parameter Information
	Information("Target: {0}", parameters.Target);
	Information("Configuration: {0}", parameters.Configuration);
	Information("Segment to Bump: {0}", parameters.SegmentBump);
	Information("Next Segment Threshold: {0}%", parameters.SegmentThreshold);
	Information("Verbosity: {0}", parameters.Verbosity);
	
	// Solution Information
	Information("Solution Directory: {0}", solutionData.SolutionDirectory);
	Information("Artifact clsDirectory: {0}", solutionData.ArtifactDirectory);

	// Project(s) Information
	Information("{0}{1}", Parameters.NewLine(1), Parameters.Line(50, Constants.Star));
	Information("Solution Projects:");
	Information("{0}", Parameters.Line(50, Constants.Star));
	foreach(var project in solutionData.Projects)
	{
		Information("{0}Project: {1}", Parameters.Indent(1), project.Name);
		Information("{0}File Name: {1}", Parameters.Indent(1), project.CSProjFile.GetFilename());
		Information("{0}Project Path: {1}", Parameters.Indent(1), project.ProjectPath.FullPath);
		Information("{0}Build Folder: {1}", Parameters.Indent(1), project.BuildFolder.FullPath);
		Information("{0}Is Test Project: {1}", Parameters.Indent(1), project.IsTestProject);
		if(!project.IsTestProject)
		{
			if(parameters.Verbosity == Constants.Verbosity.Verbose)
			{
				Verbose("{0}Project Files:", Parameters.Indent(1));
				foreach(var projectFile in project.ProjectFiles)
				{
					Verbose("{0}File: {1} ({2})", Parameters.Indent(2), projectFile.Name, projectFile.Hash);
				}
			}

			Information("{0}Current Version: {1}", Parameters.Indent(1), project.Version.SemanticVersion);
			Information("{0}Requires New Version: {1}", Parameters.Indent(1), project.RequiresNewVersion);
			Information("{0}Percentage of Change: {1}%", Parameters.Indent(1), project.ChangePercentage * 100);
			Information("{0}Requires New Build: {1}", Parameters.Indent(1), project.RequiresNewBuild);
			Information("{0}Build Order: {1}", Parameters.Indent(1), project.BuildOrder);
			Information("{0}Requires Reference Package Updates: {1}", Parameters.Indent(1), project.RequiresReferencePackageUpdates);
			Information("{0}Solution Project References:", Parameters.Indent(1));
			if(project.ProjectReferences != null && project.ProjectReferences.Any())
			{
				foreach(var reference in project.ProjectReferences)
				{
					Information("{0}{1} (v{2})", Parameters.Indent(2), reference.Name, reference.Version);
				}
			}
			else
			{
				Information("{0}Project has no Package References", Parameters.Indent(2));
			}

		}
		Information("{0}", Parameters.Line(50, Constants.SingleLine));
	}

	setupComplete = true;
});

Teardown(context => {
	Information("Finished running tasks");
	if(setupComplete)
	{
		Information("Saving {0} File", Constants.JsonFile);
		Parameters.WriteJsonFile(Constants.JsonFile, solutionData);	
	}
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Clean-All-Build-Folders")
    .Does(() =>
	{
		foreach(var project in solutionData.Projects)
		{
			CleanDirectory(project.BuildFolder.FullPath);
		}
		
		var allFiles = GetFiles(solutionData.ArtifactDirectory.FullPath + "/*.nupkg");
		Information("Package Files Found: {0}", allFiles.Count());
		var filesToKeep = solutionData.GetLatestNuGetPackageNames;
		Information("Package Files To Keep: {0}", filesToKeep.Count());
		
		foreach(var file in allFiles)
		{
			if(filesToKeep.FirstOrDefault(ftk => ftk.FullPath == file.FullPath) == null)
			{
				DeleteFile(file);
			}			
		}
	});

Task("Update-Project-Versions")
	.IsDependentOn("Clean-All-Build-Folders")
	.Does(() => 
	{
		var projectsWithReferences = solutionData.Projects.Where(p => p.HasChildReferences);

		while(solutionData.Projects.Any(p => p.RequiresNewVersion))
		{
			var project = solutionData.Projects.FirstOrDefault(p => p.RequiresNewVersion);
			var oldVersion = project.Version.SemanticVersion;
			project.BumpBuildVersion(parameters);
			project.SaveProjectVersion();
			Information("Project: `{0}`{1}{2}Updating Version v{3} => v{4}",
				project.Name,
				Parameters.NewLine(1),
				Parameters.Indent(1),
				oldVersion,
				project.Version.SemanticVersion);
			
			Information("{0}--Reference Projects--", Parameters.NewLine(1));

			//TODO: Fix this
			foreach(var childProject in projectsWithReferences)
			{
				if(childProject.ProjectReferences.Any(pr => pr.Name == project.Name))
				{
					childProject.RequiresReferencePackageUpdates = true;
					Information("{0}{1}", Parameters.Indent(1), childProject.Name);
				}
				else
				{
					Information("{0}Project has no Package References", Parameters.Indent(1));
					break;
				}
			}
			Information("{0}", Parameters.Line(50, Constants.SingleLine));
		}
	});

Task("Update-Project-Reference-Versions")
	.IsDependentOn("Update-Project-Versions")
	.Does(context => 
	{
		while(solutionData.Projects.Any(p => p.RequiresReferencePackageUpdates))
		{
			var project = solutionData.Projects.FirstOrDefault(p => p.RequiresReferencePackageUpdates);
			Information("Updating References in {0}", project.Name);
			//project.UpdateProjectReference(context, parameters.Projects);
		}
	});

Task("Restore-Build-Package-Projects")
	.IsDependentOn("Update-Project-Reference-Versions")
	.Does(() =>
	{
		foreach(var project in solutionData.Projects.OrderBy(p => p.BuildOrder))
		{
			// Restore NuGet Packages for Current Project
			Information("Starting NuGet Restore for {0}", project.Name);
			DotNetCoreRestore(project.CSProjFile.FullPath, 
				new DotNetCoreRestoreSettings
				{
					ConfigFile = "./nuget.config"
				});
			Information("Finished NuGet Restore for {0}", project.Name);

			// Build Current Project
			Information("Starting Build for {0}", project.Name);
			DotNetCoreBuild(project.CSProjFile.FullPath, new DotNetCoreBuildSettings()
			{
				Configuration = parameters.Configuration
			});
			project.RequiresNewBuild = false;
			Information("Finished Build for {0}", project.Name);

			// Package Current Project
			Information("Starting NuGet Packaging for {0}", project.Name);
			DotNetCorePack(project.CSProjFile.FullPath, 
				new DotNetCorePackSettings {
					Configuration = parameters.Configuration,
					OutputDirectory = solutionData.ArtifactDirectory.FullPath,
					NoBuild = true
				});
			Information("Finished NuGet Packaging for {0}", project.Name);
			
			// Update Project File Hashes
			Information("Starting Hash Update for {0}", project.Name);
			project.UpdateProjectFileHash();
			Information("Finished Hash Update for {0}", project.Name);
			Information("{0}", Parameters.Line(50, Constants.SingleLine));
		}
	});

Task("Restore-Build-Tests")
	.IsDependentOn("Restore-Build-Package-Projects")
	.Does(() =>
	{
		foreach(var testProject in solutionData.Tests)
		{
			Information("Starting NuGet Restore for {0}", testProject.Name);
			DotNetCoreRestore(testProject.CSProjFile.FullPath, 
				new DotNetCoreRestoreSettings
				{
					ConfigFile = "./nuget.config"
				});
			Information("Finished NuGet Restore for {0}", testProject.Name);

			Information("Starting Build for {0}", testProject.Name);
			DotNetCoreBuild(testProject.CSProjFile.FullPath, new DotNetCoreBuildSettings()
			{
				Configuration = parameters.Configuration
			});
			Information("Finished Build for {0}", testProject.Name);
		}
	});
	
Task("Run-Tests")
	.IsDependentOn("Restore-Build-Tests")
	.Does(() =>
	{
		foreach(var testProject in solutionData.Tests)
		{
			Information("Starting Tests for {0}", testProject.Name);
			DotNetCoreTest(testProject.CSProjFile.FullPath,
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