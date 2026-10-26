function main([bool]$clearPublish) {
	if ($selfContainer) {
		Remove-Item .\publish -Recurse -ErrorAction Ignore 
	}	
    $currentPath = Get-Location    
   buildPackage($true)
   buildPackage($false)
   buildTool
}
function buildTool(){
	# dotnet tool package; AnyCPU because it runs on the user's dotnet host
	dotnet pack "DataverseGen.Cli/DataverseGen.Cli.csproj" -c 'Release' -p:PlatformTarget=AnyCPU -p:Version=$(getPackageVersion) -p:UpdateVersionProperties=false -o $currentPath\publish
}
function buildPackage([bool]$selfContainer){
	Remove-Item .\publish\bin -Recurse -ErrorAction Ignore
	# ReadyToRun/single-file intermediates of the self-contained publish break the framework-dependent one (crash in coreclr)
	Remove-Item .\DataverseGen.Cli\obj\Release\net10.0\win-x64 -Recurse -ErrorAction Ignore
	dotnet publish "DataverseGen.Cli/DataverseGen.Cli.csproj" -p:Version=$(getPackageVersion) -p:UpdateVersionProperties=false /p:DebugType=None /p:DebugSymbols=false -c 'Release' -f "net10.0" -r win-x64 -p:PublishSingleFile=true --self-contained $selfContainer -p:PublishReadyToRun=true -p:PublishDir=$currentPath\publish\bin	
    Set-Location $currentPath
    copyTemplates
    MakeZip $currentPath $selfContainer
}
function copyTemplates() {
    robocopy $currentPath"\DataverseGen.Core\Generators\Scriban\Templates\Multiple\Main\"  $currentPath"/publish/bin/Templates/ExampleTemplate/" *.sbncs /s
    robocopy $currentPath"\DataverseGen.Core\Generators\Scriban\Templates\Multiple\TS-Main\"  $currentPath"/publish/bin/Templates/ExampleTSTemplate/" *.sbncs /s  
}
function getGitVersioion(){
    $gitv = dotnet-gitversion | ConvertFrom-JSON -ErrorAction stop
    return $gitv
}
# Major.Minor come from the release branch / tags, the patch is the number of commits since the version source.
function getPackageVersion(){
    $gitv = getGitVersioion
    return "$($gitv.Major).$($gitv.Minor).$($gitv.CommitsSinceVersionSource)"
}
function MakeZip([string]$currentPath,[bool]$selfContainer){
    $version = getPackageVersion
	if ($selfContainer) {
		$containerName = "includedotnet"
	} else {
		$containerName = "nodotnet"
	}
    $compress = @{
        Path = $currentPath+"/publish/bin/**"
        CompressionLevel = "Fastest"
        DestinationPath = $currentPath+"/publish/DataverseGen-CLI_" +$containerName+"_"+$version+".zip"
      }
      Compress-Archive @compress
}

main
