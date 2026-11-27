# Static artwork only; no market API. Candidate filenames are validated as images, not visually verified.
$ErrorActionPreference='Stop'
$dest=Join-Path $PSScriptRoot '../ProjectPoeLedger/Web/boss-art'
New-Item -ItemType Directory -Force $dest | Out-Null
$files=[ordered]@{
 'Atziri'=@('Atziri, Queen of the Vaal monster screenshot.jpg')
 'Aul, the Crystal King'=@('Aul, the Crystal King.jpg','Aul, the Crystal King.png')
 'Ahuatotli, the Blind'=@('Ahuatotli, the Blind.png','Ahuatotli, the Blind.jpg')
 'Kurgal, the Blackblooded'=@('Kurgal, the Blackblooded.png','Kurgal, the Blackblooded.jpg')
 'Saresh, of the Weeping Black'=@('Saresh, of the Weeping Black.png','Saresh, of the Weeping Black monster screenshot.jpg')
 'Zorath, Vile Assembled'=@('Zorath, Vile Assembled.png','Zorath, Vile Assembled monster screenshot.jpg')
 'Velka, the Tide Witch'=@('Velka, the Tide Witch.png','Velka, the Tide Witch monster screenshot.jpg')
 'Exarch'=@('The Searing Exarch.png')
 'Eater'=@('The Eater of Worlds.png')
 'Black Star'=@('The Black Star.png')
 'Infinite Hunger'=@('The Infinite Hunger.png')
 'Maven'=@('The Maven.png','The Maven monster screenshot.jpg','The Maven.jpg')
 'Shaper'=@('The Shaper.png','The Shaper monster screenshot.jpg','The Shaper.jpg')
 'Elder'=@('The Elder.png','The Elder monster screenshot.jpg','The Elder.jpg')
 'Sirus'=@('Sirus, Awakener of Worlds.png','Sirus, Awakener of Worlds monster screenshot.jpg','Sirus, Awakener of Worlds.jpg')
 'Cortex'=@('Venarius.png','Venarius monster screenshot.jpg','Venarius.jpg')
 'Oshabi'=@('Oshabi, Avatar of the Grove.png','Oshabi, Avatar of the Grove monster screenshot.jpg','Oshabi, Avatar of the Grove.jpg')
 'Incarnation of Neglect'=@('Incarnation of Neglect.png')
 'Incarnation of Dread'=@('Incarnation of Dread.png','Incarnation of Dread monster screenshot.jpg')
 'Incarnation of Fear'=@('Incarnation of Fear.png','Incarnation of Fear monster screenshot.jpg')
}
$map=[ordered]@{}
Add-Type -AssemblyName System.Drawing
foreach($name in $files.Keys){
 foreach($file in $files[$name]){
  $url='https://www.poewiki.net/wiki/Special:FilePath/'+[uri]::EscapeDataString($file.Replace(' ','_'))
  $temp=Join-Path $dest ([guid]::NewGuid().ToString()+'.tmp')
  $slug=($name.ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')+'.png'
  try{
   Invoke-WebRequest -Uri $url -OutFile $temp -TimeoutSec 15 -UserAgent 'PoeLedger/0.46.1 (static game artwork)'
   $img=[System.Drawing.Image]::FromFile($temp)
   try{
    if($img.Width -lt 64 -or $img.Height -lt 64){throw 'Image too small'}
    $scale=[Math]::Min(1,640.0/[Math]::Max($img.Width,$img.Height))
    $bmp=[System.Drawing.Bitmap]::new([int]($img.Width*$scale),[int]($img.Height*$scale))
    try{$g=[System.Drawing.Graphics]::FromImage($bmp);try{$g.DrawImage($img,0,0,$bmp.Width,$bmp.Height)}finally{$g.Dispose()};$bmp.Save((Join-Path $dest $slug),[System.Drawing.Imaging.ImageFormat]::Png)}finally{$bmp.Dispose()}
   }finally{$img.Dispose()}
   $map[$name]=@{file=$slug;source='https://www.poewiki.net/wiki/File:'+ [uri]::EscapeDataString($file.Replace(' ','_'))}
   break
  }catch{Write-Warning "Candidate unavailable: $file"}finally{Remove-Item $temp -Force -ErrorAction SilentlyContinue}
 }
}
foreach($name in @('Maven','Shaper','Sirus','Exarch','Eater','Cortex','Atziri','Incarnation of Dread','Incarnation of Fear','Incarnation of Neglect','Elder')){if($map.Contains($name)){$map["Uber $name"]=$map[$name]}}
if($map.Contains('Elder')){$map['Uber Uber Elder']=$map['Elder']}
$map | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dest 'manifest.json') -Encoding utf8NoBOM
$missing=@($files.Keys | Where-Object {-not $map.Contains($_)})
Write-Host "Artwork: $($files.Count-$missing.Count)/$($files.Count) base bosses; $($map.Count) total mappings"
if($missing.Count){Write-Warning ("Missing artwork: "+($missing -join ', '))}
if($env:GITHUB_STEP_SUMMARY){Add-Content $env:GITHUB_STEP_SUMMARY ("Boss artwork coverage: "+($files.Count-$missing.Count)+"/"+$files.Count+" base bosses. Missing: "+($missing -join ", "))}
