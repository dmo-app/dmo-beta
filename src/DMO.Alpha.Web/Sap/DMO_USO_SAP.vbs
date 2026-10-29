'
' DMO: % de uso das ferramentas a partir do SAP (transação ZPPG)
'
' Feito a partir do RUN_ZPPG_PERCENTAGES_AUTO.vbs do Diogo.
'
' Ponha na MESMA pasta (a pasta ligada no DMO, em Ferramentas > Definições >
' Utilização pelo SAP):
'   DMO_USO_SAP.vbs          (este ficheiro)
'   Moldes Existentes.SAP    (o atalho do SAP para a ZPPG)
'   dmo-codigos.txt          (o DMO escreve-o sozinho: os códigos a procurar)
'
' O que faz:
'   1. Lê os códigos do dmo-codigos.txt (nome técnico + lote, um por linha).
'   2. Abre o atalho do SAP e espera pelo ecrã da ZPPG.
'   3. Procura cada código e lê o PERC da lista.
'   4. Grava tudo em dmo-uso-sap.csv, na mesma pasta.
' Da próxima vez que abrir as Ferramentas no DMO, o DMO lê esse ficheiro.
'
' A senha do SAP fica em SENHA, mais abaixo.

Option Explicit

Const SAP_WAIT_SECONDS = 300
Const SCREEN_WAIT_SECONDS = 30
Const TITULO = "DMO - uso no SAP"
Const CENTRO = "MG"

' Diogo, 28/10/2026: % uso em todas as ferramentas que existem no DMO.
' Faz uma só procura na ZPPG com «*» (todos os moldes do SAP); o DMO, no
' «Ler pasta», grava só nas ferramentas que tem (pelo nome técnico e lote)
' e ignora as outras. Os códigos do dmo-codigos.txt continuam a ser pedidos.
Const PEDIR_TODAS = False
Const LISTA_TODAS_WAIT_SECONDS = 600

' A senha do utilizador do «Moldes Existentes.SAP» (Diogo, 28/10/2026: «mete a
' senha no script»). Escreve-se sozinha na janela «Moldes Existentes» quando o
' SAP estiver fechado. Vazia, o SAP pede-a como antes.
Const SENHA = "{{SENHA}}"
Const UTILIZADOR = "{{UTILIZADOR}}"
Const JANELA_LOGON = "Moldes Existentes"

' Sem senha do SAP o script pára logo (Diogo, 29/10/2026: «prendeu na pass»,
' o PC bloqueou): nunca fica à espera da janela da senha.
If SENHA = "" Or InStr(SENHA, Chr(123) & Chr(123)) > 0 Then
    CreateObject("WScript.Shell").Popup "Falta a senha do SAP. Grave-a no DMO em Admin › SAP e carregue outra vez em «Instalar».", 30, TITULO, 48
    WScript.Quit 1
End If

Dim fso, shell, pasta, atalho, ficheiroCodigos, saida, temporario
Dim SapGuiAuto, application, connection, session
Dim searchField, grid, outFile
Dim codigo, linha, i, r, startedAt, encontrados
Dim equipment, percentUse, lastProduction, batchDesc, statusText, errorText, consumo, vida, pecas, colConsumo, colVida, colPecas

' Com /auto (o Agendador de Tarefas, ao fim do dia) não mostra a mensagem final.
' /codigos:<ficheiro>: os códigos de outro ficheiro desta pasta (o «Atualizar do
' SAP» por referência escreve-os no dmo-codigos-pedido.txt).
Dim automatico, outroFicheiro, argumento
automatico = False
outroFicheiro = ""
For Each argumento In WScript.Arguments
    If LCase(argumento) = "/auto" Then automatico = True
    If LCase(Left(argumento, 9)) = "/codigos:" Then outroFicheiro = Mid(argumento, 10)
Next

Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

pasta = fso.GetParentFolderName(WScript.ScriptFullName)
atalho = fso.BuildPath(pasta, "Moldes Existentes.SAP")
ficheiroCodigos = fso.BuildPath(pasta, "dmo-codigos.txt")
If outroFicheiro <> "" Then ficheiroCodigos = fso.BuildPath(pasta, fso.GetFileName(outroFicheiro))
' Nome com a hora desta corrida (SAP-02): um ficheiro novo a cada corrida, nunca
' a substituir o anterior; se a app estiver fechada, fica tudo para importar.
saida = fso.BuildPath(pasta, "dmo-uso-sap-" & Carimbo() & ".csv")
temporario = fso.BuildPath(pasta, "dmo-uso-sap.a-gravar")

If Not fso.FileExists(atalho) Then
    MsgBox "Falta o «Moldes Existentes.SAP» nesta pasta:" & vbCrLf & pasta, 16, TITULO
    WScript.Quit 1
End If

If Not fso.FileExists(ficheiroCodigos) And Not PEDIR_TODAS Then
    MsgBox "Falta o dmo-codigos.txt nesta pasta." & vbCrLf & _
           "Abra no DMO: Ferramentas > Definições > Utilização pelo SAP > Ler agora.", 16, TITULO
    WScript.Quit 1
End If

' Os códigos: um por linha, sem linhas vazias.
Dim total, codigos()
total = 0
ReDim codigos(0)
If fso.FileExists(ficheiroCodigos) Then
With fso.OpenTextFile(ficheiroCodigos, 1)
    Do While Not .AtEndOfStream
        linha = Trim(.ReadLine)
        If Len(linha) > 0 Then
            ReDim Preserve codigos(total)
            codigos(total) = linha
            total = total + 1
        End If
    Loop
    .Close
End With
End If

If PEDIR_TODAS Then
    ReDim Preserve codigos(total)
    codigos(total) = "*"
    total = total + 1
End If

If total = 0 Then
    If Not automatico Then MsgBox "O dmo-codigos.txt está vazio: nenhuma ferramenta do DMO está à espera da utilização.", 48, TITULO
    WScript.Quit 1
End If

' O SAP já tinha sessão aberta antes do atalho?
Dim sapAberto
sapAberto = False
On Error Resume Next
Set SapGuiAuto = GetObject("SAPGUI")
If Err.Number = 0 And Not SapGuiAuto Is Nothing Then
    Set application = SapGuiAuto.GetScriptingEngine
    If Not application Is Nothing Then sapAberto = (application.Children.Count > 0)
End If
Err.Clear
On Error GoTo 0

' Abrir o atalho do SAP.
' 7 = abre minimizado, sem tirar o foco ao que se está a fazer.
AbrirSapLogon
shell.Run Chr(34) & atalho & Chr(34), 7, False

' Esperar por uma sessão do SAP com o campo de procura da ZPPG.
startedAt = Timer
Dim senhaEscrita
' Mesmo com o SAP já aberto, o atalho pode abrir a janela da senha (Diogo,
' 29/10/2026: «o sap já estava aberto», ficou preso na senha): escreve-se se ela aparecer.
senhaEscrita = False
Do
    ' A janela da senha: escrever a senha e carregar em Logon.
    If Not senhaEscrita Then
        If shell.AppActivate(JANELA_LOGON) Then
            WScript.Sleep 500
            EscreverSenha
            senhaEscrita = True
        End If
    End If
    If TratarLogonMultiplo() Then WScript.Sleep 2000
    Set session = Nothing
    Set searchField = Nothing

    On Error Resume Next
    Set SapGuiAuto = GetObject("SAPGUI")
    If Err.Number = 0 And Not SapGuiAuto Is Nothing Then
        Set application = SapGuiAuto.GetScriptingEngine
        If Not application Is Nothing Then
            Set session = ProcurarSessaoZppg(application)
            If Not session Is Nothing Then Set searchField = session.findById("wnd[0]/usr/ctxtCOD_EQ-LOW")
        End If
    End If
    Err.Clear
    On Error GoTo 0

    If Not searchField Is Nothing Then Exit Do

    If ElapsedSeconds(startedAt) >= SAP_WAIT_SECONDS Then
        MsgBox "O SAP abriu, mas o ecrã da ZPPG não ficou pronto em " & SAP_WAIT_SECONDS & " segundos.", 16, TITULO
        WScript.Quit 1
    End If

    WScript.Sleep 1000
Loop

' Minimizar o SAP (Diogo, 22/10/2026: correr com a página minimizada).
' O script do SAP continua a funcionar com a janela minimizada.
On Error Resume Next
session.findById("wnd[0]").iconify
Err.Clear
On Error GoTo 0

' Grava primeiro num ficheiro à parte: o DMO só vê a lista quando estiver completa.
Set outFile = fso.CreateTextFile(temporario, True)
outFile.WriteLine "search,equipment,percent_use,last_production,batch_description,status,error,consumption,expected life,pieces"
encontrados = 0

For i = 0 To total - 1
    codigo = codigos(i)

    On Error Resume Next
    Err.Clear
    Set searchField = session.findById("wnd[0]/usr/ctxtCOD_EQ-LOW")
    searchField.Text = codigo
    SoCentroMg session
    session.findById("wnd[0]").sendVKey 8

    If Err.Number <> 0 Then
        errorText = "Procura falhou: " & Err.Description
        Err.Clear
        On Error GoTo 0
        WriteErrorRow outFile, codigo, errorText
        GoBackToSearch session
    Else
        On Error GoTo 0
        Dim espera
        espera = SCREEN_WAIT_SECONDS
        If codigo = "*" Then espera = LISTA_TODAS_WAIT_SECONDS
        Set grid = WaitForControl(session, "wnd[0]/usr/cntlCUSTOM_CONTROL_0010/shellcont/shell", espera)
        ' Com a janela minimizada o SAP pode não desenhar a lista: mostra-se a
        ' janela e espera-se outra vez antes de dar erro.
        Dim aviso
        aviso = AvisoSap(session)
        If grid Is Nothing And Len(aviso) = 0 Then
            Registo "a lista não apareceu com o SAP minimizado; a mostrar a janela"
            On Error Resume Next
            session.findById("wnd[0]").maximize
            Err.Clear
            On Error GoTo 0
            Set grid = WaitForControl(session, "wnd[0]/usr/cntlCUSTOM_CONTROL_0010/shellcont/shell", espera)
        End If

        If grid Is Nothing And Len(aviso) > 0 Then
            Registo codigo & ": SAP diz «" & aviso & "»"
            FecharAvisoSap session
            WriteErrorRow outFile, codigo, "SAP: " & aviso
            GoBackToSearch session
        ElseIf grid Is Nothing Then
            WriteErrorRow outFile, codigo, "A lista não apareceu" & MensagemSap(session)
            GoBackToSearch session
        ElseIf grid.RowCount < 1 Then
            WriteErrorRow outFile, codigo, "Sem resultados"
            GoBackToSearch session
        Else
            If codigo = "*" Then Registo "lista de todos os moldes: " & grid.RowCount & " linhas"
            ' As colunas das peças pelo título (o id no SAP não se conhece):
            ' Consumption, Expected life e No. of pieces.
            colConsumo = ColunaPorTitulo(grid, "consumption")
            colVida = ColunaPorTitulo(grid, "expected life")
            colPecas = ColunaPorTitulo(grid, "no. of pieces")
            For r = 0 To grid.RowCount - 1
                ' Numa lista grande o SAP só carrega as linhas à vista: desce a lista.
                If r Mod 20 = 0 Then
                    On Error Resume Next
                    grid.firstVisibleRow = r
                    Err.Clear
                    On Error GoTo 0
                End If
                equipment = ""
                percentUse = ""
                lastProduction = ""
                batchDesc = ""
                statusText = "FOUND"
                errorText = ""

                On Error Resume Next
                Err.Clear
                equipment = Trim(CStr(grid.GetCellValue(r, "EQUNR")))
                percentUse = Trim(CStr(grid.GetCellValue(r, "PERC")))
                lastProduction = Trim(CStr(grid.GetCellValue(r, "DATA_UF")))
                batchDesc = Trim(CStr(grid.GetCellValue(r, "DESC_LOTE")))
                consumo = ""
                vida = ""
                pecas = ""
                If Len(colConsumo) > 0 Then consumo = Trim(CStr(grid.GetCellValue(r, colConsumo)))
                If Len(colVida) > 0 Then vida = Trim(CStr(grid.GetCellValue(r, colVida)))
                If Len(colPecas) > 0 Then pecas = Trim(CStr(grid.GetCellValue(r, colPecas)))
                If Err.Number <> 0 Then
                    statusText = "ERROR"
                    errorText = Err.Description
                    Err.Clear
                End If
                On Error GoTo 0

                If statusText = "FOUND" Then encontrados = encontrados + 1
                outFile.WriteLine CsvValue(codigo) & "," & CsvValue(equipment) & "," & CsvValue(percentUse) & "," & _
                                  CsvValue(lastProduction) & "," & CsvValue(batchDesc) & "," & _
                                  CsvValue(statusText) & "," & CsvValue(errorText) & "," & _
                                  CsvValue(consumo) & "," & CsvValue(vida) & "," & CsvValue(pecas)
            Next
            GoBackToSearch session
        End If
    End If
Next

outFile.Close
' «saida» tem a hora desta corrida: nunca existe já, por isso não se apaga
' nada antes do MoveFile (SAP-02: apagar antes de mover perdia o ficheiro
' anterior se o MoveFile falhasse a seguir).
fso.MoveFile temporario, saida

If Not automatico Then
    MsgBox "Feito: " & encontrados & " ferramentas lidas de " & total & " códigos." & vbCrLf & _
           "Abra as Ferramentas no DMO para gravar a utilização.", 64, TITULO
End If

' A sessão que já está na ZPPG (a que o atalho abriu, ou outra já aberta).
Function ProcurarSessaoZppg(ByVal app)
    Dim c, s, campo
    Set ProcurarSessaoZppg = Nothing
    On Error Resume Next
    For Each c In app.Children
        For Each s In c.Children
            Set campo = Nothing
            Set campo = s.findById("wnd[0]/usr/ctxtCOD_EQ-LOW")
            Err.Clear
            If Not campo Is Nothing Then
                Set ProcurarSessaoZppg = s
                Exit Function
            End If
        Next
    Next
    Err.Clear
    On Error GoTo 0
End Function

Sub GoBackToSearch(ByVal sapSession)
    Dim field, startTime
    startTime = Timer
    Do
        Set field = Nothing
        On Error Resume Next
        Set field = sapSession.findById("wnd[0]/usr/ctxtCOD_EQ-LOW")
        Err.Clear
        On Error GoTo 0

        If Not field Is Nothing Then
            field.Text = ""
            Exit Do
        End If

        On Error Resume Next
        sapSession.findById("wnd[0]").sendVKey 3
        Err.Clear
        On Error GoTo 0

        If ElapsedSeconds(startTime) >= SCREEN_WAIT_SECONDS Then Exit Do
        WScript.Sleep 500
    Loop
End Sub

Function WaitForControl(ByVal sapSession, ByVal controlId, ByVal timeoutSeconds)
    Dim control, startTime
    startTime = Timer
    Set control = Nothing
    Do
        On Error Resume Next
        Err.Clear
        Set control = sapSession.findById(controlId)
        Err.Clear
        On Error GoTo 0

        If Not control Is Nothing Then
            Set WaitForControl = control
            Exit Function
        End If
        ' Uma janela do SAP por cima (por exemplo «Não existem moldes para a
        ' selecção efectuada»): não vale a pena esperar mais.
        If Len(AvisoSap(sapSession)) > 0 Then
            Set WaitForControl = Nothing
            Exit Function
        End If

        If ElapsedSeconds(startTime) >= timeoutSeconds Then
            Set WaitForControl = Nothing
            Exit Function
        End If
        WScript.Sleep 500
    Loop
End Function

' O que o SAP escreveu na barra de baixo (por exemplo, quando o código não
' existe na ZPPG), para se saber porque não veio a lista.
Function MensagemSap(ByVal sapSession)
    Dim texto
    MensagemSap = ""
    On Error Resume Next
    texto = Trim(CStr(sapSession.findById("wnd[0]/sbar").Text))
    Err.Clear
    On Error GoTo 0
    If Len(texto) > 0 Then MensagemSap = " (SAP: " & texto & ")"
    Registo "procura sem lista; SAP: " & texto & "; ecrã: " & EcraSap(sapSession)
End Function

' Só a plant MG (Diogo, 29/10/2026: «consultar apenas MG»): põe MG no campo
' da plant/centro do ecrã da ZPPG, seja qual for o nome dele.
Sub SoCentroMg(ByVal sapSession)
    Dim c, id
    On Error Resume Next
    For Each c In sapSession.findById("wnd[0]/usr").Children
        id = UCase(c.Id)
        If InStr(id, "-LOW") > 0 And (InStr(id, "WERK") > 0 Or InStr(id, "PLANT") > 0 Or InStr(id, "CENTRO") > 0) Then c.Text = CENTRO
    Next
    Err.Clear
    On Error GoTo 0
End Sub

' O título e o que há no ecrã do SAP, para o registo (dmo-sap.log).
Function EcraSap(ByVal sapSession)
    Dim t, c
    On Error Resume Next
    t = sapSession.findById("wnd[0]/titl").Text & " | " & sapSession.Info.Transaction & " |"
    For Each c In sapSession.findById("wnd[0]/usr").Children
        t = t & " " & c.Id
    Next
    Err.Clear
    On Error GoTo 0
    EcraSap = t
End Function

' O texto da janela que o SAP pôs por cima (wnd[1]); vazio se não houver.
Function AvisoSap(ByVal sapSession)
    Dim janela, c, t
    AvisoSap = ""
    On Error Resume Next
    Set janela = Nothing
    Set janela = sapSession.findById("wnd[1]")
    If Not janela Is Nothing Then
        t = ""
        For Each c In sapSession.findById("wnd[1]/usr").Children
            If Len(Trim(CStr(c.Text))) > 0 Then t = Trim(t & " " & Trim(CStr(c.Text)))
        Next
        If Len(t) = 0 Then t = Trim(CStr(janela.Text))
        If Len(t) = 0 Then t = "janela do SAP"
        AvisoSap = t
    End If
    Err.Clear
    On Error GoTo 0
End Function

Sub FecharAvisoSap(ByVal sapSession)
    On Error Resume Next
    sapSession.findById("wnd[1]").sendVKey 0
    Err.Clear
    If Not sapSession.findById("wnd[1]") Is Nothing Then sapSession.findById("wnd[1]").Close
    Err.Clear
    On Error GoTo 0
End Sub

Sub WriteErrorRow(ByVal fileHandle, ByVal procura, ByVal message)
    fileHandle.WriteLine CsvValue(procura) & ",,,,," & CsvValue("ERROR") & "," & CsvValue(message)
End Sub

' O id da coluna da lista cujo título começa por «inicio» (vazio se não houver).
Function ColunaPorTitulo(grid, inicio)
    Dim i, id, titulo
    ColunaPorTitulo = ""
    On Error Resume Next
    For i = 0 To grid.ColumnCount - 1
        id = grid.ColumnOrder(i)
        titulo = LCase(Trim(CStr(grid.GetDisplayedColumnTitle(id))))
        If Err.Number <> 0 Then
            Err.Clear
        ElseIf Left(titulo, Len(inicio)) = inicio Then
            ColunaPorTitulo = id
            Exit For
        End If
    Next
    Err.Clear
    On Error GoTo 0
End Function

Function CsvValue(ByVal textValue)
    CsvValue = Chr(34) & Replace(CStr(textValue), Chr(34), Chr(34) & Chr(34)) & Chr(34)
End Function

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
    Dim currentTime
    currentTime = Timer
    If currentTime >= startTime Then
        ElapsedSeconds = currentTime - startTime
    Else
        ElapsedSeconds = (86400 - startTime) + currentTime
    End If
End Function

' AAAAMMDD-HHMM desta corrida, para o nome do ficheiro de saída (SAP-02).
Function Carimbo()
    Carimbo = Year(Now) & Right("0" & Month(Now), 2) & Right("0" & Day(Now), 2) & "-" & _
              Right("0" & Hour(Now), 2) & Right("0" & Minute(Now), 2)
End Function
