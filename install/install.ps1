# UnityRiderBench CLI 설치 스크립트
# 사용법: irm https://raw.githubusercontent.com/kimminsu-123/Unity-Rider-Benchmarker/main/install/install.ps1 | iex

$ErrorActionPreference = "Stop"

$repo = "kimminsu-123/Unity-Rider-Benchmarker"
$installDir = Join-Path $env:LOCALAPPDATA "UnityRiderBench"
$exeName = "unityrider-bench.exe"

Write-Host "최신 릴리스 정보를 확인하는 중..."
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest"
$asset = $release.assets | Where-Object { $_.name -like "*win-x64.zip" } | Select-Object -First 1

if (-not $asset) {
    throw "win-x64 릴리스 자산을 찾을 수 없습니다. $repo 저장소에 릴리스가 게시되었는지 확인하세요."
}

$zipPath = Join-Path $env:TEMP $asset.name
Write-Host "다운로드 중: $($asset.browser_download_url)"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

Write-Host "설치 중: $installDir"
Expand-Archive -Path $zipPath -DestinationPath $installDir -Force
Remove-Item $zipPath -Force

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$installDir*") {
    Write-Host "PATH 환경변수에 등록 중..."
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$installDir", "User")
    $env:Path = "$env:Path;$installDir"
}

Write-Host ""
Write-Host "설치 완료. 새 터미널을 열고 다음 명령으로 확인하세요:"
Write-Host "  $exeName --help"
