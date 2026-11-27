# Fetch only static, explicitly mapped game artwork. No trade or economy API calls.
$ErrorActionPreference='Stop'
$dest=Join-Path $PSScriptRoot '../ProjectPoeLedger/Web/boss-art'
New-Item -ItemType Directory -Force $dest | Out-Null
$files=@{
 'Atziri'='Atziri, Queen of the Vaal monster screenshot.jpg'
 'Aul, the Crystal King'='Aul, the Crystal King.jpg'
 'Ahuatotli, the Blind'='Ahuatotli, the Blind.png'
 'Saresh, of the Weeping Black'='Saresh, of the Weeping Black.png'
 'Exarch'='The Searing Exarch.png'
 'Incarnation of Neglect'='Incarnation of Neglect.png'
}
$map=[ordered]@{}
Add-Type -AssemblyName System.Drawing
foreach($name in $files.Keys){
 $url='https://www.poewiki.net/wiki/Special:FilePath/'+[uri]::EscapeDataString($files[$name].Replace(' ','_'))
 $temp=Join-Path $dest ([guid]::NewGuid().ToString()+'.tmp')
 $slug=($name.ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')+'.png'
 try{
  Invoke-WebRequest -Uri $url -OutFile $temp -TimeoutSec 25 -UserAgent 'PoeLedger/0.46 (static game artwork)' 
  $img=[System.Drawing.Image]::FromFile($temp)
  try{ $img.Save((Join-Path $dest $slug),[System.Drawing.Imaging.ImageFormat]::Png) }finally{$img.Dispose()}
  $map[$name]=@{file=$slug;source='https://www.poewiki.net/wiki/File:'+ [uri]::EscapeDataString($files[$name].Replace(' ','_'))}
 }catch{Write-Warning "Artwork unavailable for ${name}: $_"}finally{Remove-Item $temp -Force -ErrorAction SilentlyContinue}
}
$map | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dest 'manifest.json') -Encoding utf8NoBOM
Write-Host "Fetched $($map.Count) of $($files.Count) source images."
