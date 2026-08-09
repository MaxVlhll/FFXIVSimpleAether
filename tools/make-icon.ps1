# Genere images/icon.png (512x512), l'icone du plugin dans Dalamud.
# En code plutot qu'en binaire opaque : retoucher une couleur ne demande pas d'outil.
# Contrainte DIP17 : icon.png entre 64x64 et 512x512, dans images/ a cote du manifeste.

Add-Type -AssemblyName System.Drawing

$size = 512
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.InterpolationMode = 'HighQualityBicubic'
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# --- Fond : carre arrondi sombre, degrade diagonal pour eviter l'aplat mort ---
$bg = New-RoundedRect 0 0 $size $size 104
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point($size, $size)),
    [System.Drawing.Color]::FromArgb(255, 26, 42, 54),
    [System.Drawing.Color]::FromArgb(255, 9, 16, 22))
$g.FillPath($bgBrush, $bg)

# Halo vert derriere le pin : suggere l'ether sans ajouter de forme a lire.
$halo = New-Object System.Drawing.Drawing2D.GraphicsPath
$halo.AddEllipse(70, 60, 372, 372)
$haloBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($halo)
$haloBrush.CenterColor = [System.Drawing.Color]::FromArgb(90, 90, 230, 140)
$haloBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 90, 230, 140))
$g.SetClip($bg)
$g.FillPath($haloBrush, $halo)

# --- Pin : cercle + pointe, la forme de marqueur de carte ---
$cx = 256.0; $cy = 216.0; $r = 132.0; $tipY = 452.0
$pin = New-Object System.Drawing.Drawing2D.GraphicsPath
# Arc de 135 deg a 45 deg dans le sens horaire : laisse l'ouverture en bas pour la pointe.
$pin.AddArc(($cx - $r), ($cy - $r), ($r * 2), ($r * 2), 135, 270)
$pin.AddLine(($cx + $r * [Math]::Cos([Math]::PI / 4)), ($cy + $r * [Math]::Sin([Math]::PI / 4)), $cx, $tipY)
$pin.CloseFigure()

$pinBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(120, 90)),
    (New-Object System.Drawing.Point(390, 460)),
    [System.Drawing.Color]::FromArgb(255, 138, 240, 150),
    [System.Drawing.Color]::FromArgb(255, 32, 150, 88))
$g.FillPath($pinBrush, $pin)

$pinPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 12, 40, 26), 10)
$pinPen.LineJoin = 'Round'
$g.DrawPath($pinPen, $pin)

# --- Spirale d'ether dans la tete du pin ---
# Spirale d'Archimede echantillonnee : une courbe ouverte lit mieux qu'un glyphe
# a 32 px, taille reelle de l'icone dans la liste des plugins.
$pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
for ($i = 0; $i -le 160; $i++) {
    $t = ($i / 160.0) * (2.7 * [Math]::PI)
    $rad = 7 + 9.4 * $t
    $pts.Add((New-Object System.Drawing.PointF(
        [float]($cx + $rad * [Math]::Cos($t - [Math]::PI / 2)),
        [float]($cy + $rad * [Math]::Sin($t - [Math]::PI / 2)))))
}
$spiralPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 10, 38, 24), 26)
$spiralPen.StartCap = 'Round'
$spiralPen.EndCap = 'Round'
$spiralPen.LineJoin = 'Round'
$g.DrawCurve($spiralPen, $pts.ToArray())

# Reflet en haut a gauche du pin : donne du volume, cout nul.
$glossPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 255, 255), 14)
$glossPen.StartCap = 'Round'
$glossPen.EndCap = 'Round'
$g.DrawArc($glossPen, ($cx - $r + 30), ($cy - $r + 30), (($r - 30) * 2), (($r - 30) * 2), 195, 70)

$g.ResetClip()

$outDir = Join-Path $PSScriptRoot '..\images'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$out = Join-Path $outDir 'icon.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose(); $bmp.Dispose()
"$out  $((Get-Item $out).Length) octets"
