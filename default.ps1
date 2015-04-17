properties {
    $projectName = "Cedar"
    $buildNumber = 0
    $rootDir  = Resolve-Path .\
    $buildOutputDir = "$rootDir\build"
    $mergedDir = "$buildOutputDir\merged"
    $reportsDir = "$buildOutputDir\reports"
    $srcDir = "$rootDir\src"
    $solutionFilePath = "$srcDir\$projectName.sln"
    $assemblyInfoFilePath = "$srcDir\SharedAssemblyInfo.cs"
    $ilmergePath = "$srcDir\packages\ILMerge.2.14.1208\tools\ilmerge.exe"
}

task default -depends Clean, UpdateVersion, CreateNuGetPackages, RunTests

task Clean {
    Remove-Item $buildOutputDir -Force -Recurse -ErrorAction SilentlyContinue
    exec { msbuild /nologo /verbosity:quiet $solutionFilePath /t:Clean /p:platform="Any CPU"}
}

task UpdateVersion {
    $version = Get-Version $assemblyInfoFilePath
    $oldVersion = New-Object Version $version
    $newVersion = New-Object Version ($oldVersion.Major, $oldVersion.Minor, $oldVersion.Build, $buildNumber)
    Update-Version $newVersion $assemblyInfoFilePath
}

task Compile {
    exec { msbuild /nologo /verbosity:quiet $solutionFilePath /p:Configuration=Release /p:platform="Any CPU"}
}

task RunTests -depends Compile {
    $xunitRunner = "$srcDir\packages\xunit.runner.console.2.0.0\tools\xunit.console.exe"
    $reportDir = "$reportsDir\tests\"
    EnsureDirectory $reportDir

    .$xunitRunner "$srcDir\Cedar.Tests\bin\Release\Cedar.Tests.dll" -html "$reportDir\index.html" -xml "$reportDir\tests.xml"

    # Pretty-print the xml
    [Reflection.Assembly]::LoadWithPartialName("System.Xml.Linq")
    [System.Xml.Linq.XDocument]::Load("$reportDir\tests.xml").Save("$reportDir\tests.xml")
}

task ILMerge -depends Compile {
    New-Item $mergedDir -Type Directory -ErrorAction SilentlyContinue

    $dllDir = "$srcDir\Cedar\bin\Release"
    $inputDlls = "$dllDir\Cedar.dll"
    @(	"EnsureThat",
        "System.Reactive.Core",
        "System.Reactive.Interfaces",
        "System.Reactive.Linq",
        "System.Reactive.PlatformServices") |% { $inputDlls = "$inputDlls $dllDir\$_.dll" }
    Invoke-Expression "$ilmergePath /targetplatform:v4 /internalize /allowDup /target:library /log /out:$mergedDir\Cedar.dll $inputDlls"

    $dllDir = "$srcDir\Cedar.NEventStore\bin\Release"
    $inputDlls = "$dllDir\Cedar.NEventStore.dll"
    @(	"EnsureThat",
        "System.Reactive.Core",
        "System.Reactive.Interfaces",
        "System.Reactive.Linq",`
        "System.Reactive.PlatformServices") |% { $inputDlls = "$inputDlls $dllDir\$_.dll" }
    Invoke-Expression "$ilmergePath /targetplatform:v4 /internalize /allowDup /target:library /log /out:$mergedDir\Cedar.NEventStore.dll $inputDlls"

    $dllDir = "$srcDir\Cedar.GetEventStore\bin\Release"
    $inputDlls = "$dllDir\Cedar.GetEventStore.dll"
    @(	"EnsureThat",
        "Newtonsoft.Json",
        "System.Reactive.Core",
        "System.Reactive.Interfaces",
        "System.Reactive.Linq",`
        "System.Reactive.PlatformServices") |% { $inputDlls = "$inputDlls $dllDir\$_.dll" }
    Invoke-Expression "$ilmergePath /targetplatform:v4 /internalize /allowDup /target:library /log /out:$mergedDir\Cedar.GetEventStore.dll $inputDlls"

    $dllDir = "$srcDir\Cedar.Testing\bin\Release"
    $inputDlls = "$dllDir\Cedar.Testing.dll "
    @(	"Inflector",
        "OwinHttpMessageHandler",
        "System.Reactive.Core",
        "System.Reactive.Interfaces",
        "System.Reactive.Linq",`
        "KellermanSoftware.Compare-NET-Objects",
        "System.Reactive.PlatformServices") |% { $inputDlls = "$inputDlls $dllDir\$_.dll" }
    Invoke-Expression "$ilmergePath /targetplatform:v4 /internalize /allowDup /target:library /log /out:$mergedDir\Cedar.Testing.dll $inputDlls"

    $dllDir = "$srcDir\Cedar.Testing.TestRunner\bin\Release"
    $inputDlls = "$dllDir\Cedar.Testing.TestRunner.exe "
    @("PowerArgs") |% { $inputDlls = "$inputDlls $dllDir\$_.dll" }
    Invoke-Expression "$ilmergePath /targetplatform:v4 /internalize /allowDup /target:exe /log /out:$mergedDir\Cedar.Testing.TestRunner.exe $inputDlls"
}

task CreateNuGetPackages -depends ILMerge {
    $versionString = Get-Version $assemblyInfoFilePath
    $version = New-Object Version $versionString
    $packageVersion = $version.Major.ToString() + "." + $version.Minor.ToString() + "." + $version.Build.ToString() + "-build" + $buildNumber.ToString().PadLeft(5,'0')
    $packageVersion
    gci $srcDir -Recurse -Include *.nuspec | % {
        exec { .$srcDir\.nuget\nuget.exe pack $_ -o $buildOutputDir -version $packageVersion }
    }
}

function EnsureDirectory {
    param($directory)

    if(!(test-path $directory))	{
        mkdir $directory
    }
}
