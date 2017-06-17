using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

public class BuildVersion
{
	public int Major { get; private set; }
	public int Minor { get; private set; }
	public int Revision { get; private set; }
	public int Build { get; private set; }
	public bool IsPre { get; private set; }
	public bool IsAlpha { get; private set; }

	public string GetSemVersion
	{
		get
		{
			var semVersion = Major + "." + Minor + "." + Build;
			if(this.Revision >= 0)
			{
				semVersion += "." + Revision;
			}
			if(IsPre)
			{
				semVersion += "-PRE";
			}
			else if (IsAlpha)
			{
				semVersion += "-ALPHA";
			}
			return semVersion;
		}	
	}

	public static BuildVersion GetVersionFor(ICakeContext context, Project project)
	{
		var projectFile = XElement.Load(project.ProjectFile.FullPath);
		var currentVersion = projectFile.Descendants("Version").FirstOrDefault();
		if(currentVersion == null || String.IsNullOrWhiteSpace(currentVersion.Value))
		{
			return new BuildVersion
			{
				Major = 0,
				Minor = 0,
				Revision = 0,
				Build = 0,
				IsPre = false,
				IsAlpha = false
			};
		}
		else
		{
			var cleanCurrentVersion = currentVersion.Value.ToUpper().Replace("-PRE", "").Replace("-ALPHA","");
			var semVersion = new Version(cleanCurrentVersion);
			return new BuildVersion
			{
				Major = semVersion.Major >= 0 ? semVersion.Major : 0,
				Minor = semVersion.Minor >= 0 ? semVersion.Minor : 0,				
				Build = semVersion.Build >= 0 ? semVersion.Build : 0,
				Revision = semVersion.Revision >= 0 ? semVersion.Revision : 0,
				IsPre = currentVersion.Value.ToUpper().Contains("-PRE"),
				IsAlpha = currentVersion.Value.ToUpper().Contains("-ALPHA")
			};

		}
	}

	public void UpdateProjectVersion(Project project)
	{
		var projectFile = XElement.Load(project.ProjectFile.FullPath);
		var currentVersion = projectFile.Descendants("Version").FirstOrDefault();
		if(currentVersion != null)
		{
			currentVersion.Value = this.GetSemVersion;
		}
		projectFile.Save(project.ProjectFile.FullPath);
	}

	public void BumpRevision()
	{
		this.Revision += 1;
	}

	public void BumpBuild()
	{
		this.Build += 1;
		this.Revision = 0;
	}

	public void BumpMinor()
	{
		this.Minor += 1;
		this.Revision = 0;
		this.Build = 0;
	}

	public void BumpMajor()
	{
		this.Major += 1;
		this.Minor = 0;
		this.Revision = 0;
		this.Build = 0;
	}
}