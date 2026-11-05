param(
    [string]$ApplicationPath,
    [string[]]$ShortcutDirectory,
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sourceRoot = Split-Path -Parent $PSScriptRoot
$sourceCheckout = (Test-Path -LiteralPath (Join-Path $sourceRoot 'Directory.Build.props')) -and
    (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'common.ps1'))
if (-not $ApplicationPath) {
    if ($sourceCheckout) {
        . (Join-Path $PSScriptRoot 'common.ps1')
        $identity = Get-ApplicationIdentity
        $ApplicationPath = Join-Path $sourceRoot $identity.executable
    } else {
        # Release ZIP: this script and the executable are next to each other.
        $ApplicationPath = Join-Path $PSScriptRoot 'NinjaTraderAutoLogin.exe'
    }
}
$ApplicationPath = [IO.Path]::GetFullPath($ApplicationPath)
if ([IO.Path]::GetFileName($ApplicationPath) -ine 'NinjaTraderAutoLogin.exe') { throw 'Unexpected helper executable name.' }
if (-not $Remove -and -not (Test-Path -LiteralPath $ApplicationPath -PathType Leaf)) { throw 'Application executable not found.' }
$applicationFolder = Split-Path -Parent $ApplicationPath
if (-not $ShortcutDirectory) {
    $localFolder = if ($sourceCheckout) { $sourceRoot } else { $applicationFolder }
    $ShortcutDirectory = @($localFolder, [Environment]::GetFolderPath('DesktopDirectory'))
}
$entries = @(
    @{ Name = 'Auto Login for NinjaTrader.lnk'; Argument = '--launch'; Kind = 'Launch' },
    @{ Name = 'Auto Login for NinjaTrader Settings.lnk'; Argument = '--settings'; Kind = 'Settings' }
)
# The product was previously named NinjaTrader Auto Login.
$previousEntries = @(
    @{ Name = 'NinjaTrader Auto Login.lnk'; Argument = '--launch'; Kind = 'Launch' },
    @{ Name = 'NinjaTrader Login Settings.lnk'; Argument = '--settings'; Kind = 'Settings' }
)
function Test-OwnedShortcut($Shortcut, $Entry) {
    $knownDescription = $Shortcut.Description -eq 'NinjaTrader Auto Login' -or
        $Shortcut.Description -eq ('NinjaTrader Auto Login | ' + $Entry.Kind) -or
        $Shortcut.Description -eq ('Auto Login for NinjaTrader | ' + $Entry.Kind)
    return $knownDescription -and [IO.Path]::GetFileName($Shortcut.TargetPath) -ieq 'NinjaTraderAutoLogin.exe' -and
        $Shortcut.Arguments -eq $Entry.Argument
}
if (-not ('NinjaTraderAutoLogin.Shortcuts.ShortcutFile' -as [type])) {
    # Use the Unicode Shell API directly; WScript.Shell can lose Persian filename characters.
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace NinjaTraderAutoLogin.Shortcuts
{
    public sealed class ShortcutDetails
    {
        public string TargetPath = "", Arguments = "", WorkingDirectory = "", Description = "", IconPath = "";
        public int IconIndex;
    }
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink { }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count, IntPtr findData, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetHotkey(out short value);
        void SetHotkey(short value);
        void GetShowCmd(out int value);
        void SetShowCmd(int value);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string value, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string value, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string value);
    }
    public static class ShortcutFile
    {
        public static ShortcutDetails Read(string path)
        {
            var details = new ShortcutDetails();
            if (!File.Exists(path)) { return details; }
            object instance = new ShellLink();
            try
            {
                ((IPersistFile)instance).Load(path, 0);
                var link = (IShellLinkW)instance;
                var buffer = new StringBuilder(32768);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 4); details.TargetPath = buffer.ToString(); buffer.Clear();
                link.GetArguments(buffer, buffer.Capacity); details.Arguments = buffer.ToString(); buffer.Clear();
                link.GetWorkingDirectory(buffer, buffer.Capacity); details.WorkingDirectory = buffer.ToString(); buffer.Clear();
                link.GetDescription(buffer, buffer.Capacity); details.Description = buffer.ToString(); buffer.Clear();
                link.GetIconLocation(buffer, buffer.Capacity, out details.IconIndex); details.IconPath = buffer.ToString();
                return details;
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }
        public static void Write(string path, ShortcutDetails details)
        {
            object instance = new ShellLink();
            try
            {
                var link = (IShellLinkW)instance;
                link.SetPath(details.TargetPath);
                link.SetArguments(details.Arguments);
                link.SetWorkingDirectory(details.WorkingDirectory);
                link.SetDescription(details.Description);
                link.SetIconLocation(details.IconPath, details.IconIndex);
                ((IPersistFile)instance).Save(path, true);
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }
    }
}
'@
}
$prepared = @()
# Validate all destinations first, so an unrelated shortcut is never overwritten or removed.
foreach ($location in @($ShortcutDirectory | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $location -PathType Container)) { throw 'Shortcut destination directory not found.' }
    foreach ($entry in $entries) {
        $path = Join-Path $location $entry.Name
        $shortcut = [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Read($path)
        $prepared += @{ Path = $path; Shortcut = $shortcut; Entry = $entry }
        if (Test-Path -LiteralPath $path) {
            if (-not (Test-OwnedShortcut $shortcut $entry)) { throw 'An existing shortcut belongs to another application; preserve it.' }
        }
    }
}
foreach ($item in $prepared) {
    if ($Remove) {
        if (Test-Path -LiteralPath $item.Path) { Remove-Item -LiteralPath $item.Path }
        continue
    }
    $shortcut = $item.Shortcut
    $shortcut.TargetPath = $ApplicationPath
    $shortcut.Arguments = $item.Entry.Argument
    $shortcut.WorkingDirectory = $applicationFolder
    $shortcut.Description = 'Auto Login for NinjaTrader | ' + $item.Entry.Kind
    $icon = Join-Path $applicationFolder 'assets\settings.ico'
    $shortcut.IconPath = if ($item.Entry.Kind -eq 'Settings' -and (Test-Path -LiteralPath $icon -PathType Leaf)) { $icon } else { $ApplicationPath }
    $shortcut.IconIndex = 0
    [NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Write($item.Path, $shortcut)
}
foreach ($location in @($ShortcutDirectory | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object -Unique)) {
    foreach ($entry in $previousEntries) {
        $path = Join-Path $location $entry.Name
        if ((Test-Path -LiteralPath $path) -and (Test-OwnedShortcut ([NinjaTraderAutoLogin.Shortcuts.ShortcutFile]::Read($path)) $entry)) {
            Remove-Item -LiteralPath $path
        }
    }
}
Write-Output 'Shortcuts updated. No application was started.'
