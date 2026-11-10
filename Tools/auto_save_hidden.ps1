param([string]$Name, [int]$TimeoutSec = 120)
# Background-only save for a Gemini download dialog: never takes focus, never sends keystrokes.
# Finds the "blob:" Save dialog (#32770), sets the file-name Edit via WM_SETTEXT and clicks Save via WM_COMMAND(IDOK).
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public class BG {
  public delegate bool EP(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(EP f, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr w, EP f, IntPtr p);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, string l);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  public static IntPtr Dialog() { IntPtr f = IntPtr.Zero;
    EnumWindows((h, p) => { if (!IsWindowVisible(h)) return true; var c = new StringBuilder(64); GetClassName(h, c, 64);
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      if (c.ToString() == "#32770" && t.ToString().StartsWith("blob:")) { SetWindowPos(h,IntPtr.Zero,-32000,-32000,0,0,0x15); f = h; return false; } return true; }, IntPtr.Zero); return f; }
  public static IntPtr NameEdit(IntPtr dlg) { IntPtr f = IntPtr.Zero;
    EnumChildWindows(dlg, (h, p) => { var c = new StringBuilder(64); GetClassName(h, c, 64);
      if (c.ToString() == "Edit" && f == IntPtr.Zero) { f = h; return false; } return true; }, IntPtr.Zero); return f; }
}
"@
$dl = Join-Path $env:USERPROFILE "Downloads"
$raw = Join-Path $PSScriptRoot "gemini\raw"; New-Item -ItemType Directory -Force $raw | Out-Null
$file = "bepjam_$Name.png"; $dlPath = Join-Path $dl $file
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
  if (Test-Path $dlPath) { Start-Sleep -Milliseconds 800; Move-Item -Force $dlPath (Join-Path $raw "$Name.png"); "saved $Name"; exit 0 }
  $d = [BG]::Dialog()
  if ($d -ne [IntPtr]::Zero) {
    $e = [BG]::NameEdit($d)
    if ($e -ne [IntPtr]::Zero) {
      [BG]::SendMessage($e, 0x000C, [IntPtr]::Zero, $file) | Out-Null      # WM_SETTEXT
      Start-Sleep -Milliseconds 200
      [BG]::PostMessage($d, 0x0111, [IntPtr]1, [IntPtr]::Zero) | Out-Null  # WM_COMMAND IDOK
      Start-Sleep -Milliseconds 1500
    }
  }
  Start-Sleep -Milliseconds 30
}
"timeout"; exit 1
