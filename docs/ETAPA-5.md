# Etapa 5 — Consultas e acompanhamento dos chamados

Pasta principal: D:\Curso programação .NET -exercicios\central-chamados

## O que você construiu

- Prioridades Baixa, Normal, Alta e Urgente, escolhidas ao abrir o chamado.
- Busca por trecho do título e filtros de status e prioridade.
- Paginação no banco, com 10 chamados por página.
- Painel de total, abertos, em atendimento e resolvidos.
- Comentários do solicitante e do técnico responsável, registrados no histórico.

As permissões da etapa 4 continuam valendo em todas as consultas e ações.

## Atualizar e executar

Se o servidor estiver aberto, encerre-o com Ctrl+C. No PowerShell:

```powershell
Set-Location -LiteralPath "D:\Curso programação .NET -exercicios\central-chamados"
dotnet run --project src/CentralChamados.Web -- --init
dotnet run --project src/CentralChamados.Web
```

Abra http://localhost:5287 e entre com sua conta. O primeiro comando aplica também a migration Prioridades. Chamados antigos recebem prioridade Normal; o histórico é preservado.

O banco web padrão continua em src/CentralChamados.Web/.dados/chamados.db. Se você usa CHAMADOS_DB, mantenha o mesmo caminho absoluto nos dois comandos. O pacote não contém banco, contas ou senhas prontas.

Para criar uma conta e conceder o perfil Técnico, siga o guia da etapa 4. O comando administrativo continua:

```powershell
dotnet run --project src/CentralChamados.Web -- --promover-tecnico "email-da-conta-cadastrada@example.test"
```

## Regras desta etapa

### Prioridade

A prioridade é escolhida na abertura e permanece fixa nesta etapa. O padrão é Normal. A lista ordena Urgente, Alta, Normal e Baixa; dentro de cada prioridade, ordena por título e desempata pelo ID.

Prioridade organiza a fila, mas não representa um prazo de atendimento. Não há indicador de atraso, pois ainda não definimos uma regra de prazo.

### Filtros e painel

A busca procura um trecho literal no título, diferenciando maiúsculas, minúsculas e acentos. % e _ são caracteres comuns, não curingas. Espaços no início e no fim da busca são removidos.

Os filtros são combinados: título E status E prioridade. Clicar em Filtrar retorna à primeira página; Anterior e Próxima preservam os filtros. Limpar filtros remove todos.

As contagens consideram TODOS os resultados dos filtros, não apenas os dez itens visíveis. Se você selecionar Aberto, os números de Em atendimento e Resolvidos serão zero. Esse comportamento é indicado acima do painel.

O solicitante vê somente os próprios chamados, incluindo as contagens. O técnico vê a fila geral. O ID de um solicitante enviado manualmente na URL não muda esse escopo.

Uma página maior que a última é ajustada para a última disponível; uma pesquisa vazia mostra zero resultados. Parâmetros inválidos, como Pagina=0 ou Prioridade=99, retornam HTTP 400.

### Comentários

O solicitante original pode comentar seu chamado. O técnico precisa estar atribuído ao chamado e manter o perfil Técnico. Outros técnicos podem consultar a fila, mas não comentar esse atendimento.

Comentários são permitidos também em chamados resolvidos. Não mudam status, prioridade ou solução. Ao reabrir, a atribuição é removida; o técnico anterior deixa de poder comentar até assumir novamente.

O comentário deve conter entre 1 e 2000 caracteres após remover espaços das extremidades. A interface também limita a entrada a 2000 caracteres. Autor e horário vêm da sessão e do servidor. HTML é exibido como texto, sem execução.

Cada comentário entra como evento no histórico, com sequência, autor e horário UTC. Não há edição, exclusão ou anexos nesta etapa.

## Roteiro de estudo e demonstração

1. Entre como solicitante e abra três chamados com prioridades diferentes.
2. Confira a ordem na lista e as contagens.
3. Combine uma palavra do título, um status e uma prioridade.
4. Limpe os filtros e confira os números novamente.
5. Para experimentar a segunda página, crie mais de dez chamados.
6. Abra um chamado e envie um comentário; veja o registro no histórico.
7. Entre como técnico. Antes de assumir, o formulário de comentário não aparece.
8. Assuma o atendimento e comente. O histórico registra a conta correta.
9. Resolva o chamado e observe a mudança nas contagens.
10. Entre com outro solicitante: ele não pode acessar esse chamado.

## Como o código funciona

Leia nesta ordem:

1. **Chamado.cs:** enum PrioridadeChamado e método Comentar. As regras do comentário estão no domínio.
2. **ConsultasChamados.cs:** PesquisarAsync restringe o escopo, aplica filtros, agrupa contagens e busca apenas a página solicitada.
3. **ChamadosController.cs:** a sessão define o escopo da consulta e o autor do comentário. Não é o formulário que escolhe a identidade.
4. **Index.cshtml:** o formulário GET mantém a pesquisa na URL; links de paginação transportam os filtros.
5. **Detalhes.cshtml:** formulário de comentário separado do formulário de solução.
6. **ConsultasTests.cs e OperacaoWebTests.cs:** exemplos de paginação, contagens, isolamento e tentativas de adulteração.

O banco executa filtros, ordenação e paginação antes de materializar os resultados. Não carregamos a fila inteira para depois selecionar dez registros em memória.

Contagens e itens da página são lidos na mesma transação, mantendo consistência dentro de uma resposta. Mudanças entre duas requisições ainda podem alterar a composição das páginas; não há uma fotografia fixa de toda a navegação.

## Testes e conferência

```powershell
dotnet test CentralChamados.slnx -c Release
```

**87 testes aprovados:** 23 de domínio, 25 de persistência e 39 HTTP/Identity.

Cobertura desta etapa: prioridades válidas/inválidas, preservação de dados antigos, filtros combinados, contagens por escopo, páginas sem duplicação em dados estáveis, página fora do limite, caracteres literais, autorização e validação dos comentários, rollback e codificação HTML.

No navegador, foram conferidos login de teste, painel, segunda página, filtros combinados, comentário vazio e comentário válido. O painel também foi inspecionado em largura de 390 px, sem transbordamento horizontal. A demonstração usou um banco isolado com dados fictícios.

## Próxima etapa

Preparar a apresentação no GitHub: README com capturas, roteiro de demonstração, questões de entrevista e execução confirmada do CI. A Central ainda não foi publicada nem hospedada.
