$exePath = "C:\Users\QNB\Documents\Default Project\mackeys\mackeysremap\MacKeysRemapGui\publish\MacKeysRemapGui.exe"
$action = New-ScheduledTaskAction -Execute $exePath -Argument "/tray"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 0)
Register-ScheduledTask -TaskName "MacKeysRemap" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -Force
Write-Host "Task created successfully!"
