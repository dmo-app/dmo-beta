@echo off
rem DMO: liga este PC ao botao "Atualizar do SAP" do DMO. Duplo clique uma vez.
rem 1. O endereco dmo-sap: passa a abrir o DMO_SAP_DIARIO.vbs desta pasta.
rem 2. A tarefa "DMO SAP" corre uma vez por dia, a hora marcada no DMO
rem    (Admin > Definicoes > SAP); com o PC desligado, corre quando ligar.
reg add "HKCU\Software\Classes\dmo-sap" /ve /d "URL:DMO SAP" /f >nul
reg add "HKCU\Software\Classes\dmo-sap" /v "URL Protocol" /d "" /f >nul
reg add "HKCU\Software\Classes\dmo-sap\shell\open\command" /ve /d "wscript.exe \"%~dp0DMO_SAP_DIARIO.vbs\" \"%%1\"" /f >nul
if errorlevel 1 goto erro
schtasks /Delete /F /TN "DMO uso SAP" >nul 2>&1
schtasks /Delete /F /TN "DMO producoes SAP" >nul 2>&1
set H=0{{HORA}}
set H=%H:~-2%
schtasks /Create /F /SC DAILY /ST %H%:00 /TN "DMO SAP" /TR "wscript.exe \"%~dp0DMO_SAP_DIARIO.vbs\" /agendado"
if errorlevel 1 goto erro
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 2); Set-ScheduledTask -TaskName 'DMO SAP' -Settings $s | Out-Null"
echo Feito. O botao "Atualizar do SAP" do DMO ja funciona neste PC.
echo A leitura do dia corre sozinha a hora marcada no DMO.
pause
exit /b 0
:erro
echo Nao foi possivel instalar. Fale com o DMO.
pause
