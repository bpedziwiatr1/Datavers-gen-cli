using DataverseGen.Core.CustomApi;
using DataverseGen.Core.Generators;

namespace DataverseGen.Core.Tests.Generators;

[TestClass]
public class CustomApiCSharpGeneratorTests
{
	[TestMethod]
	public void WriteCustomApis_CreatesCSharpRequestClass()
	{
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			CustomApiCSharpGenerator.WriteCustomApis(tempRoot,
				"MyApp.CustomApis",
				new[]
				{
					new CustomApiModel
					{
						RequestClassName = "ProcessStatusRequest",
						OperationName = "newupdatestatus",
						RequestParameters = new[]
						{
							new CustomApiParameterModel
							{
								DisplayName = "Customer Id",
								UniqueName = "customerid",
								PropertyName = "customerid",
								RequestPropertyName = "customerid",
								ConstructorParameterName = "customerid",
								CSharpTypeName = "string"
							}
						}
					}
				});

			string filePath = Path.Combine(tempRoot, "processstatusrequest.customapi.cs");
			string content = File.ReadAllText(filePath);

			StringAssert.Contains(content, "namespace MyApp.CustomApis;");
			StringAssert.Contains(content, "public sealed class ProcessStatusRequest : OrganizationRequest");
			StringAssert.Contains(content, "public const string RequestNameValue = \"newupdatestatus\";");
			StringAssert.Contains(content, "public string CustomerId");
			StringAssert.Contains(content, "RequestName = RequestNameValue;");
		}
		finally
		{
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
		}
	}

	[TestMethod]
	public void WriteCustomApis_IncludesBoundParameterAndMultipleProperties()
	{
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			CustomApiCSharpGenerator.WriteCustomApis(tempRoot,
				"MyApp.CustomApis",
				new[]
				{
					new CustomApiModel
					{
						RequestClassName = "ProcessStatusRequest",
						OperationName = "newupdatestatus",
						BoundEntityLogicalName = "account",
						RequestParameters = new[]
						{
							new CustomApiParameterModel
							{
								DisplayName = "Flag",
								UniqueName = "flag",
								PropertyName = "flag",
								RequestPropertyName = "flag",
								ConstructorParameterName = "flag",
								CSharpTypeName = "bool"
							},
							new CustomApiParameterModel
							{
								DisplayName = "Count",
								UniqueName = "count",
								PropertyName = "count",
								RequestPropertyName = "count",
								ConstructorParameterName = "count",
								CSharpTypeName = "int"
							}
						}
					}
				});

			string filePath = Path.Combine(tempRoot, "processstatusrequest.customapi.cs");
			string content = File.ReadAllText(filePath);

			StringAssert.Contains(content, "public EntityReference Entity");
			StringAssert.Contains(content, "public ProcessStatusRequest(bool flag, int count, EntityReference entity)");
			StringAssert.Contains(content, "public bool Flag");
			StringAssert.Contains(content, "public int Count");
			StringAssert.Contains(content, "Parameters[ParameterNames.Entity] = value;");
		}
		finally
		{
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
		}
	}

	[TestMethod]
	public void WriteCustomApis_CreatesParameterlessRequestClass()
	{
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			CustomApiCSharpGenerator.WriteCustomApis(tempRoot,
				"MyApp.CustomApis",
				new[]
				{
					new CustomApiModel
					{
						RequestClassName = "PingRequest",
						OperationName = "newping"
					}
				});

			string filePath = Path.Combine(tempRoot, "pingrequest.customapi.cs");
			string content = File.ReadAllText(filePath);

			StringAssert.Contains(content, "public PingRequest()");
			StringAssert.Contains(content, "RequestName = RequestNameValue;");
			Assert.IsFalse(content.Contains("public EntityReference Entity"));
		}
		finally
		{
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
		}
	}

	[TestMethod]
	public void WriteCustomApis_EscapesCSharpKeywordParameters()
	{
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			CustomApiCSharpGenerator.WriteCustomApis(tempRoot,
				"MyApp.CustomApis",
				new[]
				{
					new CustomApiModel
					{
						RequestClassName = "KeywordRequest",
						OperationName = "newkeyword",
						RequestParameters = new[]
						{
							new CustomApiParameterModel
							{
								DisplayName = "Private",
								UniqueName = "private",
								PropertyName = "private",
								RequestPropertyName = "private",
								ConstructorParameterName = "private",
								CSharpTypeName = "string"
							}
						}
					}
				});

			string filePath = Path.Combine(tempRoot, "keywordrequest.customapi.cs");
			string content = File.ReadAllText(filePath);

			StringAssert.Contains(content, "public string Private");
			StringAssert.Contains(content, "public KeywordRequest(string @private)");
		}
		finally
		{
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
		}
	}

	private static CustomApiParameterModel Parameter(string name, string type, bool isOptional = false, string description = "")
	{
		return new CustomApiParameterModel
		{
			DisplayName = name,
			UniqueName = $"new_{name.ToLowerInvariant()}",
			PropertyName = name,
			RequestPropertyName = $"new_{name.ToLowerInvariant()}",
			ConstructorParameterName = name,
			CSharpTypeName = type,
			IsOptional = isOptional,
			Description = description
		};
	}

	private static string Render(CustomApiModel customApi)
	{
		return CustomApiCSharpGenerator.RenderRequestClass("MyApp.CustomApis", customApi);
	}

	[TestMethod]
	public void RenderRequestClass_DeclaresParameterNamesConstants()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			RequestParameters = [Parameter("Comment", "string"), Parameter("Count", "int")]
		});

		StringAssert.Contains(content, "public static class ParameterNames");
		StringAssert.Contains(content, "public const string Comment = \"new_comment\";");
		StringAssert.Contains(content, "public const string Count = \"new_count\";");
	}

	[TestMethod]
	public void RenderRequestClass_UsesConstantsInAccessors()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			RequestParameters = [Parameter("Comment", "string")]
		});

		StringAssert.Contains(content, "Parameters.TryGetValue(ParameterNames.Comment, out object value)");
		StringAssert.Contains(content, "Parameters[ParameterNames.Comment] = value;");
		Assert.IsFalse(content.Contains("Parameters[\"new_comment\"]"));
		Assert.IsFalse(content.Contains("ContainsKey"));
	}

	[TestMethod]
	public void RenderRequestClass_BoundEntityUsesConstant()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			BoundEntityLogicalName = "account"
		});

		StringAssert.Contains(content, "public const string Entity = \"entity\";");
		StringAssert.Contains(content, "Parameters[ParameterNames.Entity] = value;");
	}

	[TestMethod]
	public void RenderRequestClass_OptionalParametersFollowRequiredWithDefaults()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			BoundEntityLogicalName = "account",
			RequestParameters =
			[
				Parameter("Note", "string", isOptional: true),
				Parameter("Id", "Guid"),
				Parameter("Count", "int", isOptional: true)
			]
		});

		StringAssert.Contains(content, "public FooRequest(Guid id, EntityReference entity, string note = null, int? count = null)");
		StringAssert.Contains(content, "if (note != null)");
		StringAssert.Contains(content, "if (count != null)");
		StringAssert.Contains(content, "Id = id;");
	}

	[TestMethod]
	public void RenderRequestClass_OptionalValueTypePropertyIsNullable()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			RequestParameters =
			[
				Parameter("Count", "int", isOptional: true),
				Parameter("Flag", "bool"),
				Parameter("Note", "string", isOptional: true)
			]
		});

		StringAssert.Contains(content, "public int? Count");
		StringAssert.Contains(content, "(int?)value");
		StringAssert.Contains(content, "public bool Flag");
		StringAssert.Contains(content, "public string Note");
	}

	[TestMethod]
	public void RenderRequestClass_DocumentsRequiredAndOptional()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			RequestParameters =
			[
				Parameter("Id", "Guid", description: "The <id> & more\r\nsecond line"),
				Parameter("Note", "string", isOptional: true)
			]
		});

		StringAssert.Contains(content, "/// <summary>Required. The &lt;id&gt; &amp; more second line</summary>");
		StringAssert.Contains(content, "/// <summary>Optional.</summary>");
	}

	[TestMethod]
	public void RenderRequestClass_RequiredBoundEntityIsDocumented()
	{
		string content = Render(new CustomApiModel
		{
			RequestClassName = "FooRequest",
			OperationName = "new_foo",
			BoundEntityLogicalName = "account"
		});

		StringAssert.Contains(content, "/// <summary>Required.</summary>");
		StringAssert.Contains(content, "public FooRequest(EntityReference entity)");
	}

	[TestMethod]
	public void WriteCustomApis_WritesResponseClassOnlyWhenResponsePropertiesExist()
	{
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			CustomApiCSharpGenerator.WriteCustomApis(tempRoot,
				"MyApp.CustomApis",
				new[]
				{
					new CustomApiModel { RequestClassName = "PingRequest", ResponseClassName = "PingResponse", OperationName = "newping" },
					new CustomApiModel
					{
						RequestClassName = "GetStatusRequest",
						ResponseClassName = "GetStatusResponse",
						OperationName = "newgetstatus",
						ResponseProperties = new[]
						{
							new CustomApiParameterModel
							{
								DisplayName = "Status Text",
								UniqueName = "statustext",
								RequestPropertyName = "statustext",
								CSharpTypeName = "string"
							}
						}
					}
				});

			Assert.IsFalse(File.Exists(Path.Combine(tempRoot, "pingresponse.customapi.cs")));
			string content = File.ReadAllText(Path.Combine(tempRoot, "getstatusresponse.customapi.cs"));
			StringAssert.Contains(content, "public sealed class GetStatusResponse : OrganizationResponse");
			StringAssert.Contains(content, "public const string StatusText = \"statustext\";");
			StringAssert.Contains(content, "public string StatusText => Results.TryGetValue(ParameterNames.StatusText");
		}
		finally
		{
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
		}
	}

	[TestMethod]
	public void WriteCustomApis_ReturnsWhenEnumerableIsNull()
	{
		string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			CustomApiCSharpGenerator.WriteCustomApis(tempRoot, "MyApp.CustomApis", (IEnumerable<CustomApiModel>)null!);

			Assert.AreEqual(0, Directory.GetFileSystemEntries(tempRoot).Length);
		}
		finally
		{
			if (Directory.Exists(tempRoot))
			{
				Directory.Delete(tempRoot, true);
			}
		}
	}
}
