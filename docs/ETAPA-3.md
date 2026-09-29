# Etapa 3 — Central de Chamados no navegador

Projeto principal: D:\Curso programação .NET -exercicios\central-chamados

## O que você construiu

Uma aplicação ASP.NET Core MVC com Razor Views. É o complemento visual para sua API de Pedidos & Estoque: aqui o servidor devolve páginas HTML e recebe formulários.

- Cadastro de participantes da demonstração.
- Lista e abertura de chamados.
- Detalhes com histórico, técnico responsável e solução.
- Formulários para assumir, resolver e reabrir.
- Layout responsivo, rótulos e erros junto aos campos.
- Proteção antifalsificação em todos os POSTs de negócio.

Ainda não há login. Qualquer pessoa com acesso à demonstração pode selecionar os participantes e consultar os chamados. Execute localmente; autenticação e autorização serão a etapa 4.

## Executar, passo a passo

Abra o PowerShell e execute:

```powershell
Set-Location "D:\Curso programação .NET -exercicios\central-chamados"
dotnet run --project src/CentralChamados.Web -- --init
dotnet run --project src/CentralChamados.Web
```

O primeiro comando do projeto aplica as migrations e encerra, sem apagar registros. O segundo inicia o site em http://localhost:5287. Deixe o terminal aberto; Ctrl+C encerra o servidor.

O banco padrão da aplicação web fica em src/CentralChamados.Web/.dados/chamados.db, relativo ao ContentRoot do projeto web. É separado do banco padrão do console. Para compartilhar um banco existente ou escolher outro, defina o caminho absoluto antes de ambos os comandos:

```powershell
$env:CHAMADOS_DB = "D:\MeusBancos\central-chamados.db"
dotnet run --project src/CentralChamados.Web -- --init
dotnet run --project src/CentralChamados.Web
```

Não há participantes pré-cadastrados nem migração automática ao iniciar o servidor. Abra Participantes e cadastre duas pessoas de demonstração.

## Roteiro de cinco minutos

1. Cadastre um solicitante e uma técnica.
2. Abra um chamado escolhendo o solicitante.
3. Nos detalhes, escolha a técnica e clique em Assumir atendimento.
4. Escolha a mesma técnica, escreva uma solução e registre.
5. Escolha o solicitante original e reabra, explicando o motivo.
6. Confira os quatro eventos. A solução anterior continua no histórico.
7. Pare o servidor e inicie de novo: o chamado continua no banco.

Experimente também enviar um formulário vazio e tentar resolver com outro participante. O formulário mostra o erro e o histórico não muda.

## Entendendo MVC neste projeto

**Model:** os formulários em Models/Formularios.cs descrevem os campos aceitos e suas validações de entrada. O domínio continua sendo responsável pelas regras de negócio.

**Controller:** ChamadosController recebe o pedido HTTP, verifica ModelState, chama ChamadosService e escolhe a tela ou o redirecionamento.

**View:** os arquivos .cshtml em Views/Chamados transformam os dados em HTML. Razor codifica o conteúdo informado pelo usuário, evitando renderizá-lo como scripts.

Fluxo de abertura: formulário → POST Abrir → validação → serviço → domínio/SQLite → redirecionamento → GET Detalhes.

Esse redirecionamento após salvar é o padrão Post/Redirect/Get: atualizar a página de detalhes não repete o POST anterior.

Os erros aparecem após enviar o formulário. A validação do servidor funciona sem JavaScript. Os limites de tamanho também aparecem nos elementos HTML.

O token antifalsificação vincula o formulário ao navegador que o recebeu. Ele ajuda a bloquear POSTs forjados por outros sites, mas não identifica o usuário e não substitui login.

## O que estudar primeiro

1. Leia AberturaForm e identifique Required e StringLength.
2. Compare as duas ações Abrir do controller: GET exibe; POST valida e salva.
3. Encontre asp-for, asp-action e asp-validation-for em Abrir.cshtml.
4. Siga a chamada AbrirAsync até o construtor de Chamado.
5. Leia FluxoCompletoPeloHttpPersisteHistorico para entender um teste que atravessa a aplicação inteira.

## Validação realizada

```powershell
dotnet test CentralChamados.slnx -c Release
```

42 testes aprovados: 17 de domínio, 14 de persistência e 11 HTTP. Os novos testes verificam o fluxo completo, validação, participante inexistente, técnico incorreto, estado desatualizado, antifalsificação, 404 e codificação HTML.

Também foi percorrido o fluxo completo no navegador com um banco separado de teste. Layout verificado em desktop e viewport de 390 px, sem transbordamento horizontal na tela de detalhes.

## Próxima etapa

ASP.NET Core Identity: cadastro/login, cookies, perfis Solicitante e Técnico e restrição de acesso por usuário. A identidade usada nas ações passará a vir da sessão autenticada.
