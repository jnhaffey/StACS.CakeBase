using Newtonsoft.Json;

public class Project
{
    public string Name { get; set; }
    [JsonIgnore]
    public int BuildOrder { get; set; }
    [JsonIgnore]
    public BuildVersion Version { get; set; }
    [JsonIgnore]
    public List<ProjectReference> ProjectReferences { get; set; }
    public List<ProjectFile> ProjectFiles { get; set; }
    [JsonIgnore]
    public decimal ChangePercentage { get; set; }
    [JsonIgnore]
    public FilePath CSProjFile { get; set; }
    [JsonIgnore]
    public DirectoryPath ProjectPath { get; set; }
    [JsonIgnore]
    public DirectoryPath BuildFolder { get; set; }
    [JsonIgnore]
    public bool IsTestProject { get; set; }
    [JsonIgnore]
    public bool RequiresNewBuild { get; set; }
    [JsonIgnore]
    public bool RequiresNewVersion { get; set; }
    [JsonIgnore]
    public bool RequiresReferencePackageUpdates { get; set; }

	public static Project CreateNew(FilePath project, string buildDirectory)
	{
		return new Project
        {
            Name = project.GetFilenameWithoutExtension().ToString(),
            BuildOrder = 0,
            ProjectReferences = new List<ProjectReference>(),
            ProjectFiles = new List<ProjectFile>(),
            CSProjFile = project,
            ProjectPath = project.GetDirectory(),
            BuildFolder = new DirectoryPath(buildDirectory),
            IsTestProject = project.GetFilename().ToString().IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0,
            RequiresNewBuild = false
        };
	}

	public void AddProjectVersion()
	{
		this.Version = BuildVersion.CreateNew(this);
	}

	public void AddProjectFiles()
	{
		var files = System.IO.Directory.GetFiles(this.ProjectPath.FullPath, "*.*", SearchOption.AllDirectories)
		.Where(f => !Constants.IgnoreList.Any(il => f.Contains(il)))
		.ToList();

		foreach(var file in files)
		{
			this.ProjectFiles.Add(ProjectFile.CreateNew(file.Replace('/','\\')));
		}
	}

	public void UpdateProjectFileHash()
	{
		foreach(var file in this.ProjectFiles)
		{
			file.GenerateHash();
		}
	}

	public void AddProjectReferences(List<Project> allSolutionProjects)
	{
		var projectFile = XElement.Load(this.CSProjFile.FullPath);
		var references = projectFile.Descendants("PackageReference").ToList();
		var listOfReferences = new List<ProjectReference>();
		foreach(var reference in references)
		{
			var referenceName = reference.Attribute("Include").Value;
			if(allSolutionProjects.FirstOrDefault(p => p.Name.Contains(referenceName)) != null)
			{
				listOfReferences.Add(ProjectReference.CreateNew(referenceName, new Version(reference.Attribute("Version").Value)));
			}
		}
	}

	public void SaveProjectVersion()
	{
		var projectFile = XElement.Load(this.CSProjFile.FullPath);
		var currentVersion = projectFile.Descendants("Version").FirstOrDefault();
		if(currentVersion != null)
		{
			currentVersion.Value = this.Version.SemanticVersion;
		}
		projectFile.Save(this.CSProjFile.FullPath);
		this.RequiresNewVersion = false;	
	}

	public void BumpBuildVersion(Parameters parameters)
	{
		var segmentToBump = parameters.SegmentBump;
		if(parameters.SegmentThreshold <= (this.ChangePercentage*100) && segmentToBump != SegmentBump.Major)
		{
			segmentToBump++;
		}

		switch(segmentToBump)
		{
			case SegmentBump.Build:
				this.Version.Build++;
				break;
			case SegmentBump.Minor:
				this.Version.Build = 0;
				this.Version.Minor++;
				break;
			case SegmentBump.Major:
				this.Version.Build = 0;
				this.Version.Minor = 0;
				this.Version.Major++;
				break;
		}
	}

	public string GetCurrentPackageName
	{
		get
		{
			return String.Format("{0}.{1}.{2}.{3}.nupkg", this.Name, this.Version.Major, this.Version.Minor, this.Version.Build);
		}
	}

	public bool HasChildReferences
	{
		get
		{
			return this.ProjectReferences != null && this.ProjectReferences.Any();	
		}		
	}
}
