namespace DMO.Alpha.Core.Controlo;

/// <summary>Uma visita registada à folha de um controlo (o registo de visão).</summary>
public sealed record ControloViewingReadModel(
    string OperatorId,
    DateTime ViewedAtUtc);
