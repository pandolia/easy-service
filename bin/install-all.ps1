Write-Host EasyService Batch Install Tool
Write-Host
Write-Host This tool installs all EasyServices in all sub-directories in current directory.

ForEach($d in (Get-Childitem -Directory))
{
    if (-Not (Test-Path -Path "$d\svc.conf" -PathType Leaf)) {
        Write-Host
        Write-Host No svc.conf found in $d
        continue
    }

    Write-Host
    Write-Host svc install "$d\"
    svc install "$d\"
}