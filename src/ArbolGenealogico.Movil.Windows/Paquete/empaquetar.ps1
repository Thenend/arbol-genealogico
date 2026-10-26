# Hace el paquete MSIX de Ramo para la Microsoft Store (en Windows, con el SDK de Windows instalado; en GitHub lo hace
# .github/workflows/windows.yml). Los datos de identidad salen de Partner Center > Ramo > Gestión de productos >
# Identidad del producto. No hace falta firmarlo: la tienda lo firma al publicarlo.
#
#   .\empaquetar.ps1 -Nombre 12345Mario.Ramo -Editor "CN=XXXXXXXX-..." -EditorVisible "Mario" -Version 1.0.1.0
param(
    [string]$Nombre = "Ramo.Pruebas",
    [string]$Editor = "CN=Ramo",
    [string]$EditorVisible = "Ramo",
    [string]$Version = "1.0.0.0",
    [ValidateSet("x64", "arm64")][string]$Arquitectura = "x64",
    [string]$Salida = "$PSScriptRoot\..\..\..\dist"
)
$ErrorActionPreference = "Stop"
$proyecto = Resolve-Path "$PSScriptRoot\.."
$trabajo = Join-Path $env:TEMP "ramo-msix-$Arquitectura"
if (Test-Path $trabajo) { Remove-Item $trabajo -Recurse -Force }

# la aplicación, con .NET dentro (la persona no tiene que instalar nada más)
dotnet publish $proyecto -c Release -r "win-$Arquitectura" --self-contained true -p:PublishReadyToRun=true -o $trabajo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ha fallado" }

Copy-Item "$PSScriptRoot\Imagenes" "$trabajo\Imagenes" -Recurse
Copy-Item "$PSScriptRoot\Strings" "$trabajo\Strings" -Recurse
(Get-Content "$PSScriptRoot\Package.appxmanifest" -Raw -Encoding UTF8).
    Replace('$NOMBRE$', $Nombre).Replace('$EDITOR$', $Editor).Replace('$EDITOR_VISIBLE$', $EditorVisible).
    Replace('$VERSION$', $Version).Replace('$ARQUITECTURA$', $Arquitectura) |
    Set-Content "$trabajo\AppxManifest.xml" -Encoding UTF8

# makeappx y makepri del SDK de Windows (la versión más nueva que haya)
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" | Sort-Object FullName | Select-Object -Last 1
if (-not $sdk) { throw "No está el SDK de Windows (makeappx.exe)" }
$bin = $sdk.DirectoryName

# resources.pri: para que Windows elija el icono de cada tamaño (barra de tareas, menú Inicio) y el nombre en cada idioma
Push-Location $trabajo
& "$bin\makepri.exe" createconfig /cf priconfig.xml /dq es-ES /o | Out-Null
# todos los idiomas en el mismo resources.pri (sin esto, makepri saca un .pri por idioma, que van en otros paquetes)
$config = [xml](Get-Content priconfig.xml -Raw)
$config.SelectNodes("//packaging") | ForEach-Object { $_.ParentNode.RemoveChild($_) } | Out-Null
$config.Save("$PWD\priconfig.xml")
& "$bin\makepri.exe" new /pr . /cf priconfig.xml /mn AppxManifest.xml /of resources.pri /o
if ($LASTEXITCODE -ne 0) { throw "makepri ha fallado" }
& "$bin\makepri.exe" dump /if resources.pri /of dump.xml /dt Detailed /o | Out-Null
$nombres = Select-String -Path dump.xml -Pattern "Ramo – " -AllMatches
if ($nombres.Matches.Count -lt 6) { throw "Faltan nombres de la aplicación en resources.pri" }
Remove-Item priconfig.xml, dump.xml, Strings -Recurse
Pop-Location

New-Item -ItemType Directory -Force $Salida | Out-Null
$msix = Join-Path (Resolve-Path $Salida) "Ramo-$Version-$Arquitectura.msix"
& "$bin\makeappx.exe" pack /d $trabajo /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx ha fallado" }
Write-Host "Hecho: $msix"
