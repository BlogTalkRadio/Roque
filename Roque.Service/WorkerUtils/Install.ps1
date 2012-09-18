﻿param($installPath, $toolsPath, $package, $project)

foreach ($reference in $project.Object.References)
{
    if (($reference.Name -eq "Roque.Core") -or ($reference.Name -eq "Roque.Redis"))
    {
        if($reference.CopyLocal -eq $true)
        {
            $reference.CopyLocal = $false;
        }
    }
}