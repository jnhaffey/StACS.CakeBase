using Newtonsoft.Json;

public class Parameters
{
    public string Target { get; set; }
    public string Configuration { get; set; }
    public SegmentBump SegmentBump { get; set; }
    public int SegmentThreshold { get; set; }
    public string Verbosity { get; set; }
    public bool JsonDataExists { get; set; }

	public static Parameters CreateNew(ICakeContext context)
	{
		return new Parameters
		{
			Target = context.Argument("target", "Default"),
			Configuration = context.Argument("configuration", "Debug"),
			SegmentBump = (SegmentBump)Enum.Parse(typeof(SegmentBump), context.Argument("segmentBump","Build")),
			SegmentThreshold = context.Argument("segmentThreshold", 50),
			Verbosity = context.Argument("verbosity", "Normal")
		};
	}

	public static SolutionData ReadJsonFile(string file)
	{
		return JsonConvert.DeserializeObject<SolutionData>(System.IO.File.ReadAllText(file));
	}

	public static void WriteJsonFile(string file, SolutionData data)
	{
		data.LastUpdated = DateTime.Now;
		var jsonFormat = JsonConvert.SerializeObject(data);
		System.IO.File.WriteAllText(file, jsonFormat);
	}

	public static string Line(int repeat, char lineChar)
	{
		return new String(lineChar, repeat);
	}

	public static string Indent(int repeat)
	{
		return new String('\t', repeat);
	}

	public static string NewLine(int repeat)
	{
		var sb = new StringBuilder();
		for(var i = 0; i < repeat; i++)
		{
			sb.Append(Constants.NewLine);	
		}
		return sb.ToString();
	}
}