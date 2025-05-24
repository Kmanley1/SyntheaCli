$RootDir         = "C:\_Template\Projects\_code\synthea-cli"
$SolutionName    = "Synthea.Cli"              
$ProjectName     = "Synthea.Cli"
$TestProjectName = "Synthea.Cli.UnitTests"

#mkdir $RootDir; 
Set-Location $RootDir
dotnet new sln                 -n $SolutionName
dotnet new console             -n $ProjectName     --framework net8.0
dotnet new xunit               -n $TestProjectName --framework net8.0
dotnet sln $SolutionName.sln   add "$ProjectName\$ProjectName.csproj"
dotnet sln $SolutionName.sln   add "$TestProjectName\$TestProjectName.csproj"
dotnet add "$TestProjectName\$TestProjectName.csproj" reference "$ProjectName\$ProjectName.csproj"
