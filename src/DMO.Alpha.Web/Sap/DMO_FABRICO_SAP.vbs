'
' DMO: produções da ZFMD (garrafas por produção) para a pasta do DMO.
'
' Feito a partir da gravação do Diogo (26/10/2026).
'
' Ponha na MESMA pasta do DMO_USO_SAP.vbs (a pasta ligada no DMO).
'
' O que faz:
'   1. Usa o SAP já aberto, ou abre-o pelo «Moldes Existentes.SAP».
'   2. Minimiza o SAP e abre a ZFMD na mesma janela (/nZFMD).
'   3. Centro MG, dos últimos DIAS dias até hoje, todos os materiais, F8.
'   4. Grava a lista em dmo-fabrico-sap.txt, na mesma pasta.
' Da próxima vez que abrir as Ferramentas no DMO, o DMO lê esse ficheiro.
'
' Com /auto (o Agendador de Tarefas) não mostra a mensagem final.
' A senha do SAP fica em SENHA, mais abaixo.

Option Explicit

Const SAP_WAIT_SECONDS = 300
Const SCREEN_WAIT_SECONDS = 120
Const DIAS = {{DIAS}}
Const CENTRO = "MG"
Const GRELHA = "wnd[0]/usr/cntlGRID1/shellcont/shell/shellcont[1]/shell"
Const TITULO = "DMO - produções no SAP"

' A senha do utilizador do «Moldes Existentes.SAP» (Diogo, 28/10/2026: «mete a
' senha no script»). Escreve-se sozinha na janela «Moldes Existentes» quando o
' SAP estiver fechado. Vazia, o SAP pede-a como antes.
Const SENHA = "{{SENHA}}"
Const UTILIZADOR = "{{UTILIZADOR}}"
Const JANELA_LOGON = "Moldes Existentes"

' /auto: sem mensagem final. /inicio:dd.mm.aaaa e /fim:dd.mm.aaaa: as datas
' escolhidas no DMO («Atualizar do SAP» por data). Sem elas, os últimos DIAS dias.
Dim automatico, dataInicio, dataFim, argumento
automatico = False
dataInicio = ""
dataFim = ""
For Each argumento In WScript.Arguments
    If LCase(argumento) = "/auto" Then automatico = True
    If LCase(Left(argumento, 8)) = "/inicio:" Then dataInicio = Mid(argumento, 9)
    If LCase(Left(argumento, 5)) = "/fim:" Then dataFim = Mid(argumento, 6)
Next
If dataInicio = "" Then dataInicio = DataSap(Date - DIAS)
If dataFim = "" Then dataFim = DataSap(Date)

Dim fso, shell, pasta, atalho, saida, temporario
Dim SapGuiAuto, application, session, grid, outFile
Dim startedAt, r, c, colunas, linha, valor, total

Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

pasta = fso.GetParentFolderName(WScript.ScriptFullName)
atalho = fso.BuildPath(pasta, "Moldes Existentes.SAP")
' Nome com a hora desta corrida (SAP-02): um ficheiro novo a cada corrida, nunca
' a substituir o anterior; se a app estiver fechada, fica tudo para importar.
saida = fso.BuildPath(pasta, "dmo-fabrico-sap-" & Carimbo() & ".txt")
temporario = fso.BuildPath(pasta, "dmo-fabrico-sap.a-gravar")

' Uma sessão do SAP: a que já está aberta ou a que o atalho abre.
Set session = PrimeiraSessao()
If session Is Nothing Then
    If Not fso.FileExists(atalho) Then
        Falhar "O SAP não está aberto e falta o «Moldes Existentes.SAP» nesta pasta:" & vbCrLf & pasta
    End If
    AbrirSapLogon
    shell.Run Chr(34) & atalho & Chr(34), 7, False
    startedAt = Timer
    Dim senhaEscrita
    senhaEscrita = (SENHA = "")
    Do
        WScript.Sleep 1000
        ' A janela da senha: escrever a senha e carregar em Logon.
        If Not senhaEscrita Then
            If shell.AppActivate(JANELA_LOGON) Then
                WScript.Sleep 500
                EscreverSenha
                senhaEscrita = True
            End If
        End If
        If TratarLogonMultiplo() Then WScript.Sleep 2000
        Set session = PrimeiraSessao()
        If Not session Is Nothing Then
            ' Pronto: com o campo dos comandos e sem nenhuma janela por cima.
            If Not Campo(session, "wnd[0]/tbar[0]/okcd") Is Nothing And Campo(session, "wnd[1]") Is Nothing Then Exit Do
        End If
        If ElapsedSeconds(startedAt) >= SAP_WAIT_SECONDS Then
            Falhar "O SAP não ficou pronto em " & SAP_WAIT_SECONDS & " segundos."
        End If
    Loop
End If

' Minimizar o SAP (Diogo, 22/10/2026: correr com a página minimizada).
On Error Resume Next
session.findById("wnd[0]").iconify
Err.Clear
On Error GoTo 0

' A ZFMD na mesma janela.
session.findById("wnd[0]/tbar[0]/okcd").text = "/nzfmd"
session.findById("wnd[0]").sendVKey 0
' O passo da gravação do Diogo logo a seguir a abrir a ZFMD (um menu), que leva ao
' ecrã da seleção. Só se o ecrã da seleção ainda não estiver aberto.
If EsperarCampo(session, "wnd[0]/usr/ctxtCENTRO-LOW", 5) Is Nothing Then
    On Error Resume Next
    session.findById("wnd[0]/mbar/menu[1]/menu[11]/menu[0]").select
    Err.Clear
    On Error GoTo 0
End If
If EsperarCampo(session, "wnd[0]/usr/ctxtCENTRO-LOW", 30) Is Nothing Then
    Falhar "O ecrã da ZFMD não abriu."
End If

session.findById("wnd[0]/usr/ctxtCENTRO-LOW").text = CENTRO
session.findById("wnd[0]/usr/ctxtDATA-LOW").text = dataInicio
session.findById("wnd[0]/usr/ctxtDATA-HIGH").text = dataFim
session.findById("wnd[0]/usr/ctxtMAT-LOW").text = ""
session.findById("wnd[0]").sendVKey 0
session.findById("wnd[0]").sendVKey 8

Set grid = EsperarCampo(session, GRELHA, SCREEN_WAIT_SECONDS)
If grid Is Nothing Then
    Falhar "A lista da ZFMD não apareceu (sem produções nas datas, ou o ecrã mudou)."
End If

' Grava primeiro num ficheiro à parte: o DMO só vê a lista quando estiver completa.
' Separado por tabulações, com os títulos das colunas como no SAP.
Set colunas = grid.ColumnOrder
Dim texto
texto = ""
linha = ""
For c = 0 To colunas.Count - 1
    If c > 0 Then linha = linha & vbTab
    linha = linha & Limpo(grid.GetDisplayedColumnTitle(colunas(c)))
Next
texto = texto & linha & vbCrLf

total = grid.RowCount
For r = 0 To total - 1
    ' A grelha só carrega as linhas que se veem: andar com ela.
    If r Mod 20 = 0 Then
        On Error Resume Next
        grid.firstVisibleRow = r
        Err.Clear
        On Error GoTo 0
    End If
    linha = ""
    For c = 0 To colunas.Count - 1
        valor = ""
        On Error Resume Next
        valor = grid.GetCellValue(r, colunas(c))
        Err.Clear
        On Error GoTo 0
        If c > 0 Then linha = linha & vbTab
        linha = linha & Limpo(valor)
    Next
    texto = texto & linha & vbCrLf
Next
' Em UTF-8: os títulos do SAP têm símbolos que o ficheiro normal não aceita.
Dim stream
Set stream = CreateObject("ADODB.Stream")
stream.Type = 2
stream.Charset = "utf-8"
stream.Open
stream.WriteText texto
On Error Resume Next
If fso.FileExists(temporario) Then fso.DeleteFile temporario, True
Err.Clear
On Error GoTo 0
stream.SaveToFile temporario, 2
stream.Close
' «saida» tem a hora desta corrida: nunca existe já, por isso não se apaga
' nada antes do MoveFile (SAP-02: apagar antes de mover perdia o ficheiro
' anterior se o MoveFile falhasse a seguir).
fso.MoveFile temporario, saida

' Voltar ao menu do SAP.
On Error Resume Next
session.findById("wnd[0]/tbar[0]/okcd").text = "/n"
session.findById("wnd[0]").sendVKey 0
Err.Clear
On Error GoTo 0

If Not automatico Then
    MsgBox "Feito: " & total & " produções gravadas." & vbCrLf & _
           "Abra as Ferramentas no DMO para as ler.", 64, TITULO
End If

Function PrimeiraSessao()
    Dim cn, s
    Set PrimeiraSessao = Nothing
    On Error Resume Next
    Set SapGuiAuto = GetObject("SAPGUI")
    If Err.Number = 0 And Not SapGuiAuto Is Nothing Then
        Set application = SapGuiAuto.GetScriptingEngine
        For Each cn In application.Children
            For Each s In cn.Children
                Set PrimeiraSessao = s
                Err.Clear
                On Error GoTo 0
                Exit Function
            Next
        Next
    End If
    Err.Clear
    On Error GoTo 0
End Function

Function Campo(ByVal sapSession, ByVal id)
    Set Campo = Nothing
    On Error Resume Next
    Set Campo = sapSession.findById(id)
    Err.Clear
    On Error GoTo 0
End Function

Function EsperarCampo(ByVal sapSession, ByVal id, ByVal segundos)
    Dim inicio
    inicio = Timer
    Do
        Set EsperarCampo = Campo(sapSession, id)
        If Not EsperarCampo Is Nothing Then Exit Function
        If ElapsedSeconds(inicio) >= segundos Then Exit Function
        WScript.Sleep 500
    Loop
End Function

Function DataSap(ByVal d)
    DataSap = Right("0" & Day(d), 2) & "." & Right("0" & Month(d), 2) & "." & Year(d)
End Function

' AAAAMMDD-HHMM desta corrida, para o nome do ficheiro de saída (SAP-02).
Function Carimbo()
    Carimbo = Year(Now) & Right("0" & Month(Now), 2) & Right("0" & Day(Now), 2) & "-" & _
              Right("0" & Hour(Now), 2) & Right("0" & Minute(Now), 2)
End Function

Function Limpo(ByVal texto)
    Limpo = Trim(Replace(Replace(Replace(CStr(texto), vbTab, " "), vbCr, " "), vbLf, " "))
End Function

Sub Falhar(ByVal mensagem)
    If Not automatico Then MsgBox mensagem, 16, TITULO
    WScript.Quit 1
End Sub

' O ecrã do «logon múltiplo» (Diogo, 28/10/2026: o mesmo utilizador já
' tinha entrado noutro sítio e o script seguia por cima dele, «Função
' impossível»): escolhe «Continuar com este logon sem terminar os outros»
' e confirma, para não fechar o SAP de quem o está a usar.
Function TratarLogonMultiplo()
    Dim gui, motor, c, s, opcao
    TratarLogonMultiplo = False
    On Error Resume Next
    Set gui = GetObject("SAPGUI")
    If Err.Number <> 0 Or gui Is Nothing Then Err.Clear: On Error GoTo 0: Exit Function
    Set motor = gui.GetScriptingEngine
    For Each c In motor.Children
        For Each s In c.Children
            Set opcao = Nothing
            Set opcao = s.findById("wnd[1]/usr/radMULTI_LOGON_OPT2")
            Err.Clear
            If Not opcao Is Nothing Then
                opcao.select
                s.findById("wnd[1]").sendVKey 0
                Err.Clear
                Registo "logon múltiplo: continuar sem terminar os outros"
                TratarLogonMultiplo = True
            End If
        Next
    Next
    Err.Clear
    On Error GoTo 0
End Function

' Um registo do que o script fez, para se perceber onde parou
' (dmo-sap.log, na mesma pasta; o DMO não lê .log).
Sub Registo(ByVal texto)
    Dim f
    On Error Resume Next
    Set f = fso.OpenTextFile(fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "dmo-sap.log"), 8, True)
    f.WriteLine Now & "  " & WScript.ScriptName & "  " & texto
    f.Close
    Err.Clear
    On Error GoTo 0
End Sub

' O «Moldes Existentes.SAP» só entra sem pedir a senha com o SAP Logon já
' aberto (Diogo, 28/10/2026: «isto supostamente deveria abrir logo, sem pedir
' pass»). Sem ele aberto, abre-o minimizado e espera que fique pronto.
Sub AbrirSapLogon()
    Dim exe, inicio, motor
    If SapLogonAberto() Then Registo "SAP Logon já aberto": Exit Sub
    exe = shell.ExpandEnvironmentStrings("%ProgramFiles(x86)%") & "\SAP\FrontEnd\SAPgui\saplogon.exe"
    If Not fso.FileExists(exe) Then exe = shell.ExpandEnvironmentStrings("%ProgramFiles%") & "\SAP\FrontEnd\SAPgui\saplogon.exe"
    If Not fso.FileExists(exe) Then Registo "saplogon.exe não encontrado": Exit Sub
    Registo "a abrir o SAP Logon"
    shell.Run Chr(34) & exe & Chr(34), 7, False
    inicio = Timer
    Do
        WScript.Sleep 1000
        If SapLogonAberto() Then
            Registo "SAP Logon pronto"
            ' Um momento para o SAP Logon acabar de arrancar.
            WScript.Sleep 3000
            Exit Sub
        End If
    Loop While ElapsedSeconds(inicio) < 60
    Registo "SAP Logon não ficou pronto em 60 s"
End Sub

Function SapLogonAberto()
    Dim gui
    SapLogonAberto = False
    On Error Resume Next
    Set gui = GetObject("SAPGUI")
    SapLogonAberto = (Err.Number = 0 And Not gui Is Nothing)
    Err.Clear
    On Error GoTo 0
End Function

' Entrar no SAP pela janela «Moldes Existentes» (Diogo, 28/10/2026: o
' teclado escrevia a senha onde estivesse o cursor, às vezes na caixa do
' nome, e em maiúsculas com o Caps Lock). Pelo Windows (UI Automation)
' põe-se o nome na caixa do nome e a senha na caixa da senha, esteja o
' cursor onde estiver, e carrega-se em Logon. O ficheiro temporário com a
' senha apaga-se logo a seguir. Se o Windows não chegar às caixas, escreve
' a senha como antes.
Sub EscreverSenha()
    Dim ps, f, codigo
    ps = fso.BuildPath(shell.ExpandEnvironmentStrings("%TEMP%"), "dmo-sap-logon.ps1")
    Set f = fso.CreateTextFile(ps, True)
    f.WriteLine "$ErrorActionPreference = 'Stop'"
    f.WriteLine "Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms"
    f.WriteLine "$A = [System.Windows.Automation.AutomationElement]"
    f.WriteLine "$T = [System.Windows.Automation.TreeScope]"
    f.WriteLine "$K = [System.Windows.Forms.SendKeys]"
    f.WriteLine "$w = $A::RootElement.FindFirst($T::Children, (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, '" & ParaPs(JANELA_LOGON) & "')))"
    f.WriteLine "if (-not $w) { $w = $A::RootElement.FindFirst($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, '" & ParaPs(JANELA_LOGON) & "'))) }"
    f.WriteLine "if (-not $w) { exit 2 }"
    f.WriteLine "$caixas = $w.FindAll($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))"
    f.WriteLine "$nome = $null; $senha = $null"
    f.WriteLine "foreach ($c in $caixas) { if ($c.Current.IsPassword) { $senha = $c } elseif (-not $nome) { $nome = $c } }"
    f.WriteLine "if (-not $nome -or -not $senha) { exit 3 }"
    f.WriteLine "$caps = [Console]::CapsLock"
    f.WriteLine "if ($caps) { $K::SendWait('{CAPSLOCK}') }"
    f.WriteLine "try {"
    f.WriteLine "  try { $nome.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('" & ParaPs(UTILIZADOR) & "') }"
    f.WriteLine "  catch { $nome.SetFocus(); Start-Sleep -Milliseconds 200; $K::SendWait('^a'); $K::SendWait('" & ParaPs(ParaSendKeys(UTILIZADOR)) & "') }"
    f.WriteLine "  $senha.SetFocus(); Start-Sleep -Milliseconds 200"
    f.WriteLine "  $K::SendWait('^a'); $K::SendWait('" & ParaPs(ParaSendKeys(SENHA)) & "'); $K::SendWait('{ENTER}')"
    f.WriteLine "} finally { if ($caps) { $K::SendWait('{CAPSLOCK}') } }"
    f.WriteLine "exit 0"
    f.Close
    Registo "janela da senha encontrada; a preencher"
    codigo = shell.Run("powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & ps & """", 0, True)
    On Error Resume Next
    fso.DeleteFile ps, True
    Err.Clear
    On Error GoTo 0
    Registo "preencher pelo Windows: código " & codigo & " (0 = feito)"
    ' Sem o Windows: pelo teclado. A janela abre com o cursor na caixa do nome
    ' (Diogo, 28/10/2026: a senha ia parar lá): o nome por cima do que lá
    ' estiver, Tab, a senha e Enter.
    If codigo <> 0 Then
        shell.AppActivate JANELA_LOGON
        WScript.Sleep 300
        shell.SendKeys "^a" & ParaSendKeys(UTILIZADOR) & "{TAB}" & ParaSendKeys(SENHA) & "{ENTER}"
    End If
End Sub

' Um texto entre plicas no PowerShell: a plica dobra-se.
Function ParaPs(ByVal texto)
    ParaPs = Replace(texto, "'", "''")
End Function

' O SendKeys trata + ^ % ~ ( ) { } [ ] como teclas especiais.
Function ParaSendKeys(ByVal texto)
    Dim i, ch
    ParaSendKeys = ""
    For i = 1 To Len(texto)
        ch = Mid(texto, i, 1)
        If InStr("+^%~(){}[]", ch) > 0 Then ch = "{" & ch & "}"
        ParaSendKeys = ParaSendKeys & ch
    Next
End Function

Function ElapsedSeconds(ByVal startTime)
    Dim t
    t = Timer - startTime
    If t < 0 Then t = t + 86400
    ElapsedSeconds = t
End Function
