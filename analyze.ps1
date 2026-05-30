Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('D:\dev\claude-pet\screenshot.png')
Write-Host "Size: $($bmp.Width) x $($bmp.Height)"
$counts = @{}
for ($y = 0; $y -lt $bmp.Height; $y += 3) {
    for ($x = 0; $x -lt $bmp.Width; $x += 3) {
        $c = $bmp.GetPixel($x, $y)
        $r = [math]::Round($c.R / 32) * 32
        $g = [math]::Round($c.G / 32) * 32
        $b = [math]::Round($c.B / 32) * 32
        $key = "$r,$g,$b"
        if (-not $counts.ContainsKey($key)) { $counts[$key] = 0 }
        $counts[$key]++
    }
}
$total = ($bmp.Width/3) * ($bmp.Height/3)
Write-Host "Sampled pixels: $total"
$counts.GetEnumerator() | Sort-Object Value -Descending | Select -First 10 | ForEach-Object {
    $pct = [math]::Round(100.0 * $_.Value / $total, 1)
    Write-Host "  RGB($($_.Key)) = $($_.Value) ($pct%)"
}
$bmp.Dispose()
