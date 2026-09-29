# Etapa 2 — banco e identidade dos participantes

Nesta etapa, os chamados deixam de desaparecer quando o programa termina. Vamos estudar a mudança em partes pequenas.

## 1. Executar

Abra o PowerShell na pasta principal do projeto:

```powershell
Set-Location -LiteralPath 'D:\Curso programação .NET -exercicios\central-chamados'
dotnet run --project src/CentralChamados.BancoDemo -- init
dotnet run --project src/CentralChamados.BancoDemo -- demo
dotnet run --project src/CentralChamados.BancoDemo -- listar
```

O comando init cria as tabelas pelas migrations. O demo grava uma sequência de ações. O listar abre o mesmo arquivo em outro processo e mostra os dados anteriores.

Procure quatro eventos: abertura, início de atendimento, resolução e reabertura. O estado final é Aberto. O histórico continua mostrando a solução anterior.

Cada execução de demo cria dois participantes e um chamado novos. Os dados ficam em .dados/chamados.db. Não envie esse arquivo ao GitHub.

## 2. Por que trocar nomes por IDs?

Na etapa 1, o domínio comparava o nome de quem tentava resolver o chamado com o nome do técnico. Isso confunde homônimos e impede uma renomeação segura.

Agora cada Usuario tem um Guid estável. Chamado guarda SolicitanteId e TecnicoId. A regra compara IDs.

Exemplo: Ana assume um atendimento e depois muda o nome para Ana Silva. Ela mantém o mesmo ID e continua responsável. Outra Ana, com ID diferente, não pode resolver esse chamado.

Ter um ID não prova que alguém é essa pessoa. Autenticação será implementada em uma etapa posterior.

## 3. Nome atual e nome histórico

O resultado da consulta mostra o nome atual do participante. Cada evento mantém AutorId e uma cópia do nome usada no momento da ação.

Por isso a demonstração mostra:

| Evento | Nome registrado |
|---|---|
| Atendimento iniciado | Ana (demonstração) |
| Chamado resolvido | Ana Silva (demonstração) |

A renomeação não reescreve o passado. As propriedades Solicitante e Tecnico na entidade guardam os nomes capturados na abertura e atribuição; a consulta usa Usuarios para mostrar os nomes atuais.

## 4. Como o EF Core grava?

Leia nesta ordem:

1. src/CentralChamados.Domain/Usuario.cs: identidade e renomeação.
2. src/CentralChamados.Domain/Chamado.cs: transições e registro de eventos.
3. src/CentralChamados.Persistence/ChamadosDbContext.cs: tabelas, relacionamentos e restrições.
4. src/CentralChamados.Persistence/ChamadosService.cs: carregar, executar a regra e salvar.
5. src/CentralChamados.BancoDemo/Program.cs: exemplo de uso.

DbContext representa uma sessão de acesso ao banco. Cada operação do serviço usa um contexto novo. A migration Inicial descreve a criação das tabelas Usuarios, Chamados e Eventos.

Os construtores privados permitem que o EF materialize as entidades. Os métodos públicos continuam controlando as transições; não foi criado um setter público de Status.

## 5. Preservar a ordem dos eventos

Dois eventos podem ter o mesmo horário. Por isso existe Sequencia: 0, 1, 2 e assim por diante, dentro de cada chamado.

O banco impede repetir a mesma sequência para o mesmo chamado. Ao recarregar, o serviço ordena por essa sequência antes de usar as regras do domínio.

As datas são armazenadas como ticks UTC. O instante é preservado; o fuso original não é armazenado. TimeProvider permite que os testes controlem o relógio sem depender do horário real da máquina.

## 6. Gravar estado e histórico juntos

Resolver um chamado altera Status e Solucao e acrescenta um evento. Esses dados pertencem à mesma transação.

Se o evento não puder ser salvo, a resolução também deve ser desfeita. O teste FalhaNoEventoDesfazMudancaDoChamadoEPermiteNovaTentativa provoca uma falha no SQLite e confirma que o chamado continua EmAtendimento, sem solução e com o histórico anterior.

As operações de escrita reservam a escrita no SQLite antes de ler o estado. Duas tentativas simultâneas de assumir o mesmo chamado não podem sobrescrever uma à outra. A consulta detalhada usa uma transação de leitura para manter resumo e eventos consistentes.

Isso é uma implementação para SQLite local, não uma garantia de alto volume de acessos. Uma espera por banco ocupado pode terminar em erro após o limite configurado no provedor.

## 7. Testar

```powershell
dotnet test CentralChamados.slnx --configuration Release
dotnet test tests/CentralChamados.Persistence.Tests --filter Renomear
```

São 31 testes: 17 de domínio e 14 com persistência. Os testes de banco usam arquivos temporários e as migrations reais, sem modificar seu banco de demonstração.

Experimente explicar três casos:

- Por que renomear Ana não remove sua responsabilidade?
- Por que outra pessoa também chamada Ana não pode resolver?
- Se falhar a gravação do evento, o que fica salvo?

## 8. Próxima etapa

Vamos criar as telas com ASP.NET Core MVC e Razor Views: lista, detalhes, abertura e atendimento. O banco e o serviço desta etapa serão reutilizados.

Ainda não há tela nem conta com senha. Não apresente esta entrega como aplicação web pronta. A etapa 1 fica registrada no ZIP anterior; os exemplos e testes atuais já usam os IDs.
