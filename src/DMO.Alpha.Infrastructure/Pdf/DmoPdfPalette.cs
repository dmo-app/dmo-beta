namespace DMO.Alpha.Infrastructure.Pdf;

/// <summary>
/// OS PDFs SÃO PARA IMPRIMIR (DMO-515, 27/10/2026): sempre claros, a preto e
/// cinzento, sem as cores da app nem cartões escuros. As cores de estado só
/// pintam o texto (OK verde, NOK vermelho), nunca o fundo. O azul da paleta
/// da app fica aqui só como referência (Sky..Slate), sem uso nos PDFs.
///
/// Antes: A PALETA OFICIAL DO DMO (decisão do Diogo, 17/10/2026;
/// docs/ui-reference/paleta-oficial.md) nos PDFs: os mesmos tons que
/// `--dmo-palette-*` e `--dmo-brand-*` em wwwroot/0_ASSET_CANONICAL_DESIGN_SYSTEM.css.
/// Se a paleta mudar lá, muda-se também aqui. As cores de estado ficam fora
/// da paleta, de propósito, mas também vivem aqui (abaixo): os geradores não
/// têm cores soltas.
/// </summary>
internal static class DmoPdfPalette
{
    public const string Sky = "#afd1f2";
    public const string Mist = "#92b4cc";
    public const string Steel = "#6494b4";
    public const string Ocean = "#4c7c9c";
    public const string Slate = "#44647c";

    /// <summary>Cabeçalhos, títulos e links (como os botões da app).</summary>
    public const string Accent = "#333333";
    /// <summary>Fundo escuro dos cartões em destaque (--dmo-brand-700).</summary>
    public const string Deep = "#333333";
    /// <summary>Texto principal (--dmo-brand-900).</summary>
    public const string Ink = "#1f1f1f";
    /// <summary>Texto secundário (--dmo-muted).</summary>
    public const string Muted = "#5c5c5c";
    /// <summary>Linhas e contornos (--dmo-border).</summary>
    public const string Line = "#c8c8c8";
    /// <summary>Texto claro sobre fundo escuro (--dmo-brand-100).</summary>
    public const string OnDeep = "#e6e6e6";
    /// <summary>Fundo suave azul (--dmo-brand-050).</summary>
    public const string Tint = "#eeeeee";
    /// <summary>Fundo quase branco (--dmo-subtle).</summary>
    public const string Soft = "#f6f6f6";
    /// <summary>Fundo branco dos cartões e das linhas pares.</summary>
    public const string White = "#ffffff";

    // AS CORES DE ESTADO (sweep de 19/10/2026, 3.16): fora da paleta, como a
    // regra permite, e iguais às do ecrã. Antes cada gerador tinha as suas:
    // o OK das punções saía num verde no PDF do PU NNPB e noutro no do
    // Resumo, «por verificar» saía no castanho do erro, e «não avaliável»
    // saía a âmbar, a cor de «por verificar».

    // Medições e verificações: as áreas, os Pegamentos e o PU NNPB
    // (as mesmas do ecrã do Resumo).
    public const string Ok = "#15803d";
    public const string OkBackground = White;
    public const string OkBorder = Line;
    public const string Nok = "#b91c1c";
    public const string NokBackground = White;
    public const string NokBorder = "#efb1b1";
    /// <summary>O valor fora do corredor, na tabela dos Pegamentos.</summary>
    public const string NokCell = "#f8d4d4";
    public const string ToCheck = "#a16207";
    public const string ToCheckBackground = White;
    /// <summary>«Não avaliável» (sem nominal): o cinzento do ecrã dos Pegamentos, nunca o âmbar.</summary>
    public const string NotEvaluable = "#94a3b8";
    public const string NotEvaluableBackground = White;

    // A aprovação (os selos e o cartão «Aprovação»): as cores do cartão de
    // aprovação do ecrã do Resumo. O «Não aprovado» usa o vermelho do NOK.
    public const string Approved = Ok;
    public const string ToApprove = Muted;
    public const string ToApproveBackground = ToCheckBackground;
    public const string ToApproveBorder = Line;

    // Os avisos (as cores de --dmo-warning do ecrã) e a «Alteração» das punções.
    public const string Warning = ToApprove;
    public const string WarningBackground = Soft;
    public const string WarningText = Ink;
    public const string Change = "#956124";
}
