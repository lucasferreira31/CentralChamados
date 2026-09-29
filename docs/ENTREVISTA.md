# Perguntas para estudar e explicar o projeto

Este material serve para estudo. No currículo, descreva como projeto pessoal e cite apenas o que consegue demonstrar.

1. **Por que MVC neste projeto?** O servidor recebe formulários e devolve HTML com Razor. O objetivo é demonstrar uma aplicação web completa, complementando o projeto de API.
2. **Autenticação e autorização são iguais?** Autenticação valida a conta. Autorização verifica se ela pode acessar o recurso e executar a ação.
3. **Por que não confiar em AutorId no formulário?** O cliente pode adulterá-lo. O controller obtém a conta da sessão e usa o participante vinculado.
4. **Por que conta e participante são entidades diferentes?** A conta gerencia acesso; o participante mantém identidade de domínio e vínculos históricos.
5. **Por que transações?** Estado e evento precisam ser gravados juntos. Um erro deve desfazer ambos; há testes que provocam falhas de gravação.
6. **Como impedir dois técnicos de assumir o mesmo chamado?** A operação lê e altera dentro de uma transação de escrita SQLite, revalidando o estado no domínio.
7. **Como as contagens não vazam dados alheios?** O escopo autorizado é aplicado antes dos filtros, agrupamento e paginação.
8. **Por que ordenar também por ID?** Título e prioridade podem empatar. Um desempate único mantém páginas estáveis enquanto os dados não mudam.
9. **Por que não carregar tudo e paginar em memória?** O banco executa Skip/Take antes da materialização, limitando os itens retornados.
10. **Como comentários evitam execução de HTML?** Razor codifica o texto. Não usamos Html.Raw para conteúdo do usuário.
11. **Antifalsificação substitui login?** Não. O token protege contra POSTs forjados por outros sites; a sessão e a autorização resolvem problemas diferentes.
12. **Quais testes são mais representativos?** Acesso a chamado alheio, autor forjado, concorrência, rollback, filtros combinados e migration com dados antigos.
13. **Por que SQLite?** Facilita a execução local sem servidor adicional. Não afirmamos suporte testado a SQL Server.
14. **O que falta para hospedagem?** Definir ambiente, HTTPS, proxy, persistência, backups, chaves de Data Protection, recuperação de conta e operação.
15. **O que você melhoraria com mais tempo?** Recuperação de senha, confirmação de e-mail, busca apropriada para português, histórico paginado e SLA com regra de negócio explícita.

Exemplo de descrição para currículo:

“Projeto pessoal: aplicação de chamados em ASP.NET Core MVC com Identity, EF Core e SQLite; perfis de acesso, histórico transacional, filtros, paginação e 87 testes automatizados.”

Não apresente o projeto como experiência de estágio ou sistema comercial em produção.
