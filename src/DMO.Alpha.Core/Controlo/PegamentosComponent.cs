namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Componente dimensional medido na folha de Pegamentos (spec Controlo
/// Create — Pegamentos: «Pegamentos uses the persisted production component
/// context already established for the production»; manual §7 L330-336:
/// «O controlo aplica-se independentemente a: CM; BQ; MF. Cada componente tem
/// o seu próprio nominal e as suas próprias medições»). A identificação do
/// componente medido é preservada por linha de medição (manual L304); o
/// valor persiste por token canónico (CM/BQ/MF).
/// </summary>
public enum PegamentosComponent
{
    Cm = 1,

    Bq = 2,

    Mf = 3
}
