#load "./project.cake"
#load "./version.cake"

public class Parameters
{
	public string Target { get; private set; }
	public string Configuration { get; private set; }
	public bool IsReleaseBuild { get; private set; }
	public VersionBump BumpVersion { get; private set; }
	public ICollection<Project> Projects { get; private set; }
	public ICollection<Project> Tests { get; private set; }
	public DirectoryPath SolutionDirectory { get; private set; }

	public void Initialize(ICakeContext context, ICollection<GitDiffFile> gitDiffs)
	{
		Projects = new List<Project>();
		Tests = new List<Project>();

		var allProjects = Project.GetProjects(context, this);

		foreach(var project in allProjects)
		{			
			project.AddProjectReferenceData(Projects.Select(p => p.Name).ToList());

			var relativePath = this.SolutionDirectory.GetRelativePath(project.ProjectPath).FullPath.Replace("/",@"\");
			project.CheckForChanges(gitDiffs.Where(d => d.Path.Contains(relativePath)).ToList(), this);

			if(!project.IsTestProject)
			{
				project.Version = BuildVersion.GetVersionFor(context, project);
				Projects.Add(project);
			}
			else
			{
				Tests.Add(project);
			}
		}

		this.SetBuildOrder(context);
	}

	public static Parameters GetParameters(ICakeContext context)
	{
		var bump = VersionBump.Default;
		switch(context.Argument("bump", "Build"))
		{
			case "Build":
				bump = VersionBump.Build;
				break;
			case "Revision":
				bump = VersionBump.Revision;
				break;
			case "Minor":
				bump = VersionBump.Minor;
				break;
			case "Major":
				bump = VersionBump.Major;
				break;
		}

		return new Parameters
		{
			Target = context.Argument("target", "Default"),
			Configuration = context.Argument("configuration", "Release"),
			BumpVersion = bump,
			IsReleaseBuild = false,
			SolutionDirectory = context.Environment.WorkingDirectory.FullPath
		};
	}

	private void SetBuildOrder(ICakeContext context)
	{
		context.Information("Beginning Calculation of Build Order");
		var skipProject = string.Empty;
		while(Projects.Any(p => p.BuildOrder == 0))
		{
			var project = Projects.FirstOrDefault(p => p.BuildOrder == 0 && p.Name != skipProject);
			skipProject = string.Empty;
			context.Information("{0} of {1} Projects Processed => {2}",
				Projects.Where(p => p.BuildOrder != 0).Count(),
				Projects.Count(),
				project.Name);

			if(project.HasChildReferences())
			{
				var refProjectNames = project.ProjectReferences.Select(pr => pr.Name).ToList();
				var totalPoints = 1;
				foreach(var refProject in Projects.Where(p => refProjectNames.Contains(p.Name)))
				{
					if(refProject.BuildOrder == 0)
					{
						context.Information("{0} has References that have not been ordered, skipping to next project.", project.Name);
						skipProject = project.Name;
						break;
					}
					else
					{
						totalPoints += refProject.BuildOrder;
					}			
				}
				if(skipProject == string.Empty)
				{
					project.BuildOrder = totalPoints;
				}
			}
			else
			{
				project.BuildOrder = 1;
			}
		}
		context.Information("");
	}
}