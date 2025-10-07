# Add-MauiDllsToPackage.ps1
# Automatically adds Content items to .wapproj for all DLLs in WPF output directory

param(
    [string]$Configuration = "Debug",
    [string]$WapProjPath = "..\Xaml.Demo.Package\Xaml.Demo.Package.wapproj"
)

$wpfBinDir = "..\Xaml.Demo.Host.Wpf\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64"
$resolvedWpfDir = Resolve-Path $wpfBinDir -ErrorAction SilentlyContinue

if (-not $resolvedWpfDir) {
    Write-Error "WPF output directory not found: $wpfBinDir"
    Write-Host "Please build the WPF project first."
    exit 1
}

# Get all DLLs
$dlls = Get-ChildItem -Path $resolvedWpfDir -Filter "*.dll" | Where-Object {
    # Include MAUI and dependency DLLs, exclude Windows system DLLs that are already in the runtime
    $name = $_.Name
    $name -like "Xaml Demo.dll" -or
    $name -like "Microsoft.Maui*.dll" -or
    $name -like "Microsoft.Extensions*.dll" -or
    $name -like "Microsoft.WindowsAppRuntime*.dll" -or
    $name -like "Microsoft.UI.*.dll" -or
    $name -like "System.Reactive*.dll"
}

Write-Host "Found $($dlls.Count) DLLs to add to package:"
$dlls | ForEach-Object { Write-Host "  - $($_.Name)" }

# Generate XML content items
$contentItems = @()
foreach ($dll in $dlls) {
    $relativePath = "..\Xaml.Demo.Host.Wpf\bin\`$(Configuration)\net10.0-windows10.0.19041.0\win-x64\$($dll.Name)"
    $linkPath = "Xaml.Demo.Host.Wpf\$($dll.Name)"
    
    $contentItems += @"
    <Content Include="$relativePath">
      <Link>$linkPath</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
"@
}

Write-Host "`nGenerated Content items:"
Write-Host "<!-- MAUI and dependency DLLs -->"
$contentItems | ForEach-Object { Write-Host $_ }

Write-Host "`n"
Write-Host "Copy the above XML and add it to the <ItemGroup> section in:"
Write-Host "  $WapProjPath"
Write-Host "`nReplace the existing MAUI DLL Content items with these generated ones."
