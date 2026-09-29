# Etapa 4 — Login e permissões na Central de Chamados

Pasta principal: D:\Curso programação .NET -exercicios\central-chamados

## O que mudou

Antes, a tela permitia escolher quem estava fazendo a ação. Agora, cadastro e login são gerenciados pelo ASP.NET Core Identity. O controller obtém o participante a partir da conta autenticada.

O solicitante vê apenas os próprios chamados. O técnico vê a fila geral; para resolver, precisa ser o responsável pelo atendimento. Apenas o solicitante original pode reabrir.

## Executar

No PowerShell:

```powershell
Set-Location -LiteralPath "D:\Curso programação .NET -exercicios\central-chamados"
dotnet run --project src/CentralChamados.Web -- --init
dotnet run --project src/CentralChamados.Web
```

Abra http://localhost:5287. O acesso à lista redireciona para Entrar. Use Criar conta para cadastrar seu acesso.

O comando --init aplica migrations e cria os perfis Solicitante e Tecnico. Pode ser repetido sem apagar dados ou duplicar perfis. Execute-o também ao atualizar a partir da etapa 3.

O banco web padrão continua em src/CentralChamados.Web/.dados/chamados.db. Para escolher outro arquivo, configure um caminho absoluto antes de TODOS os comandos:

```powershell
$env:CHAMADOS_DB = "D:\MeusBancos\central-chamados.db"
```

Não há senha padrão, conta automática ou senha escrita na configuração. Escolha suas credenciais na tela; não use dados pessoais reais para a demonstração.

## Experimentar os dois perfis

1. Crie uma conta de solicitante, entre e abra um chamado.
2. Saia e crie uma segunda conta para experimentar o atendimento técnico.
3. Pare o servidor com Ctrl+C. Na mesma pasta e usando o mesmo CHAMADOS_DB, execute o comando abaixo com o e-mail da segunda conta.
4. Inicie novamente e entre com a segunda conta. Ela verá a fila geral.
5. Assuma o chamado e registre a solução.
6. Saia, entre como solicitante e reabra, informando o motivo.

```powershell
dotnet run --project src/CentralChamados.Web -- --promover-tecnico "tecnico@example.test"
dotnet run --project src/CentralChamados.Web
```

O e-mail acima é apenas exemplo: use uma conta que você realmente cadastrou nessa aplicação.

A promoção é uma operação administrativa local: quem tem acesso ao banco e ao terminal já controla a aplicação. Não existe endpoint HTTP para promover perfis. A operação invalida as sessões antigas da conta, exigindo novo login.

Um técnico também conserva o perfil Solicitante e pode abrir chamados próprios.

## Entendendo a implementação

**Autenticação** responde quem entrou. UserManager cria contas e hashes de senha; SignInManager valida a senha e emite o cookie de sessão.

**Autorização** responde quais ações essa conta pode realizar. O atributo Authorize exige login e, em ações específicas, o perfil Tecnico. O controller também verifica o vínculo com o chamado. Ter um papel não autoriza automaticamente qualquer alteração.

Esconder um botão ajuda a orientar a tela, mas não protege a operação. Por isso, as mesmas verificações existem no servidor e são testadas com requisições HTTP.

| Operação | Solicitante | Técnico |
|---|---|---|
| Abrir chamado | Na própria conta | Na própria conta |
| Consultar lista/detalhes | Apenas próprios | Fila geral |
| Assumir chamado aberto | Não | Sim |
| Resolver | Não | Apenas se for o responsável |
| Reabrir resolvido | Apenas próprio | Apenas se for o solicitante original |

Um ID de chamado alheio retorna 404 para o solicitante. Uma ação incompatível com o perfil é recusada e leva à página de acesso negado.

## Conta e participante são diferentes

A tabela Usuarios continua representando o participante do domínio. AspNetUsers guarda a conta Identity e referencia exatamente um participante por ParticipanteId, com chave estrangeira e índice único.

Cadastrar cria participante, conta e perfil numa única transação. Se uma etapa falhar, tudo é desfeito.

Os chamados antigos e o histórico são preservados pela migration. Participantes antigos NÃO recebem contas automaticamente. Cadastrar alguém com o mesmo nome não concede acesso ao histórico dessa pessoa.

Chamados antigos ficam visíveis na fila de técnicos; se estavam atribuídos a participantes sem conta, não podem ser resolvidos por outra identidade. Para experimentar o fluxo autenticado completo, abra novos chamados com as novas contas. Uma futura vinculação de dados legados exigirá um procedimento administrativo explícito.

## Proteções implementadas e limites

- Senha de 10 a 128 caracteres no cadastro, com maiúscula, minúscula, número e símbolo.
- Senhas armazenadas como hash pelo Identity.
- Cookie HttpOnly, SameSite=Lax e validade renovável de 30 minutos, sem opção de login persistente.
- Cinco senhas incorretas bloqueiam novas tentativas da conta por cinco minutos.
- Login e cadastro compartilham limite local de 20 POSTs por minuto por endereço IP; excesso retorna HTTP 429.
- Todos os POSTs de negócio, inclusive login, cadastro e saída, exigem token antifalsificação.
- Retorno após login aceita apenas URL local.
- Páginas autenticadas enviam Cache-Control: no-store.
- Security stamp validado a cada requisição nesta aplicação pequena, para invalidar sessões após a promoção.
- Campos de senha retornam vazios quando a validação falha.

O perfil Local usa HTTP somente para desenvolvimento em localhost. Fora de Development, a aplicação usa redirecionamento HTTPS, HSTS e cookie Secure. Hospedagem e configuração de proxy, certificado, chaves de Data Protection e persistência ainda serão tratadas na etapa de publicação.

Ainda não há confirmação de e-mail, recuperação de senha, MFA ou tela administrativa de perfis. O e-mail cadastrado não é prova de propriedade da caixa postal. Use a versão como demonstração local.

## Como estudar em pequenas etapas

1. Abra Conta.cs e localize ParticipanteId.
2. Leia o método CadastrarAsync: encontre a transação e o perfil fixo Solicitante.
3. Leia PasswordSignInAsync no ContaController e a configuração de cookie no Program.cs.
4. No ChamadosController, encontre GetUserAsync e compare com os antigos seletores de participante.
5. Leia PodeVer e os atributos Authorize das ações Iniciar e Resolver.
6. Execute os testes e leia SolicitanteNaoListaNemConsultaNemReabreChamadoAlheio.
7. Explique em voz alta por que enviar AutorId no formulário não muda mais a identidade.

## Validação

```powershell
dotnet test CentralChamados.slnx -c Release
```

60 testes aprovados: 17 de domínio, 14 de persistência e 29 HTTP/Identity.

Os testes incluem cookies reais do Identity, fluxo completo com dois perfis, hash de senha, tentativa de forjar perfil/participante/autor, acesso a chamado alheio, antifalsificação, logout, bloqueio de login, retorno externo, rollback do cadastro e migration sobre banco antigo.

O comando --init foi executado duas vezes em banco separado. As telas públicas e os erros do login foram conferidos no navegador; o cadastro foi inspecionado em largura de 390 px.

## Referências oficiais

- [Introdução ao ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Configuração de senha, bloqueio e cookies](https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)

## Próxima etapa

Prioridade, filtros, paginação, comentários e painel de contagens. Depois, documentação de demonstração, CI confirmado e publicação do projeto.
