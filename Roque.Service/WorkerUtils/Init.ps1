param($installPath, $toolsPath, $package)

$global:roquePath = Join-Path $toolsPath "roque"

function global:Roque-Copy-Binaries() {
    $project = Get-Project
    $outputPath = Join-Path $project.Properties.Item("LocalPath").Value $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
    $command = $roquePath + " copybinaries /silent /path="+$outputPath
    Invoke-Expression $command
}

function global:Roque-Run {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Params
    )
    Process {
        Roque-Copy-Binaries
        $project = Get-Project
        $outputPath = Join-Path $project.Properties.Item("LocalPath").Value $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
        $command = (Join-Path $outputPath 'roque ') + $Params
        echo $command
        Invoke-Expression $command
    }
}

function global:Roque-Start {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Params
    )
    Process {
        Roque-Copy-Binaries
        $project = Get-Project
        $outputPath = Join-Path $project.Properties.Item("LocalPath").Value $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
        $command = (Join-Path $outputPath 'roque ') + $Params
        echo $command
        Start-Process -FilePath (Join-Path $outputPath 'roque.exe') -ArgumentList $Params
    }
}

function global:Roque-Work {
    Roque-Start "work"
}

function global:Roque-Work-Debug {
    Roque-Start "work /debug"
}

function global:Roque-Status {
    Roque-Run "status"
}

function global:Roque-Events {
    Roque-Run "events"
}