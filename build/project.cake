using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

public class Project
{
	public string Name { get; private set; }
	public BuildVersion Version { get; set; }
	public FilePath  ProjectFile { get; private set; }
	public DirectoryPath ProjectPath { get; private set; }
	public DirectoryPath BuildFolder { get; private set; }
	public bool IsPendingNewVersion { get; private set; }
	public bool IsPendingReferenceUpdate { get; private set; }
	public bool IsPendingNewBuild { get; private set; }
	public bool IsPendingNewPackage {get; private set; }
	public bool IsTestProject { get; private set; }
	public ICollection<ProjectReference> ProjectReferences { get; set; }
	public int BuildOrder { get; set; }

	public static ICollection<Project> GetProjects(ICakeContext context, Parameters parameters)
	{
	    if (context == null)
        {
            throw new ArgumentNullException("context");
        }

		var projectList = context.GetFiles("./**/*.csproj");

		if(projectList.Count() > 0)
		{
			var returnList = new List<Project>();
			foreach(var project in projectList)
			{
				var buildDir = project.GetDirectory().ToString() + "/bin/" + parameters.Configuration;
				returnList.Add(new Project
				{
					Name = project.GetFilenameWithoutExtension().ToString(),
					ProjectFile = project,
					ProjectPath = project.GetDirectory(),
					BuildFolder = new DirectoryPath(buildDir),
					IsTestProject = project.GetFilename().ToString().IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0
				});
			}
			return returnList;
		}
		return null;
	}

	public void CheckForChanges(ICollection<GitDiffFile> getDiffs, Parameters parameters)
	{
		if(parameters.BumpVersion == VersionBump.Default && getDiffs.Count() > 0)
		{
			this.FlagForNewVersion();
		}
	}

	public void AddProjectReferenceData(ICollection<string> solutionProjectNames)
	{
		var projectFile = XElement.Load(this.ProjectFile.FullPath);
		var references = projectFile.Descendants("PackageReference").ToList();
		this.ProjectReferences = new List<ProjectReference>();
		foreach(var reference in references)
		{
			var referenceName = reference.Attribute("Include").Value;
			if(solutionProjectNames.Contains(referenceName))
			{
				this.ProjectReferences.Add(new ProjectReference
				{
					Name = referenceName,
					Version = new Version(reference.Attribute("Version").Value)
				});
			}
		}		
	}

	public void UpdateProjectVersion(VersionBump bump)
	{
		switch(bump)
		{
			case VersionBump.Build:
				this.Version.BumpBuild();
			break;
			case VersionBump.Revision:
				this.Version.BumpRevision();
			break;
			case VersionBump.Minor:
				this.Version.BumpMinor();
			break;
			case VersionBump.Major:
				this.Version.BumpMajor();
			break;
		}
		this.Version.UpdateProjectVersion(this);
		this.IsPendingNewVersion = false;
	}

	public void BuildComplete()
	{
		this.IsPendingNewBuild = false;
	}

	public void PackagingComplete()
	{
		this.IsPendingNewPackage = false;
	}

	public bool HasChildReferences()
	{
		return this.ProjectReferences.Any();
	}

	public void UpdateProjectReference(ICakeContext context, ICollection<Project> projectList)
	{
		var projectFile = XElement.Load(this.ProjectFile.FullPath);
		var references = projectFile.Descendants("PackageReference").ToList();
		foreach(var refProject in this.ProjectReferences)
		{
			var project = projectList.FirstOrDefault(p => p.Name == refProject.Name);
			if(project != null && project.Version.GetSemVersion != refProject.Version.ToString())
			{
				var reference = references.FirstOrDefault(r => r.Attribute("Include").Value == refProject.Name);
				if(reference != null)
				{
					context.Information("\tUpdating Reference `{0}` Version | {1} => {2}",
						refProject.Name,
						reference.Attribute("Version").Value,
						project.Version.GetSemVersion);
					reference.Attribute("Version").Value = project.Version.GetSemVersion;
				}
				else
				{
					throw new Exception("Refences Not Found in CSPROJ File");
				}
			}
			else
			{
				throw new Exception("Missing Project or Bad Version Reference");
			}
		}
		projectFile.Save(this.ProjectFile.FullPath);
		this.IsPendingReferenceUpdate = false;
	}

	public void FlagForReferenceUpdates()
	{
		this.IsPendingReferenceUpdate = true;
		FlagForNewVersion();
	}

	public void FlagForNewVersion()
	{
		this.IsPendingNewVersion = true;
		this.FlagForNewBuild();
	}

	public void FlagForNewBuild()
	{
		this.IsPendingNewBuild = true;
		this.FlagForNewPackage();
	}

	public void MarkNewBuildComplete()
	{
		this.IsPendingNewBuild = false;
	}

	public void FlagForNewPackage()
	{
		this.IsPendingNewPackage = true;
	}

	public void MarkNewPackageComplete()
	{
		this.IsPendingNewPackage = false;
	}
}

public class ProjectReference
{
	public string Name { get; set; }
	public Version Version { get; set; }
}

public enum VersionBump
{
	Default,
	Build,
	Revision,
	Minor,
	Major
}