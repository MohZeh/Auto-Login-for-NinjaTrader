function Assert-PublicationContent([byte[]]$Bytes, [string]$Name, [string]$SourceRoot) {
    $hostPattern = '(?i)([A-Z]:[\\/]{1,2}(Users|Documents and Settings)[\\/]{1,2}|/(Users|home)/[^/\s]+/|clipboard-[0-9a-f]{8}-)'
    $secretPattern = '(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN [A-Z ]*PRIVATE KEY-----)'
    $privateRoots = @($SourceRoot, [Environment]::GetFolderPath('UserProfile')) | Where-Object { -not [String]::IsNullOrWhiteSpace($_) }
    foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode, [Text.Encoding]::BigEndianUnicode)) {
        $text = $encoding.GetString($Bytes)
        if ($text -match $hostPattern) { throw ('Host-specific content: ' + $Name) }
        if ($text -match $secretPattern) { throw ('Possible secret pattern: ' + $Name) }
        foreach ($root in $privateRoots) {
            foreach ($variant in @($root, $root.Replace('\', '/'), $root.Replace('\', '\\'))) {
                if ($text.IndexOf($variant, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw ('Host-specific content: ' + $Name) }
            }
        }
    }
}

function Assert-PublicationArchive([string]$ArchivePath, [string[]]$ExpectedEntries, [string]$SourceRoot) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        if (@($names | Sort-Object -Unique).Count -ne $names.Count -or
            @(Compare-Object ($ExpectedEntries | Sort-Object) ($names | Sort-Object)).Count -ne 0) { throw 'Archive contains unexpected or missing entries.' }
        foreach ($entry in $zip.Entries) {
            $stream = $entry.Open()
            $memory = New-Object IO.MemoryStream
            try {
                $stream.CopyTo($memory)
                Assert-PublicationContent -Bytes $memory.ToArray() -Name $entry.FullName -SourceRoot $SourceRoot
            } finally { $stream.Dispose(); $memory.Dispose() }
        }
    } finally { $zip.Dispose() }
}
