using  System.Security.Cryptography;

public class ProjectFile
{
    public string Name { get; set; }
    public string Hash { get; set; }

	public static ProjectFile CreateNew(string name)
	{
		var projectFile = new ProjectFile
		{
			Name = name
		};
		projectFile.GenerateHash();
		return projectFile;
	}

	public void GenerateHash()
	{
		using (var md5 = MD5.Create())
		{
			using (var stream = System.IO.File.OpenRead(this.Name))
			{
				this.Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
			}
		}
	}
}