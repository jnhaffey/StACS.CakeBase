public static class Constants
{
	public const char SingleLine = '-';
	public const char DoubleLine = '=';
	public const char Star = '*';
	public const string NewLine = "\n\r";
	public const string JsonFile = "projectData.json";
	public static List<String> IgnoreList =  new List<String>
    {
        "\\bin\\",
        "\\obj\\",
        ".csproj.user"
    };

	public static class Verbosity
	{
		public const string Quiet = "Quiet";
		public const string Minimal = "Minimal";
		public const string Normal = "Normal";
		public const string Verbose = "Verbose";
		public const string Diagnostic = "Diagnostic";
	}
}