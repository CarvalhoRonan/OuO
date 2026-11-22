# Validação do OuO 1.0.0.107

OneDrive com fotos/imagens e vídeos ordenados por tamanho, paginação, comparação da cota com os arquivos listados e histórico de versões. A saída no aviso inicial foi corrigida. A compilação é portátil e permanece sem assinatura.

## Verificações desta compilação

- 369 verificações automatizadas aprovadas e 26 telas conferidas. Os novos casos cobrem MIME e extensões, ordem por tamanho, contagem sem duplicações, arquivos sem tamanho, comparação de quota, paginação segura de versões, cancelamento e continuação repetida.
- O recurso integrado contém apenas os dados do cadastro do aplicativo de computador. O teste de compilação confirmou que campos extras de sessão são descartados.
- Com o rclone oficial, os pedidos de autorização dos dois serviços iniciaram sem entrada em terminal, pediram somente leitura e terminaram após cancelamento. O teste do Google nesta compilação usou o cadastro integrado real, sem efetuar login.
- A interface mostra categorias e saldo sem explicação. Foram conferidos filtros de fotos e vídeos, pastas com tamanhos correspondentes ao filtro, histórico e navegação de uma lista com mais de duas páginas. Os casos anteriores de temas e consulta de cota permanecem aprovados.
- O projeto Google está externo no modo Testando. A API do Drive foi ativada, o cliente de computador foi criado, o escopo foi salvo e a conta autorizada foi adicionada ao grupo de teste. A revisão para distribuição pública ainda não foi realizada.
- O motor de produção consultou uma conta OneDrive pessoal autorizada, com listagem completa, tipos de arquivo e ordenação por tamanho conferidos. A leitura do histórico de um arquivo também foi aprovada, sem baixar conteúdo.
- A atualização de cota levou 1.544 segundos nessa rodada. A leitura completa levou 573.657 segundos em uma conta com muitos arquivos. Os filtros usam o inventário em memória; não fazem uma nova leitura remota. Tempos variam conforme conta e conexão.
- O erro Hwnd de zero foi reproduzido na versão anterior por um ensaio de saída no aviso inicial. A compilação corrigida passou pela recusa no botão Sair, fechamento do aviso e recusa ao iniciar pela bandeja. Os três casos encerraram sem iniciar monitoramento de hardware e sem exceção. Os auxiliares de reprodução ficam em source/checks.
- Google Fotos individual foi adiado por escolha do usuário; as alternativas são descritas em GOOGLE-FOTOS-OPCOES.md. O saldo Google sem serviço identificado permanece sem atribuição automática.
- Dados pessoais consultados, nomes de arquivos e autorizações não acompanham o relatório público. Outras contas, cofres, compartilhamentos, falhas de rede e demais cenários ainda sem ensaio seguem em fase de testes.

## Defender e limites da qualificação

- O Defender não encontrou ameaças na análise específica do executável desta compilação antes dos testes. Saída do verificador: 0. Horário UTC: 2026-11-18T02:53:42.0283494Z. Esse resultado se refere ao arquivo e às definições disponíveis naquele momento.
- A análise específica do ZIP é feita depois de sua criação e registrada junto ao pacote, com seu SHA-256.
- O alerta Behavior:Win32/Persistence.A!ml foi reproduzido no pacote anterior 1.0.0.102. Sua classificação segue sem decisão da Microsoft. A análise limpa desta prévia não determina a causa daquele alerta.
- MSI/Office/reparos do sistema e os demais grupos mantêm as limitações descritas em STATUS-DO-ESCOPO.md. Esta entrega não qualifica o produto inteiro como estável.

SHA-256 do executável: E5FE6256BCACD55371CF751AC39B3ECD2919AFE5A3049A6ACACE7380F1E29CF0