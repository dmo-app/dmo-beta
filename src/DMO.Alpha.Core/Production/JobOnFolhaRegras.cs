namespace DMO.Alpha.Core.Production;

/// <summary>
/// Uma linha CAL como a folha a manda ao guardar: a designação, o valor, a QTD
/// Máq. e a marca de por omissão (<c>Cal[i].Padrao</c>: a designação por
/// omissão com que a linha abriu; null nas outras).
/// </summary>
public sealed record LinhaCalEnvio(string? Designacao, string? Valor, string? QtdMaq = null, string? Padrao = null);

/// <summary>O campo de uma linha CAL, para a chave de uma recusa de tamanho.</summary>
public enum JobOnFolhaCampoCal
{
    Designacao,
    Valor,
    QtdMaq
}

/// <summary>
/// As regras do texto escrito da folha do Job On ao gravar, num só sítio para
/// os handlers e para o ecrã:
/// <list type="bullet">
/// <item>cada texto normaliza-se antes de se contar e de se gravar: CRLF passa a
/// LF, os espaços das pontas saem e o vazio passa a null. O browser conta cada
/// quebra de linha como um carácter e envia-a como dois; por isso uma nota de
/// várias linhas no limite passa;</item>
/// <item>um texto acima do máximo do catálogo é a recusa <c>TextoLongo</c>
/// (o único bloqueio da folha: um dado que não cabe), com a chave do campo
/// ou, na CAL, <c>cal:{linha}:designacao</c>, <c>cal:{linha}:valor</c> ou
/// <c>cal:{linha}:qtd_maq</c>, em que a linha é a posição no envio, a contar
/// de 1;</item>
/// <item>uma linha CAL por omissão sem valor, sem QTD Máq. e com a designação
/// por mudar não se grava; uma linha sem nada escrito não fica; uma linha só
/// com um dos campos fica.</item>
/// </list>
/// </summary>
public static class JobOnFolhaRegras
{
    /// <summary>O prefixo da chave de um campo de uma linha CAL numa recusa (<c>cal:3:valor</c>).</summary>
    public const string PrefixoChaveCal = "cal:";

    /// <summary>O máximo do texto de uma alteração da folha em «O que mudou» (o de <see cref="JobOnChange.ValueMaxLength"/>).</summary>
    public const int ResumoMaximo = JobOnChange.ValueMaxLength;

    private const string SufixoDesignacao = "designacao";
    private const string SufixoValor = "valor";
    private const string SufixoQtdMaq = "qtd_maq";

    /// <summary>
    /// O texto como se conta e se grava: CRLF passa a LF, sem os espaços das
    /// pontas, e o vazio passa a null.
    /// </summary>
    public static string? Normalizar(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        var limpo = texto.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>
    /// Os valores da folha normalizados, uma entrada por chave do catálogo: uma
    /// chave que o envio não traz fica vazia, e uma chave que não é do catálogo
    /// não entra.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> NormalizarFolha(IReadOnlyDictionary<string, string?> folha) =>
        JobOnFolhaCampos.Todos.ToDictionary(
            c => c.Chave,
            c => folha.TryGetValue(c.Chave, out var valor) ? Normalizar(valor) : null,
            StringComparer.Ordinal);

    /// <summary>A linha CAL com os campos e a marca de por omissão normalizados.</summary>
    public static LinhaCalEnvio NormalizarLinha(LinhaCalEnvio linha) =>
        new(Normalizar(linha.Designacao), Normalizar(linha.Valor), Normalizar(linha.QtdMaq), Normalizar(linha.Padrao));

    /// <summary>A chave de um campo de uma linha CAL numa recusa: <c>cal:3:designacao</c>, <c>cal:3:valor</c> ou <c>cal:3:qtd_maq</c>.</summary>
    public static string ChaveCal(int linha, JobOnFolhaCampoCal campo) =>
        $"{PrefixoChaveCal}{linha}:{campo switch
        {
            JobOnFolhaCampoCal.Designacao => SufixoDesignacao,
            JobOnFolhaCampoCal.Valor => SufixoValor,
            _ => SufixoQtdMaq
        }}";

    /// <summary>
    /// A primeira chave cujo texto, já normalizado, passa o máximo, pela ordem da
    /// folha: os campos do catálogo e depois as linhas CAL (designação, valor,
    /// QTD Máq.). Null quando tudo cabe.
    /// </summary>
    public static string? PrimeiraChaveLonga(
        IReadOnlyDictionary<string, string?>? folha,
        IReadOnlyList<LinhaCalEnvio>? cal)
    {
        if (folha is not null)
        {
            foreach (var campo in JobOnFolhaCampos.Todos)
            {
                if (folha.TryGetValue(campo.Chave, out var valor) && valor is { } texto && texto.Length > campo.Maximo)
                {
                    return campo.Chave;
                }
            }
        }

        for (var i = 0; i < (cal?.Count ?? 0); i++)
        {
            var linha = cal![i];
            if (linha.Designacao is { Length: > JobOnFolhaCampos.CalMaximo })
            {
                return ChaveCal(i + 1, JobOnFolhaCampoCal.Designacao);
            }

            if (linha.Valor is { Length: > JobOnFolhaCampos.CalMaximo })
            {
                return ChaveCal(i + 1, JobOnFolhaCampoCal.Valor);
            }

            if (linha.QtdMaq is { Length: > JobOnFolhaCampos.CalMaximo })
            {
                return ChaveCal(i + 1, JobOnFolhaCampoCal.QtdMaq);
            }
        }

        return null;
    }

    /// <summary>
    /// Uma linha por omissão ainda por escrever: sem valor, sem QTD Máq. e com a
    /// designação igual à marca de por omissão. Não se grava. Espera a linha
    /// normalizada.
    /// </summary>
    public static bool PorOmissaoPorEscrever(LinhaCalEnvio linha) =>
        linha.Padrao is { } padrao
        && linha.Valor is null
        && linha.QtdMaq is null
        && string.Equals(linha.Designacao, padrao, StringComparison.Ordinal);

    /// <summary>
    /// A linha fica ao gravar: tem alguma coisa escrita e não é uma linha por
    /// omissão por escrever. Espera a linha normalizada.
    /// </summary>
    public static bool Fica(LinhaCalEnvio linha) =>
        (linha.Designacao is not null || linha.Valor is not null || linha.QtdMaq is not null)
        && !PorOmissaoPorEscrever(linha);

    /// <summary>As linhas que ficam ao gravar, pela ordem do envio e numeradas a partir de 1. Espera as linhas normalizadas.</summary>
    public static IReadOnlyList<JobOnFolhaCalLida> LinhasQueFicam(IEnumerable<LinhaCalEnvio> cal) =>
        cal.Where(Fica)
            .Select((l, i) => new JobOnFolhaCalLida(i + 1, l.Designacao, l.Valor, l.QtdMaq))
            .ToList();

    /// <summary>
    /// As linhas CAL com que a folha abre: as gravadas, pela ordem; sem nenhuma
    /// gravada, as oito designações por omissão, com a marca de por omissão e o
    /// resto vazio.
    /// </summary>
    public static IReadOnlyList<LinhaCalEnvio> CalParaAbrir(IReadOnlyList<JobOnFolhaCalLida> gravadas) =>
        gravadas.Count > 0
            ? gravadas.Select(l => new LinhaCalEnvio(l.Designacao, l.Valor, l.QtdMaq)).ToList()
            : JobOnFolhaCampos.CalPorOmissao.Select(d => new LinhaCalEnvio(d, null, null, d)).ToList();

    /// <summary>
    /// A folha enviada tem alguma coisa escrita: um campo com texto ou uma linha
    /// CAL que fica. Espera o envio normalizado.
    /// </summary>
    public static bool TemEscrito(IReadOnlyDictionary<string, string?>? folha, IReadOnlyList<LinhaCalEnvio>? cal) =>
        (folha?.Values.Any(v => v is not null) ?? false) || (cal?.Any(Fica) ?? false);

    /// <summary>
    /// O que muda da folha gravada para a enviada, pela ordem da folha: o rótulo
    /// de cada campo diferente («CM · QTD Stock») e «CAL» quando as linhas que
    /// ficam não são as gravadas. Um envio sem folha (null) ou sem CAL (null) não
    /// muda essa parte. Espera o envio normalizado.
    /// </summary>
    public static IReadOnlyList<string> Alteracoes(
        JobOnFolhaLida gravada,
        IReadOnlyDictionary<string, string?>? folha,
        IReadOnlyList<LinhaCalEnvio>? cal)
    {
        var mudou = new List<string>();
        if (folha is not null)
        {
            foreach (var campo in JobOnFolhaCampos.Todos)
            {
                var enviado = folha.TryGetValue(campo.Chave, out var valor) ? valor : null;
                if (!string.Equals(gravada.Valor(campo.Chave), enviado, StringComparison.Ordinal))
                {
                    mudou.Add(campo.RotuloAlteracao);
                }
            }
        }

        if (cal is not null && !LinhasQueFicam(cal).SequenceEqual(gravada.Cal))
        {
            mudou.Add(JobOnFolhaCampos.Cal);
        }

        return mudou;
    }

    /// <summary>
    /// O texto da alteração da folha em «O que mudou»: os rótulos do que mudou,
    /// separados por vírgulas, até <see cref="ResumoMaximo"/> caracteres; os que
    /// não cabem contam-se no fim («… e mais 2»). Null sem alterações.
    /// </summary>
    public static string? Resumo(IReadOnlyList<string> alteracoes)
    {
        if (alteracoes.Count == 0)
        {
            return null;
        }

        var texto = alteracoes[0];
        for (var i = 1; i < alteracoes.Count; i++)
        {
            var seguinte = $"{texto}, {alteracoes[i]}";
            var restantes = alteracoes.Count - i - 1;
            var cabe = restantes == 0
                ? seguinte.Length <= ResumoMaximo
                : seguinte.Length + $" e mais {restantes}".Length <= ResumoMaximo;
            if (!cabe)
            {
                return $"{texto} e mais {alteracoes.Count - i}";
            }

            texto = seguinte;
        }

        return texto;
    }

    /// <summary>
    /// O rótulo de uma chave recusada: um campo do catálogo dá o seu rótulo com a
    /// família («CM · Notas»), e uma chave da CAL dá «CAL · linha 3 · Valor».
    /// Null para uma chave que não é da folha.
    /// </summary>
    public static string? RotuloDaChave(string chave)
    {
        if (JobOnFolhaCampos.Encontrar(chave) is { } campo)
        {
            return campo.RotuloAlteracao;
        }

        return LerChaveCal(chave) is var (linha, campoCal)
            ? $"{JobOnFolhaCampos.Cal} · linha {linha} · {campoCal switch
            {
                JobOnFolhaCampoCal.Designacao => "Designação",
                JobOnFolhaCampoCal.Valor => "Valor",
                _ => JobOnFolhaCampos.QtdMaq
            }}"
            : null;
    }

    /// <summary>O máximo de caracteres de uma chave da folha (null quando a chave não é da folha).</summary>
    public static int? MaximoDaChave(string chave) =>
        JobOnFolhaCampos.Encontrar(chave)?.Maximo
        ?? (LerChaveCal(chave) is not null ? JobOnFolhaCampos.CalMaximo : null);

    /// <summary>A linha (a partir de 1) e o campo de uma chave da CAL; null quando a chave não é da CAL.</summary>
    public static (int Linha, JobOnFolhaCampoCal Campo)? LerChaveCal(string chave)
    {
        if (!chave.StartsWith(PrefixoChaveCal, StringComparison.Ordinal))
        {
            return null;
        }

        var partes = chave[PrefixoChaveCal.Length..].Split(':');
        if (partes.Length != 2
            || !int.TryParse(partes[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var linha)
            || linha < 1)
        {
            return null;
        }

        return partes[1] switch
        {
            SufixoDesignacao => (linha, JobOnFolhaCampoCal.Designacao),
            SufixoValor => (linha, JobOnFolhaCampoCal.Valor),
            SufixoQtdMaq => (linha, JobOnFolhaCampoCal.QtdMaq),
            _ => null
        };
    }
}
