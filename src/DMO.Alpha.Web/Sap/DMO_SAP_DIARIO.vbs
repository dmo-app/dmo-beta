' DMO: a leitura do SAP, tudo seguido. Este ficheiro é escrito pelo DMO
' (Admin > Definições > SAP); não precisa de o mudar aqui.
'
' Como corre:
'   - pelo botão «Atualizar do SAP» do DMO (endereço dmo-sap:...):
'       modo=normal  as produções dos últimos DIAS dias e a % uso de todas;
'       modo=data    só as produções, de inicio=aaaa-mm até fim=aaaa-mm
'                    (sem fim: até hoje);
'       modo=ref     só a % uso das ferramentas do dmo-codigos-pedido.txt.
'   - pelo Agendador de Tarefas (/agendado, de 15 em 15 minutos): uma vez
'     por dia, a partir da HORA, como o modo normal.
' No fim escreve «feito» no dmo-sap-estado.txt. Cada ficheiro lido vai logo
' para o DMO (URL, com a CHAVE do Admin › SAP): grava sem ninguém abrir a app
' (Diogo, 28/10/2026: «sem ter de ler pasta»).

Option Explicit

Const LIMITE_MINUTOS = 30
Const HORA = {{HORA}}
Const URL = "{{URL}}"
Const CHAVE = "{{CHAVE}}"

Dim fso, shell, pasta, pedido, modo, inicio, fim, comecoGeral
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
pasta = fso.GetParentFolderName(WScript.ScriptFullName)

pedido = ""
If WScript.Arguments.Count > 0 Then pedido = WScript.Arguments(0)

If LCase(pedido) = "/agendado" Then
    ' Uma vez por dia, depois da hora marcada no DMO.
    If Hour(Now) < HORA Then WScript.Quit 0
    If LerFicheiro("dmo-sap-ultima.txt") = DataDoDia(Date) Then WScript.Quit 0
    If EmCurso() Then WScript.Quit 0
    EscreverFicheiro "dmo-sap-ultima.txt", DataDoDia(Date)
    modo = "normal"
Else
    modo = LCase(Valor(pedido, "modo"))
    If modo = "" Then modo = "normal"
End If

' Uma leitura de outra vez que ficou presa no SAP: fecha-se antes de começar.
FecharPresos
comecoGeral = Now
EscreverFicheiro "dmo-sap-estado.txt", "a correr " & Agora()
Registo "início, modo " & modo

Select Case modo
    Case "data"
        inicio = Valor(pedido, "inicio")
        fim = Valor(pedido, "fim")
        If inicio = "" Then
            Correr "DMO_FABRICO_SAP.vbs", ""
        ElseIf fim = "" Then
            Correr "DMO_FABRICO_SAP.vbs", " /inicio:" & PrimeiroDia(inicio)
        Else
            Correr "DMO_FABRICO_SAP.vbs", " /inicio:" & PrimeiroDia(inicio) & " /fim:" & UltimoDia(fim)
        End If
    Case "ref"
        Correr "DMO_USO_SAP.vbs", " /codigos:dmo-codigos-pedido.txt"
    Case Else
        Correr "DMO_FABRICO_SAP.vbs", ""
        ' Os códigos de todas as ferramentas, pedidos ao DMO agora.
        BaixarCodigos
        Correr "DMO_USO_SAP.vbs", ""
End Select

Enviar "dmo-fabrico-sap.txt"
Enviar "dmo-uso-sap.csv"
EscreverFicheiro "dmo-sap-estado.txt", "feito " & Agora()
Registo "fim, modo " & modo

Sub Correr(ByVal nome, ByVal extra)
    Dim caminho, processo, comeco
    caminho = fso.BuildPath(pasta, nome)
    If Not fso.FileExists(caminho) Then
        Registo "falta " & nome
        Exit Sub
    End If
    Registo "a correr " & nome & extra
    ' Cada script tem no máximo LIMITE_MINUTOS: se ficar preso no SAP,
    ' fecha-se e segue-se para o seguinte.
    Set processo = shell.Exec("wscript.exe " & Chr(34) & caminho & Chr(34) & " /auto" & extra)
    comeco = Now
    Do While processo.Status = 0
        WScript.Sleep 5000
        If DateDiff("n", comeco, Now) >= LIMITE_MINUTOS Then
            processo.Terminate
            Registo nome & " fechado: passou " & LIMITE_MINUTOS & " minutos"
            Exit Sub
        End If
    Loop
    Registo nome & " acabou (código " & processo.ExitCode & ")"
End Sub

' O dmo-codigos.txt com todas as ferramentas com nome técnico (/sap/codigos).
' Sem rede fica o que o DMO lá deixou da última vez.
Sub BaixarCodigos()
    Dim http
    If URL = "" Or CHAVE = "" Then Exit Sub
    On Error Resume Next
    Set http = CreateObject("MSXML2.ServerXMLHTTP.6.0")
    http.setTimeouts 30000, 30000, 60000, 60000
    http.open "GET", URL & "/sap/codigos", False
    http.setRequestHeader "X-DMO-SAP", CHAVE
    http.send
    If Err.Number = 0 And http.status = 200 Then
        With fso.CreateTextFile(fso.BuildPath(pasta, "dmo-codigos.txt"), True)
            .Write http.responseText
            .Close
        End With
        Registo "códigos pedidos ao DMO"
    Else
        Registo "não foi possível pedir os códigos ao DMO: " & Err.Description
    End If
    Err.Clear
    On Error GoTo 0
End Sub

' Manda um ficheiro lido nesta corrida para o DMO (/sap/enviar). Sem rede ou
' com erro fica na pasta, e o DMO lê-o quando abrirem as Ferramentas.
Sub Enviar(ByVal nome)
    Dim caminho, stream, http
    caminho = fso.BuildPath(pasta, nome)
    If URL = "" Or CHAVE = "" Or Not fso.FileExists(caminho) Then Exit Sub
    If fso.GetFile(caminho).DateLastModified < comecoGeral Then Exit Sub
    On Error Resume Next
    Set stream = CreateObject("ADODB.Stream")
    stream.Type = 1
    stream.Open
    stream.LoadFromFile caminho
    Set http = CreateObject("MSXML2.ServerXMLHTTP.6.0")
    http.setTimeouts 30000, 30000, 300000, 300000
    http.open "POST", URL & "/sap/enviar?ficheiro=" & nome, False
    http.setRequestHeader "X-DMO-SAP", CHAVE
    http.setRequestHeader "Content-Type", "application/octet-stream"
    http.send stream.Read
    If Err.Number <> 0 Then
        Registo "não foi possível enviar " & nome & ": " & Err.Description
    Else
        Registo "enviado " & nome & " (" & http.status & ") " & http.responseText
    End If
    stream.Close
    Err.Clear
    On Error GoTo 0
End Sub

' O valor de «nome=» no endereço dmo-sap:modo=...&inicio=...
Function Valor(ByVal texto, ByVal nome)
    Dim partes, p, i
    Valor = ""
    i = InStr(texto, ":")
    If i > 0 Then texto = Mid(texto, i + 1)
    texto = Replace(texto, "/", "")
    texto = Replace(texto, "?", "&")
    partes = Split(texto, "&")
    For Each p In partes
        If LCase(Left(p, Len(nome) + 1)) = LCase(nome) & "=" Then
            Valor = Mid(p, Len(nome) + 2)
            ' Só letras, números e «-».
            Dim j, c, limpo
            limpo = ""
            For j = 1 To Len(Valor)
                c = Mid(Valor, j, 1)
                If c Like "[A-Za-z0-9-]" Then limpo = limpo & c
            Next
            Valor = limpo
        End If
    Next
End Function

' aaaa-mm -> 01.mm.aaaa
Function PrimeiroDia(ByVal anoMes)
    PrimeiroDia = DataSap(DateSerial(CInt(Left(anoMes, 4)), CInt(Mid(anoMes, 6, 2)), 1))
End Function

' aaaa-mm -> o último dia desse mês (ou hoje, se o mês ainda não acabou).
Function UltimoDia(ByVal anoMes)
    Dim d
    d = DateSerial(CInt(Left(anoMes, 4)), CInt(Mid(anoMes, 6, 2)) + 1, 0)
    If d > Date Then d = Date
    UltimoDia = DataSap(d)
End Function

Function DataSap(ByVal d)
    DataSap = Right("0" & Day(d), 2) & "." & Right("0" & Month(d), 2) & "." & Year(d)
End Function

Function DataDoDia(ByVal d)
    DataDoDia = Year(d) & "-" & Right("0" & Month(d), 2) & "-" & Right("0" & Day(d), 2)
End Function

Function Agora()
    Agora = DataDoDia(Date) & " " & Right("0" & Hour(Now), 2) & ":" & Right("0" & Minute(Now), 2)
End Function

' Outra leitura a correr agora (do botão do DMO)?
Function EmCurso()
    Dim estado
    estado = LerFicheiro("dmo-sap-estado.txt")
    EmCurso = (Left(estado, 8) = "a correr" And Mid(estado, 10, 10) = DataDoDia(Date))
End Function

Function LerFicheiro(ByVal nome)
    Dim caminho
    LerFicheiro = ""
    caminho = fso.BuildPath(pasta, nome)
    If Not fso.FileExists(caminho) Then Exit Function
    On Error Resume Next
    With fso.OpenTextFile(caminho, 1)
        If Not .AtEndOfStream Then LerFicheiro = Trim(.ReadLine)
        .Close
    End With
    Err.Clear
    On Error GoTo 0
End Function

Sub EscreverFicheiro(ByVal nome, ByVal texto)
    On Error Resume Next
    With fso.CreateTextFile(fso.BuildPath(pasta, nome), True)
        .WriteLine texto
        .Close
    End With
    Err.Clear
    On Error GoTo 0
End Sub

' Fecha os DMO_USO_SAP e DMO_FABRICO_SAP que ainda estejam a correr.
Sub FecharPresos()
    Dim wmi, p, linha
    On Error Resume Next
    Set wmi = GetObject("winmgmts:\\.\root\cimv2")
    For Each p In wmi.ExecQuery("SELECT CommandLine FROM Win32_Process WHERE Name = 'wscript.exe' OR Name = 'cscript.exe'")
        linha = LCase(p.CommandLine & "")
        If InStr(linha, "dmo_uso_sap.vbs") > 0 Or InStr(linha, "dmo_fabrico_sap") > 0 Then
            p.Terminate
            Registo "fechado um script preso: " & linha
        End If
    Next
    Err.Clear
    On Error GoTo 0
End Sub

Sub Registo(ByVal texto)
    Dim f
    On Error Resume Next
    Set f = fso.OpenTextFile(fso.BuildPath(pasta, "dmo-sap.log"), 8, True)
    f.WriteLine Now & "  " & texto
    f.Close
    Err.Clear
    On Error GoTo 0
End Sub
