param([string]$GoogleOAuthFile)

$ErrorActionPreference='Stop'
$auroraRoot=$PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if(-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))){
    $framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$references=@('System.dll','System.Core.dll','System.Security.dll','System.Management.dll','System.Xml.dll','System.Xml.Linq.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','Microsoft.VisualBasic.dll','System.IO.Compression.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll')
$arguments=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',
    ('/out:'+(Join-Path $auroraRoot 'OuO.exe')),
    ('/win32manifest:'+(Join-Path $auroraRoot 'source\app.manifest')),
    ('/win32icon:'+(Join-Path $auroraRoot 'assets\branding\ouo-circuito.ico')))
foreach($reference in $references){$arguments+='/reference:'+(Join-Path $framework $reference)}
$arguments+=(Get-ChildItem -LiteralPath (Join-Path $auroraRoot 'source') -Filter '*.cs' -File | ForEach-Object {$_.FullName})
$metadataPath=$null
try{
    if($GoogleOAuthFile){
        $inputFile=Get-Item -LiteralPath $GoogleOAuthFile
        if($inputFile.Length -gt 65536){throw 'O arquivo de cadastro do Google excede o tamanho esperado.'}
        $client=(Get-Content -LiteralPath $inputFile.FullName -Raw | ConvertFrom-Json).installed
        if(-not $client -or $client.client_id -cnotmatch '\A[0-9a-z-]{8,160}\.apps\.googleusercontent\.com\z' -or
           $client.client_secret -cnotmatch '\A[A-Za-z0-9_-]{8,200}\z'){
            throw 'Use o JSON do cliente OAuth do OuO para aplicativo de computador.'
        }
        # Include only the public desktop-client registration, never account tokens.
        $metadata=@{installed=@{client_id=[string]$client.client_id;client_secret=[string]$client.client_secret}} | ConvertTo-Json -Depth 4
        $metadataPath=Join-Path ([IO.Path]::GetTempPath()) ('ouo-google-'+[Guid]::NewGuid().ToString('N')+'.json')
        [IO.File]::WriteAllText($metadataPath,$metadata,[Text.UTF8Encoding]::new($false))
        $arguments+='/resource:'+$metadataPath+',OuO.GoogleOAuth.json,private'
    }
    & (Join-Path $framework 'csc.exe') @arguments
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar OuO.'}
}finally{
    if($metadataPath -and (Test-Path -LiteralPath $metadataPath)){Remove-Item -LiteralPath $metadataPath}
}

$sensorArguments=@('/nologo','/target:exe','/platform:anycpu','/optimize+','/codepage:65001',
    ('/out:'+(Join-Path $auroraRoot 'Sensors\Aurora.Sensors.exe')))
foreach($reference in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){
    $sensorArguments+='/reference:'+(Join-Path $framework $reference)
}
$sensorArguments+='/reference:'+(Join-Path $auroraRoot 'Sensors\LibreHardwareMonitorLib.dll')
$sensorArguments+=Join-Path $auroraRoot 'source\sensors\SensorHost.cs'
& (Join-Path $framework 'csc.exe') @sensorArguments
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o motor de sensores.'}
'OuO.exe compilado.'
