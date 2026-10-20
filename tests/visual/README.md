# Provas dos PDFs de Controlo

Os testes de `PdfRecoveryTests` geram os três componentes do PDF de referência (10 linhas), quatro CM de Peso e duas comparações independentes. Incluem igualdade ao limite, costura/90° fora com média dentro, todas dentro, componente sem leituras, nominal histórico e nominal ausente. Os números das amostras são dados de teste; nunca são usados na aplicação.

`PegamentosPageTests.PdfDownloads_UsePersistedModels_AllComponents_AndDoNotWriteDatabaseOrServerFiles` autentica um utilizador no host ASP.NET, regista as medições pela aplicação, carrega os modelos pelas queries reais e verifica quatro downloads: Peso, Pegamentos, Resumo e documento único. Usa a base EF InMemory isolada, não a base Railway. Verifica anexos PDF, ausência da configuração de diretório e preservação de controlos, medições e visitas durante os POSTs de download.

Para exportar os artefactos, em `/workspace/DMO`, com .NET 10 ativo:

```bash
export DMO_PDF_EVIDENCE_DIR=/workspace/pdf-evidence/current
dotnet test tests/DMO.Alpha.Infrastructure.Tests --filter FullyQualifiedName~PdfRecoveryTests
dotnet test tests/DMO.Alpha.Web.Tests --filter FullyQualifiedName~PdfDownloads_UsePersistedModels
python tests/visual/validate_pdf_evidence.py /workspace/pdf-evidence --reference '/caminho/5447T173 202604 (1).pdf'
```

O script Python requer Poppler (`pdftoppm`), PyMuPDF 1.26.6 e Pillow 12.3.0 (versões usadas nesta validação). Verifica conteúdo, tabelas completas, comparações independentes, alertas, histórico e os quatro destinos dos links internos. Renderiza todas as páginas e cria comparação lado a lado com a referência. Se existir `baseline/pegamentos-page-1.png`, compara também com o gerador original. A verificação automática não substitui revisão visual pelo proprietário.

A aplicação gera bytes em memória e devolve `Content-Disposition: attachment`; o browser guarda os PDFs no computador do utilizador. Apenas os testes exportam artefactos para revisão quando a variável acima está definida.

No branch original `b8921156679a4f7238413480a1f6ad8e6906a8fa`, reproduziram-se duas falhas anteriores de navegação: `Browser_ExactReferenceFilter_ServerSide_AndRecentesPrimeiro` (espera um onclick antigo) e `ResumoPage_LinksToPegamentosSheet_WithEnsuredControloId` (espera um link secundário antigo). Não foram alterados CSS global, autenticação ou navegação para corrigir essas expectativas.

Validação Railway DEV e aprovação visual do proprietário continuam pendentes. O proxy bloqueou `dmo-beta-dev-preview.up.railway.app` com HTTP 403; a inclusão desse domínio foi guardada no rascunho do ambiente, ainda não aplicada. Não houve merge, deploy nem acesso a PROD.

O Peso revisto conserva a hierarquia visual do mockup na primeira página. A comparação com o Peso anterior e os volumes ficam na segunda página, sem eliminar dados. O documento único tem quatro páginas no exemplo completo: Resumo + duas páginas de Peso + Pegamentos.
