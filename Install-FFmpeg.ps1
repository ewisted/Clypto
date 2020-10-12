function Install-FFmpeg {
    <#
    .SYNOPSIS
    Installs FFmpeg to the specified path and adds a reference to the system path for use in Clypto.
    .DESCRIPTION
    Installs FFmpeg to the specified path and adds a reference to the system path for use in Clypto.
    .EXAMPLE
    Install-FFmpeg -InstallPath "C:\ProgramFiles" -SetPathVariable
    Installs FFmpeg to ProgramFiles and adds a reference to the system path. The resulting path with be "C:\ProgramFiles\ffmpeg\ffmpeg.exe".
    .PARAMETER InstallPath
    The path to install FFmpeg to. Will be added to the system path variable.
    .PARAMETER SetPathVariable
    Whether or not to add an entry to the system path. This is requred unless FFmpeg is downloaded to the app root of the discord bot.
    #>

    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true,ValueFromPipeline=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$InstallPath,
        [Parameter()]
        [switch]$SetPathVariable
    )

    # Define root path and create the directory structure to it, if it doesn't already exist
    $ffmpegParentPath = Join-Path -Path $InstallPath -ChildPath "ffmpeg"
    if (!(Test-Path $ffmpegParentPath)) {
        New-Item -ItemType Directory -Force -Path $ffmpegParentPath -ErrorAction Stop | Out-Null
    }

    # Define the files paths for the zip and exe files
    $ffmpegZipPath = Join-Path -Path $ffmpegParentPath -ChildPath "ffmpeg.zip"
    $ffmpegFilePath = Join-Path -Path $ffmpegParentPath -ChildPath "ffmpeg.exe"

    # Check if already exists
    if (Test-Path $ffmpegFilePath) {
        Write-Host "Skipped downloading ffmpeg, file already exists."
        exit
    }

    Write-Host "Downloading ffmpeg..."

    # Download the zip archive
    $url = "https://github.com/vot/ffbinaries-prebuilt/releases/download/v4.0/ffmpeg-4.0.1-win-64.zip"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $ffmpegZipPath -ErrorAction Stop

    # Extract ffmpeg.exe from the archive
    Add-Type -Assembly System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead("$ffmpegZipPath")
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry("ffmpeg.exe"), $ffmpegFilePath)
    $zip.Dispose()

    # Ensure the file was extracted
    if (!(Test-Path $ffmpegFilePath)) {
        Write-Error "Failed to unzip ffmpeg.exe."
        exit
    }

    # Delete the archive
    Remove-Item "$ffmpegZipPath" -Force

    Write-Host "Finished downloading ffmpeg."

    if ($SetPathVariable) {
        Write-Host "Adding to system path variable..."
        $ffmpegEscapedPath = [regex]::Escape($ffmpegParentPath)
        $arrPath = $env:Path -split ';' | Where-Object {$_ -notMatch "^$ffmpegEscapedPath\\?"}
        $env:Path = ($arrPath + $ffmpegParentPath) -join ';'
        Write-Host "Finished modifying the system path environment variable."
    }
}