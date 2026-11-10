param([string]$Name)
# Saves ONLY a Gemini download. Finds the Save dialog with Win32 (title starts with "blob:"), then uses UI Automation
# on that window handle: acts only if its file-name box contains "Gemini_Generated_Image". No keystrokes are sent.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text; using System.Collections.Generic;
public class WinFind {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr p);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  public static List<IntPtr> ByPrefix(string prefix) {
    var r = new List<IntPtr>();
    EnumWindows((h, p) => { if (IsWindowVisible(h)) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); if (sb.ToString().StartsWith(prefix)) r.Add(h); } return true; }, IntPtr.Zero);
    return r;
  }
}
"@
$A = [System.Windows.Automation.AutomationElement]; $T = [System.Windows.Automation.TreeScope]; $C = [System.Windows.Automation.ControlType]
function Cond($type) { New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, $type) }
$raw = Join-Path $PSScriptRoot "gemini\raw"; New-Item -ItemType Directory -Force $raw | Out-Null
$target = Join-Path $raw "$Name.png"
$dl = Join-Path $env:USERPROFILE "Downloads\bepjam_$Name.png"
for ($i = 0; $i -lt 150; $i++) {
  foreach ($h in [WinFind]::ByPrefix("blob:")) {
    try { $w = $A::FromHandle($h) } catch { continue }
    $box = $null
    foreach ($e in $w.FindAll($T::Descendants, (Cond $C::Edit))) {
      try { $v = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } catch { $v = "" }
      if ($v -like "Gemini_Generated_Image*") { $box = $e; break }
    }
    if (-not $box) { continue }
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("bepjam_$Name.png")
    Start-Sleep -Milliseconds 200
    foreach ($b in $w.FindAll($T::Descendants, (Cond $C::Button))) {
      if ($b.Current.AutomationId -eq "1") { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break }
    }
    for ($j = 0; $j -lt 30; $j++) { if (Test-Path $dl) { Start-Sleep 1; Move-Item -Force $dl $target; "saved $target"; exit 0 }; Start-Sleep -Milliseconds 500 }
    "save invoked but file not found"; exit 1
  }
  Start-Sleep -Milliseconds 400
}
"no Gemini dialog"; exit 1
