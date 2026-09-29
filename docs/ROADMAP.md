# Plano de evolução — Central de Chamados

## Etapa 1 — fluxo e histórico (concluída)

Entidade Chamado, estados, regras de atribuição/resolução/reabertura, exemplo em console e testes. Dados em memória.

## Etapa 2 — banco e identidade dos participantes (concluída)

Adicionar EF Core, SQLite e migrations. Substituir nomes como identificadores por IDs de usuários. Persistir eventos relacionados ao chamado.

Critérios: o histórico continua após reiniciar; alteração de nome de uma pessoa não quebra sua relação com chamados anteriores; os testes verificam os relacionamentos.

## Etapa 3 — aplicação web (concluída)

Adicionar ASP.NET Core MVC e Razor Views: lista, detalhes, abertura e formulário de atendimento. Construir HTML/CSS responsivos, rótulos de formulários e mensagens de validação.

Critérios: uma pessoa consegue abrir e acompanhar um chamado pelo navegador; dados inválidos aparecem junto ao campo; o layout funciona em tela estreita.

## Etapa 4 — autenticação e autorização (concluída)

Usar ASP.NET Core Identity e cookies, mantendo a proteção antifalsificação já implementada na etapa 3. Definir perfis Solicitante e Técnico.

Critérios: solicitante vê apenas seus chamados; técnico vê a fila autorizada; modificar o ID na URL não permite acessar registros alheios; testes verificam essas restrições.

## Etapa 5 — operação e consultas (concluída)

Adicionar prioridade, filtros, paginação, comentários e painel com contagens por status. Definir explicitamente a regra de prazo antes de implementar um indicador de atraso.

Critérios: filtros combinados retornam dados corretos; números do painel correspondem ao banco; comentários não permitem executar HTML/JavaScript arbitrário na página.

## Etapa 6 — demonstração e publicação

README com capturas de tela, CI confirmado, conta demonstrativa sem dados pessoais e roteiro de cinco minutos. Se houver hospedagem, preparar configuração de produção e persistência dos dados.

Não incluir anexos, envio de e-mail ou integrações externas antes de o fluxo principal funcionar e estar coberto por testes.
