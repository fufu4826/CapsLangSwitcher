$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot 'CapsLangSwitcher.cs'
$output = Join-Path $PSScriptRoot 'CapsLangSwitcher.exe'

Add-Type -AssemblyName Microsoft.CSharp
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.OutputAssembly = $output
$parameters.CompilerOptions = '/target:winexe /optimize'
$parameters.ReferencedAssemblies.Add('System.dll') | Out-Null
$parameters.ReferencedAssemblies.Add('System.Core.dll') | Out-Null
$parameters.ReferencedAssemblies.Add('System.Drawing.dll') | Out-Null
$parameters.ReferencedAssemblies.Add('System.Windows.Forms.dll') | Out-Null

$result = $provider.CompileAssemblyFromFile($parameters, $source)
if ($result.Errors.Count -gt 0) {
    $result.Errors | ForEach-Object { Write-Error $_.ToString() }
    exit 1
}

Write-Host "Built $output"
