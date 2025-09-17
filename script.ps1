$path = 'src/LM.HubAndSpoke/Entries/HubSpokeStore.cs'
$lines = Get-Content $path
$startLine = (Select-String -Path $path -Pattern '^\s*public async Task SaveAsync').LineNumber
$endLine = (Select-String -Path $path -Pattern '^\s*public async Task<Entry\?> FindByIdsAsync').LineNumber
"start=$startLine end=$endLine"
