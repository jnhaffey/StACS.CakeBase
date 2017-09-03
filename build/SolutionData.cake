using Newtonsoft.Json;

public class SolutionData
{
    [JsonIgnore]
    public DirectoryPath SolutionDirectory { get; set; }
    [JsonIgnore]
    public DirectoryPath ArtifactDirectory { get; set; }
    public List<Project> Projects { get; set; }
    [JsonIgnore]
    public List<Project> Tests { get; set; }
    public DateTime LastUpdated { get; set; }

	public static SolutionData CreateNew(ICakeContext context, Parameters parameters)
	{
		return new SolutionData
		{
			SolutionDirectory = context.Environment.WorkingDirectory.FullPath,
			ArtifactDirectory = context.Environment.WorkingDirectory.FullPath + "/artifacts/",
			Projects = new List<Project>(),
			Tests = new List<Project>()
		};
	}

	public void AddProjectVersions()
	{
		foreach(var project in Projects)
		{
			project.AddProjectVersion();	
		}
	}

	public void AddProjectFiles()
	{
		foreach(var project in Projects)
		{
			project.AddProjectFiles();
		}	
	}

	public void AddProjectReferences()
	{
		foreach(var project in Projects)
		{
			project.AddProjectReferences(this.Projects);
		}	
	}
	

	public void AddProjectBuildOrder()
	{
		var skipProject = string.Empty;
		while (this.Projects.Any(p => p.BuildOrder == 0))
		{
			var project = this.Projects.FirstOrDefault(p => p.BuildOrder == 0 && p.Name != skipProject);
			skipProject = string.Empty;

			if (project.ProjectReferences.Any())
			{
				var refProjectNames = project.ProjectReferences.Select(pr => pr.Name).ToList();
				var totalPoints = 1;
				foreach (var refProject in this.Projects.Where(p => refProjectNames.Contains(p.Name)))
				{
					if (refProject.BuildOrder == 0)
					{
						skipProject = project.Name;
						break;
					}
					else
					{
						totalPoints += refProject.BuildOrder;
					}
				}
				if (skipProject == string.Empty)
				{
					project.BuildOrder = totalPoints;
				}
			}
			else
			{
				project.BuildOrder = 1;
			}
		}
	}

	public void PopulateSolutionProjects(ICakeContext context, Parameters parameters)
	{
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}

		var projectList = context.GetFiles("**/*.csproj");
		context.Debug("Total Projects Found: {0}", projectList.Count());

		if (projectList.Count() > 0)
		{
			foreach (var project in projectList)
			{
				context.Debug("Processing Project `{0}`", project);
				var buildDir = project.GetDirectory().ToString() + "/bin/" + parameters.Configuration;
				Projects.Add(Project.CreateNew(project, buildDir));
			}
		}
	}

	public void CompareProjectsForChanges(List<Project> previousProjects)
	{
		foreach(var currentProject in Projects)
		{
			var totalFiles = currentProject.ProjectFiles.Count();
			var totalChanged = 0;

			var previousProject = previousProjects.FirstOrDefault(p => p.Name.Equals(currentProject.Name, StringComparison.OrdinalIgnoreCase));
			if(previousProject != null)
			{
				foreach(var currentProjectFile in currentProject.ProjectFiles)
				{
					var previousProjectFile = previousProject.ProjectFiles.FirstOrDefault(f => f.Name.Equals(currentProjectFile.Name, StringComparison.OrdinalIgnoreCase));
					if(previousProjectFile != null)
					{
						if(!previousProjectFile.Hash.Equals(currentProjectFile.Hash, StringComparison.OrdinalIgnoreCase))
						{
							totalChanged++;
							currentProject.RequiresNewBuild = true;
							currentProject.RequiresNewVersion = true;
						}
					}
					else
					{
						totalChanged++;
						currentProject.RequiresNewBuild = true;
						currentProject.RequiresNewVersion = true;
					}
				}
			}
			else
			{
				totalChanged = totalFiles;
				currentProject.RequiresNewBuild = true;
				currentProject.RequiresNewVersion = true;
			}

			currentProject.ChangePercentage = (decimal)totalChanged / (decimal)totalFiles;
		}
	}

	public void SetAllProjectsToBuild()
	{
		Projects.ForEach(p => {
			p.RequiresNewVersion = true;
			p.RequiresNewBuild = true;
			p.ChangePercentage = 1.0M;
		});
	}

	[JsonIgnore]
	public FilePathCollection GetLatestNuGetPackageNames
	{
		get
		{
			var pathList = new List<FilePath>();
			foreach (var project in this.Projects)
			{
				pathList.Add(new FilePath(this.ArtifactDirectory.FullPath + "/" + project.GetCurrentPackageName));
			}
			return new FilePathCollection(pathList, new PathComparer(true));		
		}
	}
}