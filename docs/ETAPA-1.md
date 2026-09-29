# Etapa 1 — ciclo de vida de um chamado

Este guia registra a primeira etapa. O código atual já usa usuários com IDs e eventos persistentes; veja [a etapa 2](ETAPA-2.md). O ZIP da etapa 1 preserva a versão original.

## Objetivo

Entender que nem toda mudança de estado é permitida. Um enum sozinho não protege o fluxo: são os métodos da entidade que verificam as transições.

## O fluxo

```text
Aberto → IniciarAtendimento → EmAtendimento → Resolver → Resolvido
  ↑                                                       |
  └────────────────────── Reabrir ─────────────────────────┘
```

Uma reabertura limpa o responsável e a solução atuais, mas mantém os eventos anteriores no histórico. Um novo atendimento pode então começar com outro técnico.

## Status com enum

`StatusChamado` evita escrever estados arbitrários como texto livre. Você escolhe entre os valores conhecidos pelo código.

A propriedade possui `private set`: outro arquivo não pode pular as verificações e marcar um chamado como resolvido diretamente.

## Histórico protegido

A lista interna é privada. A propriedade pública devolve uma coleção somente para leitura. Cada `EventoChamado` é um record com data, autor e descrição.

Isso permite consultar a trajetória sem que quem recebeu a coleção remova eventos diretamente.

## Tempo controlado

Os métodos recebem `DateTimeOffset agora`. Nos testes, passamos datas fixas; a demonstração usa o horário atual.

O resultado é repetível: um teste não depende da hora em que foi executado. A validação também impede um evento anterior ao último registro.

## Validação não é autenticação

A comparação entre o nome informado e o responsável é uma regra didática do domínio. Ela não prova quem está usando o sistema.

Na futura aplicação web, o servidor obterá o identificador do usuário autenticado. Não basta confiar em um campo do formulário dizendo “sou a técnica Ana”.

## Ler o teste de reabertura

Abra `ReaberturaPreservaSolucaoAnteriorNoHistorico`. O teste verifica que:

1. O chamado resolvido pode ser reaberto.
2. A atribuição atual é limpa.
3. A solução anterior continua no histórico.
4. Outro técnico pode assumir o novo atendimento.

## Execute e investigue

```powershell
dotnet run --project src/CentralChamados.Demo
dotnet test CentralChamados.slnx
```

Coloque um breakpoint em `Resolver` e compare o estado antes e depois da tentativa feita por outro técnico.

## Exercícios pequenos

1. Acrescente um teste para impedir reabertura sem motivo.
2. Acrescente um teste para impedir título com mais de 100 caracteres.
3. Modifique a demonstração para reabrir o chamado e iniciar um novo atendimento.
4. Explique por que o evento antigo precisa permanecer, mesmo quando `Solucao` volta a ser nulo.

## Critério para avançar

Você consegue explicar estado, transição, encapsulamento do histórico e a diferença entre validar um nome e autenticar uma pessoa. A próxima etapa será persistir chamado, usuários e eventos.
