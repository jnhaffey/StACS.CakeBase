using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

public class BuildVersion
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Build { get; set; }
    public bool IsBeta { get; set; }

	public static BuildVersion CreateNew(Project project)
	{
		var projectFile = XElement.Load(project.CSProjFile.FullPath);
		var currentVersion = projectFile.Descendants("Version").FirstOrDefault();
		if(currentVersion == null || String.IsNullOrWhiteSpace(currentVersion.Value))
		{
			return new BuildVersion
			{
				Major = 0,
				Minor = 0,
				Build = 0,
				IsBeta = false
			};
		}
		else
		{
			var cleanCurrentVersion = currentVersion.Value.ToUpper().Replace("-BETA", "");
			var semVersion = new Version(cleanCurrentVersion);
			return new BuildVersion
			{
				Major = semVersion.Major >= 0 ? semVersion.Major : 0,
				Minor = semVersion.Minor >= 0 ? semVersion.Minor : 0,				
				Build = semVersion.Build >= 0 ? semVersion.Build : 0,
				IsBeta = currentVersion.Value.ToUpper().Contains("-BETA"),
			};

		}
	}

	public string SemanticVersion
	{
		get
		{
			var semVersion = this.Major + "." + this.Minor + "." + this.Build;
			if(this.IsBeta)
			{
				semVersion += "-BETA";
			}
			return semVersion;		
		}
	}
}