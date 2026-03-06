' 静默关闭 PromptMaster v6（完全无窗口闪烁）
' 使用方法：双击运行或通过语音命令调用

CreateObject("Wscript.Shell").Run "powershell -WindowStyle Hidden -Command ""Get-Process -Name 'PromptMasterv6' -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() }; Start-Sleep -Seconds 3; Stop-Process -Name 'PromptMasterv6' -Force -ErrorAction SilentlyContinue""", 0, False
