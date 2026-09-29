# Demonstração em cinco minutos

Prepare antes: SDK .NET 10, banco inicializado e duas contas fictícias cadastradas pela tela. Use e-mails de exemplo, como solicitante@example.test e tecnico@example.test; escolha as senhas localmente. Não há senha padrão ou conta automática.

Na raiz do projeto:

```powershell
dotnet run --project src/CentralChamados.Web -- --init
dotnet run --project src/CentralChamados.Web
```

Acesse http://localhost:5287, cadastre as duas contas, encerre o servidor e promova somente a conta de técnico:

```powershell
dotnet run --project src/CentralChamados.Web -- --promover-tecnico "tecnico@example.test"
dotnet run --project src/CentralChamados.Web
```

## 0:00 — O problema

“Este é um projeto pessoal de atendimento interno. O solicitante acompanha suas solicitações; o técnico gerencia a fila. O histórico registra quem fez cada ação.”

## 0:30 — Abrir e consultar

Entre como solicitante e abra um chamado de prioridade Alta. Mostre os filtros e as contagens. Explique que a contagem considera todos os resultados filtrados e autorizados, não só a página atual.

## 1:30 — Atender

Saia e entre como técnico. Assuma o chamado, adicione um comentário e registre uma solução. Mostre autor, sequência e horário no histórico.

## 2:30 — Reabrir

Volte à conta solicitante e reabra com um motivo. Explique que a solução anterior fica no histórico, mas a atribuição atual é removida.

## 3:15 — Demonstrar autorização

Use uma terceira conta fictícia de solicitante. Cole o endereço do chamado: o servidor responde 404. O teste automatizado também verifica adulteração do autor e do perfil enviados no formulário.

## 4:00 — Mostrar engenharia

Abra o domínio, o serviço de consultas e um teste HTTP. Mostre que regras, persistência e interface têm responsabilidades diferentes. Execute:

```powershell
dotnet test CentralChamados.slnx -c Release
```

São 87 testes. No repositório, a aba Actions apresenta o resultado de build e testes em Windows e Linux.

## 4:45 — Explicar limites

Prioridade ainda não é SLA. Não há recuperação de senha, confirmação de e-mail, anexos ou hospedagem pública. O projeto demonstra o fluxo e suas regras; use dados fictícios para apresentação.

Para explicar paginação visualmente, prepare mais de dez chamados antes da demonstração. O projeto possui testes para páginas, filtros e ordenação estável.
