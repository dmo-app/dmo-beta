# DMO-663 — provas das regressões

As mutações abaixo foram aplicadas uma de cada vez. O código compilou; a falha foi uma asserção, não um erro de compilação. Cada ficheiro foi reposto no fim, antes do CI completo.

| Mutação | Resultado | Asserção observada |
|---|---|---|
| Retirar a leitura de No. of pieces | Detetada | `Assert.Equal() Failure: Values differ` |
| Retirar a leitura de Supplier | Detetada | `Assert.Equal() Failure: Strings differ` |
| Retirar a leitura de Purchase | Detetada | `Assert.Equal() Failure: Values differ` |
| Permitir ao SAP substituir a percentagem manual | Detetada | `Assert.Equal() Failure: Values differ` |
| Gravar a quantidade BQ no campo errado | Detetada | `Assert.Equal() Failure: Values differ` |
| Ignorar o valor recusado | Detetada | `Assert.Null() Failure: Value of type 'Nullable<int>' has a value` |
| Aceitar uma ficha com versão antiga | Detetada | `Assert.Equal() Failure: Values differ` |
| Impedir o × de fechar o aviso | Detetada | `ERR_ASSERTION: false !== true` |

Resultado: **8/8 detetadas**. Os registos brutos desta execução estão em `/workspace/scratch/dmo663-mutacao-*.log`.

O HTML usado para confirmar os títulos foi o anexo `tabela.HTML` (SHA-256 `a112a0b702c591c67ac3d98741aa103b5f353e33a48840fec70aa166548fa302`). O conteúdo do ficheiro de negócio não foi copiado para o repositório; os testes usam dados sintéticos.
