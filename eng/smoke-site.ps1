param(
    [string]$OutputDir = (Join-Path ([IO.Path]::GetTempPath()) ('keefetch-site-smoke-' + [guid]::NewGuid().ToString('N'))),
    [Alias('EdgePath')]
    [string]$BrowserPath,
    [int]$Port = 18123,
    [int[]]$Widths = @(375,768,1440),
    [ValidateSet('default','forced-colors','reduced-motion','text-200','no-site-scripts')]
    [string]$AccessibilityMode = 'default'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$Widths.Count -or @($Widths | Select-Object -Unique).Count -ne $Widths.Count -or @($Widths | Where-Object { $_ -notin @(375,768,1440) }).Count) { throw 'Widths must select unique values from 375, 768 and 1440.' }
if (!$BrowserPath) {
    $BrowserPath = @('C:\Program Files\Google\Chrome\Application\chrome.exe', 'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe', 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', 'C:\Program Files\Microsoft\Edge\Application\msedge.exe') |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$BrowserPath -or !(Test-Path -LiteralPath $BrowserPath)) { throw 'Installed Chrome or Microsoft Edge is required.' }
if (Test-Path -LiteralPath $OutputDir) { throw 'Use a new smoke output directory to avoid stale evidence.' }
$out = [IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $out | Out-Null
$copy = Join-Path $out 'site'
Copy-Item -LiteralPath (Join-Path $repoRoot 'site') -Destination $copy -Recurse
$sourceHashes = @{}
Get-ChildItem -LiteralPath $copy -File -Recurse | Where-Object { $_.FullName -notlike "$copy\dist\*" -and $_.FullName -notlike "$copy\.git\*" } | ForEach-Object {
    $relative = $_.FullName.Substring($copy.Length + 1).Replace('\','/')
    $sourceHashes[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$sourceHashes | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $out 'source-sha256.json')
$sourceHashes = @{}
Get-ChildItem -LiteralPath $copy -File -Recurse | Where-Object { $_.FullName -notlike "$copy\dist\*" -and $_.FullName -notlike "$copy\.git\*" } | ForEach-Object {
    $relative = $_.FullName.Substring($copy.Length + 1).Replace('\','/')
    $sourceHashes[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$sourceHashes | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $out 'source-sha256.json')

# Only the temporary site receives these assertions. No production behavior changes.
$smokeJs = @'
function checkSmoke() {
  const errors = [];
  const expectedWidth = Number(new URL(location.href).searchParams.get('smoke-width'));
  const expectedHeight = Number(new URL(location.href).searchParams.get('smoke-height'));
  document.documentElement.setAttribute('data-smoke-viewport', `${innerWidth}x${innerHeight}@${devicePixelRatio}`);
  if (innerWidth !== expectedWidth) errors.push('viewport-width-mismatch');
  if (innerHeight !== expectedHeight) errors.push('viewport-height-mismatch');
  const mode = document.querySelector('meta[name="keefetch-smoke-mode"]').content;
  if (mode === 'forced-colors' && !matchMedia('(forced-colors: active)').matches) errors.push('forced-colors-not-active');
  if (mode === 'reduced-motion' && (!matchMedia('(prefers-reduced-motion: reduce)').matches || document.documentElement.dataset.motion !== 'off')) errors.push('reduced-motion-not-active');
  if (mode === 'text-200' && parseFloat(getComputedStyle(document.documentElement).fontSize) < 32) errors.push('text-size-not-active');
  if (mode === 'no-site-scripts') {
    if (document.documentElement.dataset.enhanced || document.documentElement.dataset.motion) errors.push('site-script-executed');
    if (!document.getElementById('primary-nav').getClientRects().length) errors.push('no-script-navigation-hidden');
  }
  if (document.documentElement.scrollWidth > document.documentElement.clientWidth + 1) {
    errors.push('document-overflow');
    const overflow = [...document.querySelectorAll('body *')].filter(node => {
      if (node.closest('.table-wrap,.sr-only')) return false;
      if (node.closest('.vault-window') && !node.classList.contains('vault-window')) return false;
      const r = node.getBoundingClientRect();
      return r.width > 0 && (r.left < -1 || r.right > innerWidth + 1);
    }).slice(0,30).map(node => ({tag:node.tagName,id:node.id,class:node.className,right:node.getBoundingClientRect().right,text:node.textContent.trim().slice(0,60)}));
    document.documentElement.setAttribute('data-smoke-overflow', JSON.stringify(overflow));
  }
  if (document.querySelectorAll('h1').length !== 1) errors.push('h1');
  if (!document.querySelector('main#main')) errors.push('main');
  for (const image of document.images) if (!image.complete || !image.naturalWidth) errors.push('image-load');
  for (const node of document.querySelectorAll('a,button')) {
    if (!node.getClientRects().length) continue;
    const r = node.getBoundingClientRect();
    if (!node.closest('.table-wrap') && (r.left < -1 || r.right > innerWidth + 1)) errors.push('control-overflow');
  }
  document.documentElement.setAttribute('data-smoke-status', errors.length ? errors.join(',') : 'pass');
}
addEventListener('load', () => { checkSmoke(); document.fonts.ready.then(checkSmoke); });
// Fullscreen sizing can settle after load; measure the final resize too.
addEventListener('resize', () => { if (document.readyState === 'complete') checkSmoke(); });
// Profile cards load asynchronously from local JSON after the page load event.
new MutationObserver(() => { if (document.readyState === 'complete') checkSmoke(); }).observe(document.body, {childList:true,subtree:true});
'@
[IO.File]::WriteAllText((Join-Path $copy 'smoke.js'), $smokeJs)
$pages = @('index.html','getting-started.html','profiles.html','privacy.html','troubleshooting.html','benchmarks.html','contributing.html')
foreach ($page in $pages) {
    $path = Join-Path $copy $page
    $head = '<meta name="keefetch-smoke-mode" content="'+$AccessibilityMode+'">'
    if ($AccessibilityMode -eq 'no-site-scripts') {
        # Block production scripts while allowing only the measurement script.
        $head += '<meta http-equiv="Content-Security-Policy" content="script-src ''nonce-keefetch-smoke''">'
    }
    $tail = '<script src="smoke.js" nonce="keefetch-smoke" defer></script>'
    if ($AccessibilityMode -eq 'text-200') { $tail += '<style>html { font-size: 200% !important; }</style>' }
    $html = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8).Replace('<head>', '<head>'+$head).Replace('</head>', $tail+'</head>')
    [IO.File]::WriteAllText($path, $html)
}
$python = (Get-Command python -ErrorAction Stop).Source
$server = Start-Process -FilePath $python -ArgumentList @('-m','http.server',"$Port",'--bind','127.0.0.1','--directory',('"'+$copy+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'server.log') -RedirectStandardError (Join-Path $out 'server.err')
try {
    $ready = $false
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        try { Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:$Port/index.html" -TimeoutSec 2 | Out-Null; $ready=$true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (!$ready) { throw 'Local smoke server did not start.' }
    $results = @()
    $heights = @{375=812;768=1024;1440=1000}
    foreach ($width in $Widths) {
        $size = @($width,$heights[$width])
        $windowWidth = $size[0]
        $windowHeight = $size[1]
        foreach ($page in $pages) {
            for ($renderAttempt = 1; $renderAttempt -le 3; $renderAttempt++) {
            $name = [IO.Path]::GetFileNameWithoutExtension($page) + '-' + $size[0] + '-attempt-' + $renderAttempt
            $png = Join-Path $out ($name+'.png')
            $dom = Join-Path $out ($name+'.html')
            $profile = Join-Path $out ($name+'-browser-profile')
            $arguments = @('--headless=new','--disable-gpu','--force-device-scale-factor=1','--no-first-run','--no-default-browser-check','--disable-background-networking',('--user-data-dir="'+$profile+'"'),('--window-size='+$windowWidth+','+$windowHeight),'--hide-scrollbars','--virtual-time-budget=3000','--dump-dom',('--screenshot="'+$png+'"'),("http://127.0.0.1:$Port/$($page)?smoke-width=$($size[0])&smoke-height=$($size[1])"))
            # A virtual screen plus fullscreen avoids desktop window chrome and
            # minimum-window-width clipping in Chromium's unified headless mode.
            $arguments = @('--start-fullscreen',('--screen-info={'+$windowWidth+'x'+$windowHeight+'}')) + $arguments
            if ($AccessibilityMode -eq 'forced-colors') { $arguments = @('--force-high-contrast') + $arguments }
            if ($AccessibilityMode -eq 'reduced-motion') { $arguments = @('--force-prefers-reduced-motion') + $arguments }
            # Own the process handle and drain both pipes. Refreshing the wrapper
            # returned by Start-Process can lose a fast-exiting browser's exit code.
            $start = New-Object System.Diagnostics.ProcessStartInfo
            $start.FileName = $BrowserPath
            $start.Arguments = $arguments -join ' '
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $process = New-Object System.Diagnostics.Process
            $process.StartInfo = $start
            try {
                if (!$process.Start()) { throw "Browser could not start: $name" }
                $stdout = $process.StandardOutput.ReadToEndAsync()
                $stderr = $process.StandardError.ReadToEndAsync()
                if (!$process.WaitForExit(45000)) {
                    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
                    throw "Browser timed out: $name"
                }
                $exitCode = $process.ExitCode
                [IO.File]::WriteAllText($dom, $stdout.GetAwaiter().GetResult())
                [IO.File]::WriteAllText((Join-Path $out ($name+'.err')), $stderr.GetAwaiter().GetResult())
            } finally { $process.Dispose() }
            if ($exitCode -ne 0 -or !(Test-Path $png) -or (Get-Item $png).Length -lt 1000) { throw "Render failed: $name (exit $exitCode)" }
            $rendered = [IO.File]::ReadAllText($dom, [Text.Encoding]::UTF8)
            if ($rendered -notmatch 'data-smoke-viewport="(\d+)x(\d+)@1"') { throw "Missing measured viewport: $name" }
            $actualWidth = [int]$Matches[1]
            $actualHeight = [int]$Matches[2]
            if ($actualWidth -ne $size[0] -or $actualHeight -ne $size[1]) {
                if ($renderAttempt -eq 3) { throw "Could not establish exact viewport: $name ($actualWidth x $actualHeight)" }
                # Account for platform-specific desktop frame insets. Preserve
                # every attempted DOM/screenshot and remeasure a fresh process.
                $windowWidth += $size[0] - $actualWidth
                $windowHeight += $size[1] - $actualHeight
                continue
            }
            if ($rendered -notmatch 'data-smoke-status="pass"') { throw "DOM smoke failed: $name; inspect $dom" }
            $results += [pscustomobject]@{page=$page;width=$actualWidth;height=$actualHeight;mode=$AccessibilityMode;status='pass';attempts=$renderAttempt;screenshot=$png}
            break
            }
        }
    }
    $results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'results.json')
    $version = (Get-Item -LiteralPath $BrowserPath).VersionInfo
    [pscustomobject]@{path=$BrowserPath;product=$version.ProductName;version=$version.ProductVersion;mode=$AccessibilityMode} | ConvertTo-Json | Set-Content (Join-Path $out 'browser.json')
    Write-Output "Site smoke passed: $($results.Count) page/viewports. Evidence: $out"
} finally {
    if (!$server.HasExited) { Stop-Process -Id $server.Id -ErrorAction SilentlyContinue }
}
