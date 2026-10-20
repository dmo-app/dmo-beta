using System.Net;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O envio que o browser faz quando o operador muda um campo e o formulário
/// dele é enviado (o requestSubmit de data-dmo-autosubmit e o envio de
/// recurso de data-dmo-valor): a ação e os campos com nome do formulário que
/// contém esse campo, pela ordem da página, com os valores que o servidor
/// desenhou (o input com o value, o select com a opção selected ou a
/// primeira) e o campo mudado com o valor novo. Mostra o que um envio leva,
/// sem um browser (deep sweep, 18/10/2026, A1-M1 e B-boquilhas-3).
/// </summary>
internal static partial class EnvioDoFormulario
{
    public static (string Acao, IReadOnlyList<(string Nome, string Valor)> Campos) Mudar(
        string html, string idDoCampo, string valorNovo) =>
        Preencher(html, (idDoCampo, valorNovo));

    /// <summary>
    /// O envio de um formulário em que o operador preencheu vários campos
    /// (pelo id, todos no formulário do primeiro): os outros seguem como a
    /// página os desenhou.
    /// </summary>
    public static (string Acao, IReadOnlyList<(string Nome, string Valor)> Campos) Preencher(
        string html, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        if (preenchidos.Length == 0)
        {
            throw new ArgumentException("Falta o campo que o operador preenche.", nameof(preenchidos));
        }
        return Enviar(html, preenchidos);
    }

    /// <summary>
    /// O envio completo, como o browser o monta (fase base dos testes,
    /// 19/10/2026): os campos preenchidos pelo id; os radios escolhidos pelo
    /// name e value (o operador marca «Entrada»); e o botão carregado pelo
    /// id, que leva o seu name e value e, com formaction, muda o destino.
    /// O formulário é o do botão ou, sem botão, o do primeiro campo. Os
    /// campos dele são os de dentro do &lt;form&gt; sem atributo form e os
    /// de qualquer sítio da página com form="id" dele; um campo de dentro
    /// com form="outro" é do outro formulário e não vai.
    /// </summary>
    public static (string Acao, IReadOnlyList<(string Nome, string Valor)> Campos) Enviar(
        string html,
        (string IdDoCampo, string Valor)[] preenchidos,
        (string Nome, string Valor)[]? radios = null,
        string? idDoBotao = null)
    {
        radios ??= [];
        var forms = FormRegex().Matches(html);
        Match? botao = null;
        Match form;
        if (idDoBotao is not null)
        {
            botao = BotaoRegex().Matches(html).FirstOrDefault(b => Atributo(b.Groups["attrs"].Value, "id") == idDoBotao)
                ?? throw new InvalidOperationException($"A página não tem o botão #{idDoBotao}.");
            form = FormDe(html, forms, botao)
                ?? throw new InvalidOperationException($"O botão #{idDoBotao} não é de nenhum formulário.");
        }
        else if (preenchidos.Length > 0)
        {
            var idDoCampo = preenchidos[0].IdDoCampo;
            var primeiro = CampoRegex().Matches(html).FirstOrDefault(c => Atributo(c.Groups["attrs"].Value, "id") == idDoCampo);
            form = (primeiro is null ? null : FormDe(html, forms, primeiro))
                ?? throw new InvalidOperationException($"Nenhum formulário contém #{idDoCampo}.");
        }
        else
        {
            throw new ArgumentException("Falta o campo que o operador preenche ou o botão que carrega.", nameof(preenchidos));
        }

        var acao = Atributo(form.Groups["attrs"].Value, "action")
            ?? throw new InvalidOperationException("O formulário não tem action.");
        var idDoForm = Atributo(form.Groups["attrs"].Value, "id");

        var campos = new List<(int Posicao, string Nome, string Valor)>();
        var mudados = new HashSet<string>(StringComparer.Ordinal);
        var escolhidos = new HashSet<(string, string)>();
        foreach (Match campo in CampoRegex().Matches(html))
        {
            var attrs = campo.Groups["attrs"].Value;
            var nome = Atributo(attrs, "name");
            if (nome is null || Booleano(attrs, "disabled") || !EDoFormulario(campo, attrs, form, idDoForm))
            {
                continue;
            }

            string valor;
            if (campo.Value.StartsWith("<select", StringComparison.Ordinal))
            {
                var opcoes = OpcaoRegex().Matches(campo.Groups["opcoes"].Value);
                var escolhida = opcoes.FirstOrDefault(o => Booleano(o.Groups["attrs"].Value, "selected")) ?? opcoes.FirstOrDefault();
                if (escolhida is null)
                {
                    continue;
                }
                valor = Atributo(escolhida.Groups["attrs"].Value, "value") ?? WebUtility.HtmlDecode(escolhida.Groups["texto"].Value);
            }
            else if (campo.Value.StartsWith("<textarea", StringComparison.Ordinal))
            {
                valor = WebUtility.HtmlDecode(campo.Groups["area"].Value);
            }
            else
            {
                var tipo = Atributo(attrs, "type") ?? "text";
                if (tipo is "submit" or "button" or "reset" or "image" or "file")
                {
                    continue;
                }
                valor = Atributo(attrs, "value") ?? (tipo is "checkbox" or "radio" ? "on" : string.Empty);
                var marcado = Booleano(attrs, "checked");
                if (tipo == "radio" && radios.Any(r => r.Nome == nome))
                {
                    marcado = radios.Any(r => r.Nome == nome && r.Valor == valor);
                    if (marcado)
                    {
                        escolhidos.Add((nome, valor));
                    }
                }
                if (tipo is "checkbox" or "radio" && !marcado)
                {
                    continue;
                }
            }

            var id = Atributo(attrs, "id");
            foreach (var (preenchido, valorNovo) in preenchidos)
            {
                if (id == preenchido)
                {
                    valor = valorNovo;
                    mudados.Add(preenchido);
                }
            }
            campos.Add((campo.Index, nome, valor));
        }

        var falta = preenchidos.FirstOrDefault(p => !mudados.Contains(p.IdDoCampo));
        if (falta.IdDoCampo is not null)
        {
            throw new InvalidOperationException($"#{falta.IdDoCampo} não é um campo com nome do formulário.");
        }
        var radioEmFalta = radios.FirstOrDefault(r => !escolhidos.Contains((r.Nome, r.Valor)));
        if (radioEmFalta.Nome is not null)
        {
            throw new InvalidOperationException($"O formulário não tem o radio {radioEmFalta.Nome}=«{radioEmFalta.Valor}».");
        }
        if (botao is not null)
        {
            var attrs = botao.Groups["attrs"].Value;
            if (Atributo(attrs, "name") is { } nomeDoBotao)
            {
                campos.Add((botao.Index, nomeDoBotao, Atributo(attrs, "value") ?? string.Empty));
            }
            acao = Atributo(attrs, "formaction") ?? acao;
        }
        return (acao, campos.OrderBy(c => c.Posicao).Select(c => (c.Nome, c.Valor)).ToList());
    }

    /// <summary>O formulário de um campo ou botão: o do form="id" ou o que o contém.</summary>
    private static Match? FormDe(string html, MatchCollection forms, Match elemento)
    {
        var attrs = elemento.Groups["attrs"].Value;
        if (Atributo(attrs, "form") is { } idDoForm)
        {
            return forms.FirstOrDefault(f => Atributo(f.Groups["attrs"].Value, "id") == idDoForm);
        }
        return forms.FirstOrDefault(f => Dentro(elemento, f));
    }

    private static bool EDoFormulario(Match campo, string attrs, Match form, string? idDoForm) =>
        Atributo(attrs, "form") is { } dono ? dono == idDoForm : Dentro(campo, form);

    private static bool Dentro(Match elemento, Match form)
    {
        var corpo = form.Groups["corpo"];
        return elemento.Index >= corpo.Index && elemento.Index < corpo.Index + corpo.Length;
    }

    /// <summary>
    /// O valor da opção que o operador escolhe num select pelo texto que lê
    /// (a máquina «B5», não o id dela). Falha se o select ou a opção não
    /// estão na página.
    /// </summary>
    public static string Opcao(string html, string idDoSelect, string texto)
    {
        var select = CampoRegex().Matches(html)
            .FirstOrDefault(m => m.Value.StartsWith("<select", StringComparison.Ordinal)
                && Atributo(m.Groups["attrs"].Value, "id") == idDoSelect)
            ?? throw new InvalidOperationException($"A página não tem o select #{idDoSelect}.");
        var opcao = OpcaoRegex().Matches(select.Groups["opcoes"].Value)
            .SingleOrDefault(o => WebUtility.HtmlDecode(o.Groups["texto"].Value).Trim() == texto)
            ?? throw new InvalidOperationException($"#{idDoSelect} não tem a opção «{texto}».");
        return Atributo(opcao.Groups["attrs"].Value, "value") ?? texto;
    }

    public static HttpContent Corpo(IReadOnlyList<(string Nome, string Valor)> campos) =>
        HtmlFormHelpers.BuildFormPost(campos.Select(c => (c.Nome, c.Valor)).ToArray());

    private static string? Atributo(string attrs, string nome)
    {
        var match = Regex.Match(attrs, $"(?:^|\\s){Regex.Escape(nome)}=\"(?<v>[^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    private static bool Booleano(string attrs, string nome) =>
        Regex.IsMatch(attrs, $"(?:^|\\s){Regex.Escape(nome)}(?:=|\\s|/|$)");

    [GeneratedRegex("<form\\b(?<attrs>[^>]*)>(?<corpo>.*?)</form>", RegexOptions.Singleline)]
    private static partial Regex FormRegex();

    [GeneratedRegex("<select\\b(?<attrs>[^>]*)>(?<opcoes>.*?)</select>|<textarea\\b(?<attrs>[^>]*)>(?<area>.*?)</textarea>|<input\\b(?<attrs>[^>]*?)/?>", RegexOptions.Singleline)]
    private static partial Regex CampoRegex();

    [GeneratedRegex("<button\\b(?<attrs>[^>]*)>", RegexOptions.Singleline)]
    private static partial Regex BotaoRegex();

    [GeneratedRegex("<option\\b(?<attrs>[^>]*)>(?<texto>.*?)</option>", RegexOptions.Singleline)]
    private static partial Regex OpcaoRegex();
}
