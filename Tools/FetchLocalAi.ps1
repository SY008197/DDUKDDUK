param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\Game')
)

$ErrorActionPreference = 'Stop'
$taskProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if (-not (Test-Path -LiteralPath (Join-Path $taskProjectRoot 'ProjectSettings\ProjectVersion.txt'))) {
    throw "Unity Game 폴더를 찾지 못했습니다: $taskProjectRoot"
}
$taskAiRoot = Join-Path $taskProjectRoot 'Assets\StreamingAssets\LocalAi'
$taskModelDirectory = Join-Path $taskAiRoot 'Models'
$taskRuntimeDirectory = Join-Path $taskAiRoot 'Runtime\win-x64'
$taskLicenseDirectory = Join-Path $taskAiRoot 'Licenses'
New-Item -ItemType Directory -Path $taskModelDirectory,$taskRuntimeDirectory,$taskLicenseDirectory -Force | Out-Null

$taskModelUrl = 'https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/23749fefcc72300e3a2ad315e1317431b06b590a/Qwen3-0.6B-Q8_0.gguf?download=true'
$taskModelHash = '9465E63A22ADD5354D9BB4B99E90117043C7124007664907259BD16D043BB031'
$taskModelPath = Join-Path $taskModelDirectory 'model.gguf'
$taskModelValid = (Test-Path -LiteralPath $taskModelPath) -and
    ((Get-FileHash -LiteralPath $taskModelPath -Algorithm SHA256).Hash -eq $taskModelHash)
if ($taskModelValid) {
    Write-Output 'Qwen3 모델 확인 완료. 다시 다운로드하지 않습니다.'
} else {
    Write-Output '공식 Qwen3-0.6B 모델을 다운로드합니다. 약 639 MB입니다.'
    $taskPartialModel = Join-Path $taskModelDirectory 'model.gguf.partial'
    Invoke-WebRequest -Uri $taskModelUrl -OutFile $taskPartialModel
    if ((Get-FileHash -LiteralPath $taskPartialModel -Algorithm SHA256).Hash -ne $taskModelHash) {
        throw '모델 SHA256이 공식 고정 버전과 다릅니다. 다운로드를 다시 실행하세요.'
    }
    Move-Item -LiteralPath $taskPartialModel -Destination $taskModelPath -Force
}

$taskServerPath = Join-Path $taskRuntimeDirectory 'llama-server.exe'
if (-not (Test-Path -LiteralPath $taskServerPath)) {
    Write-Output '공식 llama.cpp Windows CPU 실행 파일을 다운로드합니다.'
    $taskZip = Join-Path $taskAiRoot 'llama-b11344-win-cpu-x64.zip.partial'
    Invoke-WebRequest -Uri 'https://github.com/ggml-org/llama.cpp/releases/download/b11344/llama-b11344-bin-win-cpu-x64.zip' -OutFile $taskZip
    if ((Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash -ne 'E604411CE3D01DE6E5BA28241D7565F74EEC83D3844E4CE8317B084D451E46CE') {
        throw 'llama.cpp ZIP의 SHA256이 고정 버전과 다릅니다.'
    }
    $taskNamedZip = Join-Path $taskAiRoot 'llama-b11344-win-cpu-x64.zip'
    Move-Item -LiteralPath $taskZip -Destination $taskNamedZip -Force
    Expand-Archive -LiteralPath $taskNamedZip -DestinationPath $taskRuntimeDirectory -Force
}

if (-not (Test-Path -LiteralPath (Join-Path $taskLicenseDirectory 'llama.cpp-MIT.txt'))) {
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/ggml-org/llama.cpp/b11344/LICENSE' -OutFile (Join-Path $taskLicenseDirectory 'llama.cpp-MIT.txt')
}
if (-not (Test-Path -LiteralPath (Join-Path $taskLicenseDirectory 'Qwen3-Apache-2.0.txt'))) {
    Invoke-WebRequest -Uri 'https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/23749fefcc72300e3a2ad315e1317431b06b590a/LICENSE' -OutFile (Join-Path $taskLicenseDirectory 'Qwen3-Apache-2.0.txt')
}

# These release CRT files are tracked alongside the helper. Do not copy DLLs from
# System32 or silently install system software; the distributable source matters.
foreach ($taskCrtName in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskRuntimeDirectory $taskCrtName))) {
        throw "동봉된 $taskCrtName 파일이 빠졌습니다. 저장소의 Runtime/win-x64 폴더 전체를 다시 받으세요."
    }
}

Write-Output '로컬 AI 준비 완료. Unity에서 Windows x64로 빌드하면 모델과 실행 파일이 함께 포함됩니다.'
