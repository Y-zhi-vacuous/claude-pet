Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('D:\dev\claude-pet\screenshot.png')
Write-Host "Size: $($bmp.Width) x $($bmp.Height)"
$checks = @(
    @{Name="Ear(left)"; X=35; Y=10},
    @{Name="Eye(left)"; X=42; Y=31},
    @{Name="Nose"; X=50; Y=38},
    @{Name="Body"; X=70; Y=85},
    @{Name="Tail"; X=108; Y=75},
    @{Name="Bg"; X=10; Y=100}
)
foreach ($c in $checks) {
    $p = $bmp.GetPixel($c.X, $c.Y)
    Write-Host "$($c.Name)($($c.X),$($c.Y)): R=$($p.R) G=$($p.G) B=$($p.B)"
}
$bmp.Dispose()
