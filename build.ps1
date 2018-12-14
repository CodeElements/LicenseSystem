$path = (Get-Location).Path
$currentPath = Split-Path -Parent $path

while ($currentPath -like "*:\*") {
    if (Get-ChildItem -Path $currentPath -Filter "*.sln")
    {
        $path = $currentPath
    }

    $currentPath = Split-Path -Parent $currentPath
}

$artifactsPath = "$path\artifacts"

# Cleanup old template files
Get-ChildItem ./**/*.pp -Recurse | ForEach-Object { Remove-Item $_ }

# CodeElements.LicenseSystemUI.WinForms
$files = Get-ChildItem -Path ./CodeElements.LicenseSystemUI.WinForms -Filter *.cs -File | Where-Object Name -NotMatch "Program.cs"
$files | ForEach-Object {
    Get-Content $_.FullName | Set-Content ($_.FullName + ".pp")
}

dotnet pack CodeElements.LicenseSystemUI.WinForms/CodeElements.LicenseSystemUI.WinForms.Packable.csproj -o $artifactsPath

# CodeElements.LicenseSystem
$content = Get-Content ./CodeElements.LicenseSystem/LicenseSystem.cs -Raw
$content = $content -replace "        /// <summary>`r`n        ///     The license types of your project. TODO This enum must be replaced by your definitions.`r`n        /// </summary>`r`n        public enum LicenseTypes`r`n        {`r`n        }",""

New-Item -ItemType Directory -Force -Path ./CodeElements.LicenseSystem/net20
New-Item -ItemType Directory -Force -Path ./CodeElements.LicenseSystem/net45

"#define NET20`r`n#define MANUAL_INITIALIZATION`r`n" + $content | Out-File ./CodeElements.LicenseSystem/net20/LicenseSystem.cs.pp
"#define MANUAL_INITIALIZATION`r`n" + $content | Out-File ./CodeElements.LicenseSystem/net45/LicenseSystem.cs.pp
"#define NETSTANDARD`r`n#define MANUAL_INITIALIZATION`r`n" + $content | Out-File ./CodeElements.LicenseSystem/LicenseSystem.cs.pp

$content = Get-Content ./CodeElements.LicenseSystem/LicenseSystem.vb -Raw
$content = $content -replace "        ''' <summary>`r`n        '''     The license types of your project. TODO This enum must be replaced by your definitions.`r`n        ''' </summary>`r`n        Public Enum LicenseTypes`r`n            ReplaceMe`r`n        End Enum",""

"#Const NET20 = True`r`n#Const MANUAL_INITIALIZATION = True`r`n" + $content | Out-File ./CodeElements.LicenseSystem/net20/LicenseSystem.vb.pp
"#Const MANUAL_INITIALIZATION = True`r`n" + $content | Out-File ./CodeElements.LicenseSystem/net45/LicenseSystem.vb.pp
"#Const NETSTANDARD = True`r`n#Const MANUAL_INITIALIZATION = True`r`n" + $content | Out-File ./CodeElements.LicenseSystem/LicenseSystem.vb.pp

dotnet pack CodeElements.LicenseSystem/CodeElements.LicenseSystem.csproj -o $artifactsPath

