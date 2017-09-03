public class ProjectReference
{
    public string Name { get; set; }
    public Version Version { get; set; }

	public static ProjectReference CreateNew(string name, Version version)
	{
		return new ProjectReference
		{
			Name = name,
			Version = version
		};
	}
}