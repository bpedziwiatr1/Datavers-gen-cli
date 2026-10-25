using System.ComponentModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace DataverseGen.Core.Config;

[Serializable]
[DataContract]
public class ConfigModel
{
	[JsonProperty]
	public string ConnectionString { get; set; }
	[JsonProperty]

	public string[] Entities { get; set; }
	[JsonProperty]

	public string[] CustomApis { get; set; }
	[JsonProperty]

	public string Namespace { get; set; }
	[JsonProperty]

	public string OutDirectory { get; set; }
	[JsonProperty]

	/// <summary>Output folder for entities; falls back to <see cref="OutDirectory"/>.</summary>
	public string EntitiesOutDirectory { get; set; }
	[JsonProperty]

	/// <summary>Output folder for custom APIs; falls back to a <c>customapi</c> folder in the entities folder.</summary>
	public string CustomApiOutDirectory { get; set; }
	[JsonProperty]

	public TemplateEngineModel TemplateEngine { get; set; }
	[JsonProperty]

	public string TemplateName { get; set; }
	[JsonProperty]
	[DefaultValue("Templates")]
	public string TemplateDirectoryName { get; set; }
	[JsonProperty]
	[DefaultValue(true)]
	public bool ThrowOnEntityNotFound { get; set; }
	[JsonProperty]
	[DefaultValue(true)]
	public bool EnableConnectionStringValidation { get; set; }
	[JsonProperty]

	/// <summary>Multi-file output only: write entity files as <c>&lt;name&gt;.cs</c> / <c>&lt;name&gt;.attributes.ts</c> instead of <c>&lt;name&gt;.entity.cs|ts</c>.</summary>
	public bool LegacyEntityFileNames { get; set; }
	[JsonProperty]

	/// <summary>Remove previously generated files (marked <c>&lt;auto-generated&gt;</c>) from the output folders that were not written in this run. Default: <c>true</c>.</summary>
	public bool ClearOutputDirectory { get; set; } = true;
}

[Serializable]
[DataContract]
public class TemplateEngineModel
{
	[JsonProperty]

	public bool IsSingleOutput { get; set; }

	/// <summary>
	///  scriban/t4
	/// </summary>
	[JsonProperty]
	public string Name { get; set; }

	/// <summary>
	///  TS,C#
	/// </summary>
	[JsonProperty]
	public string Type { get; set; }
}
