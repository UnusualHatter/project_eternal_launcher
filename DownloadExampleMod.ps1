$modId = 654753
$api = "https://api.gamebanana.com/Core/Item/Data?itemtype=Mod&itemid=$modId&fields=name,Game().name,Files().aFiles()"

$data = Invoke-RestMethod -Uri $api
$payload = $null

if ($data -is [System.Array]) {
	$payload = $data
} elseif ($null -ne $data -and $data.PSObject.Properties.Name -contains 'value' -and $null -ne $data.value) {
	$payload = @($data.value)
}

if ($null -eq $payload -or $payload.Count -lt 3) {
	throw "Resposta da API em formato inesperado."
}

$modName = $payload[0]
$gameName = $payload[1]
$filesObj = $payload[2]

# Pega o primeiro arquivo disponível
$file = $null

if ($filesObj -is [System.Collections.IDictionary]) {
	$firstKey = $filesObj.Keys | Select-Object -First 1
	if ($null -ne $firstKey) {
		$file = $filesObj[$firstKey]
	}
} else {
	$fileEntry = $filesObj.PSObject.Properties | Select-Object -First 1
	if ($null -ne $fileEntry) {
		$file = $fileEntry.Value
	}
}   

if ($null -eq $file -or [string]::IsNullOrWhiteSpace($file._sDownloadUrl)) {
	throw "Nenhum arquivo válido encontrado para download."
}

$downloadUrl = $file._sDownloadUrl
$outFile = Join-Path $PWD $file._sFile

Invoke-WebRequest -Uri $downloadUrl -OutFile $outFile
Write-Host "Baixado: $modName ($gameName) -> $outFile"